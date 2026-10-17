using System.Numerics;
using Avalonia;
using Silk.NET.OpenGL;
using AvaloniaSilkEffects.Tempera.Rendering;

namespace AvaloniaSilkEffects.Tempera;

/// <summary>All the inputs of one song; a different <see cref="Seed"/> is a real track change.</summary>
public sealed record TemperaSongContext(string Seed, TemperaProgram Program, TemperaTheme Theme, TemperaSongMetadata Metadata)
{
    /// <summary>Cover colours for the gradient colour mode; empty falls back to the theme hues.</summary>
    public IReadOnlyList<TemperaRgb> CoverColors { get; init; } = [];
}

public sealed class TemperaSceneOptions
{
    public TemperaTuning Tuning { get; init; } = new();
    /// <summary>Folia staticMode: no echoes, no transitions, no post-processing.</summary>
    public bool StaticMode { get; init; }
    public double LyricsFontScale { get; init; } = 1;
}

/// <summary>
/// 凝彩 (Tempera) for <see cref="SilkEffectControl"/> — Folia's createTemperaPixiRuntime.ts. Time is the frame's elapsed clock
/// (the host seeks it to the playback position). Per frame it picks the paragraph, keeps it ±1 cached, does at most one
/// expensive thing (free a retired scene, pre-roll the next, build the credits, pre-roll the previous), poses every visible
/// scene and shot from absolute time, and renders: scenes (each through its own post chain when it has one), the credits
/// card, then the corner marks and the wipe block. The canvas stays transparent, so the cut-outs show the host background.
/// </summary>
public sealed class TemperaEffectScene(TemperaSongContext song, TemperaSceneOptions? options = null) : EffectScene
{
    /// <summary>How long before the final paragraph ends the credits card is built.</summary>
    private const double CreditsPrebuild = 4;
    private const double BlurActiveStrength = 0.01;

    private sealed class ScenePose
    {
        public required TemperaSceneView Scene;
        public int Index;
        public TemperaTransitionFrame Frame;
        public double Alpha;
    }

    private readonly Dictionary<int, TemperaSceneView> _cache = [];
    private readonly List<TemperaSceneView> _retired = [];
    private readonly List<ScenePose> _poses = [];
    private readonly TemperaOverlay _overlay = new();
    private TemperaRenderer? _renderer;
    private TemperaSurface? _main;
    private TemperaSurface? _sceneSurface;
    private TemperaCredits? _credits;
    private TemperaCreditsFrame _creditsFrame;
    private bool _creditsVisible;
    private TemperaSongContext? _pending;
    private (TemperaSongContext Song, int Index, TemperaSceneView Scene)? _staged;
    private PixelSize _pixelSize;
    private Vector2 _logical;
    private int _activeIndex = -1;
    private bool _overlayDrawn;
    private bool _isGles;

    public TemperaSongContext Song { get; private set; } = song;
    public TemperaSceneOptions Options { get; } = options ?? new TemperaSceneOptions();
    public TemperaProgram Program => Song.Program;
    public int ActiveParagraphIndex => _activeIndex;
    public int CachedParagraphCount => _cache.Count;
    public bool CreditsBuilt => _credits is not null;
    public int LastDrawCalls => _renderer?.DrawCalls ?? 0;

    /// <summary>The shot kind on screen at <paramref name="time"/> (debugging).</summary>
    public string? ShotKindAt(double time)
    {
        if (Program.Paragraphs.Count == 0) return null;
        var paragraph = Program.Paragraphs[Program.ParagraphIndexAt(time)];
        var found = paragraph.Shots.FirstOrDefault();
        foreach (var shot in paragraph.Shots) if (shot.StartTime <= time) found = shot;
        return found?.Kind;
    }

    /// <summary>Hands over to another song (or the same song with a new program / theme / metadata).</summary>
    public void SetSong(TemperaSongContext next) => _pending = next;

    public override void Initialize(EffectDevice device)
    {
        base.Initialize(device);
        _renderer = new TemperaRenderer(device.Gl, device.Primitives, device.Textures);
        _main = new TemperaSurface(device.Gl);
        _sceneSurface = new TemperaSurface(device.Gl);
        _isGles = device.OpenGlVersion.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
    }

    public override void Resize(PixelSize pixelSize, double renderScaling)
    {
        var logical = new Vector2(
            MathF.Max(320, (float)(pixelSize.Width / renderScaling)),
            MathF.Max(240, (float)(pixelSize.Height / renderScaling)));
        if (_pixelSize == pixelSize && logical == _logical) return;
        _pixelSize = pixelSize;
        _logical = logical;
        // Every scene, the credits and the overlay are laid out against the viewport: rebuild them all.
        ClearScenes();
        ReleaseStaged();
        InvalidateCredits();
        _overlayDrawn = false;
    }

    private float PixelScale => _logical.X > 0 ? _pixelSize.Width / _logical.X : 1;

    private bool TransitionsEnabled => Options.Tuning.EnableTransitions && !Options.StaticMode;

    private TemperaSceneView BuildScene(TemperaSongContext context, int index) => new(context.Program, index, context.Theme, Options.Tuning,
        _logical.X, _logical.Y, Options.LyricsFontScale, Options.StaticMode, context.CoverColors);

    private void ClearScenes()
    {
        foreach (var scene in _cache.Values) scene.Dispose();
        _cache.Clear();
        foreach (var scene in _retired) scene.Dispose();
        _retired.Clear();
        _activeIndex = -1;
    }

    private void ReleaseStaged()
    {
        _staged?.Scene.Dispose();
        _staged = null;
    }

    private void InvalidateCredits()
    {
        _credits?.Dispose();
        _credits = null;
    }

    private void BuildCredits() =>
        _credits = new TemperaCredits(Song.Theme, Options.Tuning, TemperaPalette.Resolve(Song.Theme, Options.Tuning.ColorMode, Song.CoverColors),
            Song.Metadata, _logical.X, _logical.Y, Options.LyricsFontScale);

    /// <summary>
    /// Song hand-over (Folia PixiSongSwap): the same seed, or nothing to protect, swaps at once; a real track change builds the
    /// incoming scene on one frame while the outgoing song still draws, cuts on the next, and frees the old scenes later.
    /// </summary>
    /// <returns>True when this frame already did its expensive work.</returns>
    private bool AdvanceSongSwap(double time)
    {
        if (_pending is not { } next) return false;
        if (_staged is { } staged)
        {
            Song = staged.Song;
            _pending = null;
            _retired.AddRange(_cache.Values);
            _cache.Clear();
            _cache[staged.Index] = staged.Scene;
            _activeIndex = staged.Index;
            _staged = null;
            InvalidateCredits();
            _overlayDrawn = false;
            return false;
        }
        if (next.Seed == Song.Seed || _cache.Count == 0 || Song.Program.Paragraphs.Count == 0 || next.Program.Paragraphs.Count == 0)
        {
            Song = next;
            _pending = null;
            ClearScenes();
            InvalidateCredits();
            _overlayDrawn = false;
            return false;
        }
        var index = next.Program.ParagraphIndexAt(time);
        _staged = (next, index, BuildScene(next, index));
        return true;
    }

    public override void Update(in EffectFrame frame)
    {
        Device.PostProcess.Reset();
        _poses.Clear();
        _creditsVisible = false;
        if (_renderer is null || _logical.X <= 1) return;
        var time = frame.Elapsed.TotalSeconds;
        var built = AdvanceSongSwap(time);
        if (!_overlayDrawn)
        {
            _overlay.Draw(_logical.X, _logical.Y, Options.Tuning, Song.Theme);
            _overlayDrawn = true;
        }
        _overlay.HideWipe();
        var program = Program;
        if (program.Paragraphs.Count == 0) return;
        var width = (double)_logical.X;
        var height = (double)_logical.Y;

        var paragraphIndex = program.ParagraphIndexAt(time);
        if (paragraphIndex != _activeIndex)
        {
            _activeIndex = paragraphIndex;
            if (!_cache.ContainsKey(paragraphIndex))
            {
                _cache[paragraphIndex] = BuildScene(Song, paragraphIndex);
                built = true;
            }
            foreach (var index in _cache.Keys.ToArray())
            {
                if (Math.Abs(index - paragraphIndex) <= 1) continue;
                _cache[index].Dispose();
                _cache.Remove(index);
            }
        }

        var finalParagraph = program.Paragraphs[^1];
        var hasCredits = Song.Metadata.HasCredits;
        _creditsFrame = TemperaCreditsFrame.At(time, finalParagraph.EndTime);
        if (!built && _staged is null)
        {
            var next = paragraphIndex + 1;
            var previous = paragraphIndex - 1;
            if (_retired.Count > 0)
            {
                _retired[0].Dispose();
                _retired.RemoveAt(0);
            }
            else if (next < program.Paragraphs.Count && !_cache.ContainsKey(next))
                _cache[next] = BuildScene(Song, next);
            else if (hasCredits && _credits is null && time >= finalParagraph.EndTime - CreditsPrebuild)
                BuildCredits();
            else if (previous >= 0 && !_cache.ContainsKey(previous))
                _cache[previous] = BuildScene(Song, previous);
        }
        if (hasCredits && _creditsFrame.Active && _credits is null) BuildCredits();

        var transitionsEnabled = TransitionsEnabled;
        var outgoing = program.Paragraphs[paragraphIndex].TransitionOut;
        // A translating transition needs something on the far side, so the next scene pre-rolls through the window; a wipe
        // instead uncovers the next scene after the boundary.
        var preRoll = transitionsEnabled && outgoing is not null && outgoing.Kind != TemperaTransitionKind.BlockWipe
            && time >= outgoing.StartTime;
        var wipeDrawn = false;
        foreach (var (index, scene) in _cache.OrderBy(entry => entry.Key))
        {
            var isActive = index == paragraphIndex;
            var isIncoming = preRoll && index == paragraphIndex + 1;
            scene.Container.Visible = isActive || isIncoming;
            if (!scene.Container.Visible)
            {
                foreach (var shot in scene.Shots) shot.Container.Visible = false;
                scene.ActiveShotIndex = -1;
                continue;
            }
            var transition = TemperaTransitions.SceneFrame(program, index, time, isIncoming, outgoing, transitionsEnabled);
            var activeShot = scene.FindActiveShotIndex(time);
            for (var shotIndex = 0; shotIndex < scene.Shots.Count; shotIndex++)
            {
                var shot = scene.Shots[shotIndex];
                var handingOff = shotIndex < activeShot && TemperaSceneView.Exit(shot.Shot, time) < 1;
                shot.Container.Visible = shotIndex == activeShot || handingOff;
                if (shot.Container.Visible) TemperaSceneView.UpdateShot(shot, time, width, height, Options.Tuning, Song.Theme);
            }
            scene.ActiveShotIndex = activeShot;
            var isFinal = index == program.Paragraphs.Count - 1;
            var lyricAlpha = isFinal && hasCredits ? _creditsFrame.LyricAlpha : 1;
            scene.Container.Alpha = transition.Alpha * lyricAlpha;
            scene.Container.SetPivot(width / 2, height / 2);
            scene.Container.SetPosition(width / 2 + transition.X * width, height / 2 + transition.Y * height);
            scene.Container.SetScale(transition.Scale);
            scene.Container.Rotation = transition.Rotation;
            _poses.Add(new ScenePose { Scene = scene, Index = index, Frame = transition, Alpha = scene.Container.Alpha });
            if (transition.Wipe > 0.001 && transition.Wipe < 1.999)
            {
                _overlay.DrawWipe(transition.Wipe, transition.WipeAngle, width, height, scene.Palette.Tone3);
                wipeDrawn = true;
            }
        }
        if (!wipeDrawn) _overlay.HideWipe();
        if (_credits is not null && hasCredits)
        {
            _credits.ApplyFrame(_creditsFrame, time, finalParagraph.EndTime, width, height);
            _creditsVisible = _creditsFrame.Active;
        }
    }

    private List<TemperaPass> ScenePasses(TemperaSceneView scene, double blur)
    {
        var passes = new List<TemperaPass>();
        var tuning = Options.Tuning;
        if (tuning.PostProcessEnabled && !Options.StaticMode)
        {
            if (tuning.PostProcessLensDistortion > 0) passes.Add(new TemperaPass(TemperaPassKind.Lens, (float)tuning.PostProcessLensDistortion));
            if (tuning.PostProcessGrain * 0.35 > 0)
                passes.Add(new TemperaPass(TemperaPassKind.Noise, (float)(tuning.PostProcessGrain * 0.35), scene.SceneSeed % 10000 / 10000f));
            if (tuning.PostProcessContrast * 0.5 > 0) passes.Add(new TemperaPass(TemperaPassKind.Contrast, (float)(tuning.PostProcessContrast * 0.5)));
            if (tuning.PostProcessRgbShift > 0) passes.Add(new TemperaPass(TemperaPassKind.RgbShift, (float)tuning.PostProcessRgbShift));
            if (tuning.PostProcessVignette > 0) passes.Add(new TemperaPass(TemperaPassKind.Vignette, (float)tuning.PostProcessVignette));
        }
        if (blur > BlurActiveStrength && TransitionsEnabled)
        {
            passes.Add(new TemperaPass(TemperaPassKind.BlurX, (float)blur));
            passes.Add(new TemperaPass(TemperaPassKind.BlurY, (float)blur));
        }
        return passes;
    }

    public override void Render(EffectRenderContext context)
    {
        if (_renderer is null || _main is null || _sceneSurface is null || _logical.X <= 1) return;
        var gl = Device.Gl;
        var primitives = context.Primitives;
        primitives.Flush();
        gl.GetInteger(GetPName.DrawFramebufferBinding, out var outputFramebuffer);
        Span<int> viewport = stackalloc int[4];
        gl.GetInteger(GetPName.Viewport, viewport);
        if (!_isGles) gl.Enable(EnableCap.Multisample);
        _renderer.BeginFrame();

        var pixelScale = PixelScale;
        var width = context.PixelSize.Width;
        var height = context.PixelSize.Height;
        _main.EnsureSize(width, height, pixelScale);
        _main.Clear();
        var root = Matrix3x2.CreateScale(pixelScale);

        foreach (var pose in _poses)
        {
            if (pose.Alpha <= 0.0005) continue;
            var passes = ScenePasses(pose.Scene, pose.Frame.Blur);
            if (passes.Count == 0)
            {
                _renderer.Draw(pose.Scene.Container, _main, root);
                continue;
            }
            // The whole chain runs at the lowest resolution in it: half the canvas while the transition blur is attached.
            var blurring = passes.Any(pass => pass.Kind == TemperaPassKind.BlurX);
            var sceneScale = blurring ? pixelScale * 0.5f : pixelScale;
            var sceneWidth = Math.Max(1, (int)MathF.Ceiling(_logical.X * sceneScale));
            var sceneHeight = Math.Max(1, (int)MathF.Ceiling(_logical.Y * sceneScale));
            _sceneSurface.EnsureSize(sceneWidth, sceneHeight, sceneScale);
            _sceneSurface.Clear();
            var sceneRoot = Matrix3x2.CreateScale(sceneScale);
            _renderer.Draw(pose.Scene.Container, _sceneSurface, sceneRoot);
            _renderer.RunChain(_sceneSurface, SceneBounds(pose.Scene.Container, sceneRoot, sceneWidth, sceneHeight), passes, _main);
        }

        if (_creditsVisible && _credits is not null) _renderer.Draw(_credits.Layer, _main, root);
        _renderer.Draw(_overlay.Container, _main, root);

        _main.Resolve();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)outputFramebuffer);
        gl.Viewport(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3]);
        _renderer.Present(_main, width, height);
        primitives.SetViewport(new Vector2(width, height));
    }

    /// <summary>The scene's filterArea (the viewport) through its transform, as a pixel AABB clipped to the target (Pixi bounds).</summary>
    private TemperaPixelRect SceneBounds(TemperaGroup container, Matrix3x2 root, int width, int height)
    {
        var world = container.LocalMatrix * root;
        Span<Vector2> corners = [Vector2.Zero, new(_logical.X, 0), _logical, new(0, _logical.Y)];
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (var corner in corners)
        {
            var point = Vector2.Transform(corner, world);
            minX = MathF.Min(minX, point.X);
            minY = MathF.Min(minY, point.Y);
            maxX = MathF.Max(maxX, point.X);
            maxY = MathF.Max(maxY, point.Y);
        }
        return TemperaPixelRect.FromBounds(minX, minY, maxX, maxY, width, height);
    }

    public override void DisposeGpuResources()
    {
        ClearScenes();
        ReleaseStaged();
        InvalidateCredits();
        _overlay.Dispose();
        _overlayDrawn = false;
        _renderer?.Dispose();
        _renderer = null;
        _main?.Dispose();
        _main = null;
        _sceneSurface?.Dispose();
        _sceneSurface = null;
    }
}
