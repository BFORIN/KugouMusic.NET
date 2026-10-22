using System.Numerics;
using System.Diagnostics;
using AvaloniaSilkEffects.Gpu;
using AvaloniaSilkEffects.Tempera.Text;

namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>One scene-wide post pass, in chain order (Folia createSonnetPostProcessFilters + the transition blur).</summary>
internal enum TemperaPassKind
{
    Lens,
    Noise,
    Contrast,
    RgbShift,
    Vignette,
    BlurX,
    BlurY,
}

internal readonly record struct TemperaPass(TemperaPassKind Kind, float Amount, float Seed = 0);

/// <summary>
/// Draws Tempera node trees with Pixi's semantics: container alpha multiplies into descendants, Graphics are static meshes,
/// a text layer with the difference filter renders offscreen and is coloured against a copy of what is already drawn, and a
/// scene with filters renders into its own surface and runs its pass chain over the scene's on-screen bounds.
/// </summary>
internal sealed class TemperaRenderer : IDisposable
{
    private static readonly GpuVertexLayout QuadLayout = new(2 * sizeof(float), new GpuVertexAttribute(0, 2, 0));

    private readonly GpuDevice _gpu;
    private readonly EffectPrimitiveRenderer _primitives;
    private readonly EffectTextureCache _textures;
    private readonly TemperaGlyphCache _glyphs;
    private readonly Stack<TemperaNode> _warmup = new();
    private TemperaGroup? _warmupRoot;
    private float _warmupScale;
    private readonly GpuShader _mesh;
    private readonly GpuShader _invert;
    private readonly Dictionary<TemperaPassKind, GpuShader> _passes = [];
    private readonly GpuShader _copy;
    private readonly GpuBuffer _quad;
    private readonly GpuRenderTarget _text;
    private readonly GpuRenderTarget _ping;
    private readonly GpuRenderTarget _pong;
    private readonly Vector3[] _stops = new Vector3[4];

    public TemperaRenderer(GpuDevice gpu, EffectPrimitiveRenderer primitives, EffectTextureCache textures)
    {
        _gpu = gpu;
        _primitives = primitives;
        _textures = textures;
        _glyphs = new TemperaGlyphCache(textures);
        _mesh = gpu.CreateShader(TemperaShaders.MeshVertex, TemperaShaders.MeshFragment, "tempera-mesh");
        _invert = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.InvertFragment, "tempera-difference");
        _copy = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.CopyFragment, "tempera-copy");
        _passes[TemperaPassKind.Lens] = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.LensFragment, "tempera-lens");
        _passes[TemperaPassKind.Noise] = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.NoiseFragment, "tempera-noise");
        _passes[TemperaPassKind.Contrast] = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.ContrastFragment, "tempera-contrast");
        _passes[TemperaPassKind.RgbShift] = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.RgbShiftFragment, "tempera-rgb-shift");
        _passes[TemperaPassKind.Vignette] = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.VignetteFragment, "tempera-vignette");
        var blur = gpu.CreateShader(TemperaShaders.PassVertex, TemperaShaders.BlurFragment, "tempera-blur");
        _passes[TemperaPassKind.BlurX] = blur;
        _passes[TemperaPassKind.BlurY] = blur;
        _text = gpu.CreateRenderTarget(depthStencil: false);
        _ping = gpu.CreateRenderTarget(depthStencil: false);
        _pong = gpu.CreateRenderTarget(depthStencil: false);
        _quad = gpu.CreateBuffer<float>([0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1]);
    }

    public int DrawCalls { get; private set; }

    public void BeginFrame() => DrawCalls = 0;

    // ---- Tree traversal -------------------------------------------------------------------------------------------------

    /// <summary>Draws <paramref name="root"/> into <paramref name="surface"/>; <paramref name="parent"/> maps logical pixels to the surface.</summary>
    public void Draw(TemperaNode root, TemperaSurface surface, Matrix3x2 parent, double parentAlpha = 1)
    {
        _primitives.SetViewport(new Vector2(surface.Width, surface.Height));
        DrawNode(root, surface, parent, parentAlpha);
        _primitives.Flush();
    }

    private void DrawNode(TemperaNode node, TemperaSurface surface, Matrix3x2 parent, double parentAlpha)
    {
        if (!node.Visible) return;
        var world = node.LocalMatrix * parent;
        var alpha = parentAlpha * node.Alpha;
        switch (node)
        {
            case TemperaInvertLayer layer:
                DrawInvertLayer(layer, surface, world, alpha);
                return;
            case TemperaGroup group:
                foreach (var child in group.Children) DrawNode(child, surface, world, alpha);
                return;
            case TemperaGraphic graphic:
                if (alpha > 0.0005) DrawGraphic(graphic, world, (float)alpha, surface.Width, surface.Height);
                return;
            case TemperaText text:
                if (alpha > 0.0005) DrawText(text, world, (float)alpha, surface.Scale);
                return;
        }
    }

    private void DrawGraphic(TemperaGraphic graphic, Matrix3x2 world, float alpha, int width, int height)
    {
        if (graphic.Parts.Count == 0) return;
        _primitives.Flush();
        if (graphic.Buffer is null)
        {
            graphic.Buffer = new TemperaMeshBuffer(_gpu);
            graphic.Buffer.Upload(graphic);
        }
        _mesh.SetMatrix("uMatrix", world);
        _mesh.Set("uViewport", (float)width, height);
        _gpu.SetBlend(GpuBlend.Alpha);
        foreach (var part in graphic.Parts)
        {
            if (part.Indices.Length == 0) continue;
            var a = part.Alpha * alpha;
            if (part.Stops is { } stops)
            {
                _mesh.Set("uGradient", 1);
                for (var index = 0; index < 4; index++) _stops[index] = stops[index];
                _mesh.Set("uStops", _stops);
                _mesh.Set("uAlpha", a);
            }
            else
            {
                _mesh.Set("uGradient", 0);
                var rgb = part.Color.ToVector3() * a;
                _mesh.Set("uColor", rgb.X, rgb.Y, rgb.Z, a);
            }
            _gpu.DrawIndexed(_mesh, TemperaMeshBuffer.Layout, graphic.Buffer.Vertices!, graphic.Buffer.Indices!,
                part.IndexOffset, part.Indices.Length);
            DrawCalls++;
        }
    }

    private void DrawText(TemperaText text, Matrix3x2 world, float alpha, float rasterScale)
    {
        if (text.Glyph is not { } glyph || glyph.Texture.IsDisposed || text.GlyphScale != rasterScale)
        {
            glyph = _glyphs.Get(text.Text, text.FontFamily, text.FontWeight, text.FontSize, rasterScale, text.WrapWidth);
            text.Glyph = glyph;
            text.GlyphScale = rasterScale;
        }
        _textures.Touch(glyph.Texture);
        var transform = Matrix3x2.CreateTranslation(-glyph.Center) * world;
        _primitives.DrawTexture(glyph.Texture, transform, glyph.Texture.LogicalSize, alpha, EffectBlendMode.Alpha, text.Color.ToEffect());
    }

    public void ResetWarmup()
    {
        _warmup.Clear();
        _warmupRoot = null;
    }

    /// <returns>Whether this target still has work pending after the current frame's allowance.</returns>
    public bool PrewarmText(TemperaGroup root, float rasterScale)
    {
        var resources = _textures.TextResources;
        if (!EffectTextWarmup.CanStart(resources)) return true;
        if (!ReferenceEquals(root, _warmupRoot) || rasterScale != _warmupScale)
        {
            _warmup.Clear();
            _warmup.Push(root);
            _warmupRoot = root;
            _warmupScale = rasterScale;
        }
        using var timing = resources.MeasureTextStage(EffectTextStage.Warmup);
        var started = Stopwatch.GetTimestamp();
        while (_warmup.TryPop(out var node))
        {
            if (!EffectTextWarmup.CanStart(resources) || Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 1)
            {
                _warmup.Push(node);
                return true;
            }
            // The target is a near-future shot: its nodes deliberately are not visible yet.
            if (node is TemperaGroup group)
                for (var index = group.Children.Count - 1; index >= 0; index--) _warmup.Push(group.Children[index]);
            else if (node is TemperaText text)
            {
                if (text.Glyph is { } existing && !existing.Texture.IsDisposed && text.GlyphScale == rasterScale)
                {
                    _textures.Touch(existing.Texture);
                    continue;
                }
                var bytes = _glyphs.EstimateBytes(text.Text, text.FontFamily, text.FontWeight, text.FontSize, text.WrapWidth, rasterScale);
                if (!EffectTextWarmup.Fits(bytes, _textures)) continue;
                if (!EffectTextWarmup.CanStart(resources) || Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 1)
                {
                    _warmup.Push(node);
                    return true;
                }
                text.Glyph = _glyphs.Get(text.Text, text.FontFamily, text.FontWeight, text.FontSize, rasterScale, text.WrapWidth);
                text.GlyphScale = rasterScale;
                if (resources.FrameCreatedTextures > 0) return _warmup.Count > 0;
            }
        }
        return false;
    }

    // ---- The difference filter ------------------------------------------------------------------------------------------

    private void CollectTexts(TemperaGroup group, Matrix3x2 parent, List<(TemperaText Text, Matrix3x2 World)> output)
    {
        foreach (var child in group.Children)
        {
            if (!child.Visible) continue;
            var world = child.LocalMatrix * parent;
            if (child is TemperaGroup inner) CollectTexts(inner, world, output);
            else if (child is TemperaText text) output.Add((text, world));
        }
    }

    private readonly List<(TemperaText Text, Matrix3x2 World)> _layerTexts = [];

    private void DrawInvertLayer(TemperaInvertLayer layer, TemperaSurface surface, Matrix3x2 world, double alpha)
    {
        // Bounds of the visible children, like getFastGlobalBounds: alpha does not shrink them.
        _layerTexts.Clear();
        CollectTexts(layer, world, _layerTexts);
        if (_layerTexts.Count == 0) return;
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var (text, textWorld) in _layerTexts)
        {
            var box = text.BoxSize ??= _glyphs.Box(text.Text, text.FontFamily, text.FontWeight, text.FontSize, text.WrapWidth);
            Span<Vector2> corners = [new(-box.X / 2, -box.Y / 2), new(box.X / 2, -box.Y / 2), new(box.X / 2, box.Y / 2), new(-box.X / 2, box.Y / 2)];
            foreach (var corner in corners)
            {
                var point = Vector2.Transform(corner, textWorld);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }
        var bounds = TemperaPixelRect.FromBounds(minX, minY, maxX, maxY, surface.Width, surface.Height);
        if (bounds.IsEmpty) return;

        _primitives.Flush();
        var back = layer.Inversion ? surface.Resolve() : null;

        _text.EnsureSize(surface.Width, surface.Height);
        _gpu.SetTarget(_text);
        var glBounds = bounds.ToGl(surface.Height);
        _gpu.SetScissor(bounds.ToGpu(surface.Height));
        _gpu.Clear(EffectColor.Transparent);
        foreach (var child in layer.Children) DrawNode(child, surface, world, alpha);
        _primitives.Flush();
        _gpu.SetScissor(null);

        surface.Bind();
        var invert = _invert;
        invert.SetTexture("uText", _text.Texture);
        invert.SetTexture("uBack", back);
        invert.Set("uSize", (float)surface.Width, surface.Height);
        invert.Set("uOutSize", (float)surface.Width, surface.Height);
        invert.Set("uOutFrame", glBounds);
        invert.Set("uPixelScale", surface.Scale);
        var ink = layer.Ink.ToVector3();
        var paper = layer.Paper.ToVector3();
        invert.Set("uInkColor", ink);
        invert.Set("uPaperColor", paper);
        invert.Set("uInkLuminance", Luminance(ink));
        invert.Set("uPaperLuminance", Luminance(paper));
        invert.Set("uBias", 0f);
        var tint = layer.Tint is { Length: >= 2 } stops ? stops : null;
        for (var index = 0; index < 4; index++)
            _stops[index] = tint is null ? ink : tint[Math.Min(index, tint.Length - 1)].ToVector3();
        invert.Set("uTint", _stops);
        invert.Set("uTintAmount", tint is null ? 0f : 1f);
        invert.Set("uInversion", layer.Inversion ? 1 : 0);
        _gpu.SetBlend(GpuBlend.Alpha);
        DrawQuad(invert);
        _primitives.SetViewport(new Vector2(surface.Width, surface.Height));
    }

    private static float Luminance(Vector3 rgb) => rgb.X * 0.2126f + rgb.Y * 0.7152f + rgb.Z * 0.0722f;

    private void DrawQuad(GpuShader shader)
    {
        _gpu.Draw(shader, QuadLayout, _quad, 0, 6);
        DrawCalls++;
    }

    // ---- Scene post chain -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs <paramref name="passes"/> over <paramref name="frame"/> of <paramref name="source"/> (its resolved texture) and blends
    /// the last one onto <paramref name="target"/>. Intermediate passes overwrite, like Pixi's flip/flop filter textures.
    /// </summary>
    public void RunChain(TemperaSurface source, TemperaPixelRect frame, IReadOnlyList<TemperaPass> passes, TemperaSurface target)
    {
        _primitives.Flush();
        using var timing = _gpu.Resources.MeasurePostProcess();
        var input = source.Resolve();
        var inputWidth = source.Width;
        var inputHeight = source.Height;
        var inputFrame = frame.ToGl(source.Height);
        var outputRect = frame.Scaled(target.Width / (double)source.Width, target.Width, target.Height);
        if (passes.Count == 0) passes = [new TemperaPass(TemperaPassKind.Lens, 0)];
        var flip = _ping;
        for (var index = 0; index < passes.Count; index++)
        {
            var last = index == passes.Count - 1;
            Vector4 outFrame;
            int outWidth, outHeight;
            if (last)
            {
                target.Bind();
                outFrame = outputRect.ToGl(target.Height);
                outWidth = target.Width;
                outHeight = target.Height;
                _gpu.SetBlend(GpuBlend.Alpha);
            }
            else
            {
                flip.EnsureSize(inputWidth, inputHeight);
                _gpu.SetTarget(flip);
                outFrame = inputFrame;
                outWidth = inputWidth;
                outHeight = inputHeight;
                _gpu.SetBlend(GpuBlend.None);
                _gpu.SetScissor(frame.ToGpu(source.Height));
                _gpu.Clear(EffectColor.Transparent);
                _gpu.SetScissor(null);
            }
            var pass = passes[index];
            var shader = pass.Amount == 0 && pass.Kind == TemperaPassKind.Lens ? _copy : _passes[pass.Kind];
            shader.SetTexture("uTexture", input);
            shader.Set("uInputSize", (float)inputWidth, inputHeight);
            shader.Set("uFrame", inputFrame);
            shader.Set("uOutFrame", outFrame);
            shader.Set("uOutSize", (float)outWidth, outHeight);
            shader.Set("uPixelScale", source.Scale);
            switch (pass.Kind)
            {
                case TemperaPassKind.Lens:
                    shader.Set("uDistortion", pass.Amount);
                    shader.Set("uDispersion", 0f);
                    break;
                case TemperaPassKind.Noise:
                    shader.Set("uNoise", pass.Amount);
                    shader.Set("uSeed", pass.Seed);
                    break;
                case TemperaPassKind.Contrast:
                    shader.Set("uContrast", pass.Amount);
                    break;
                case TemperaPassKind.RgbShift:
                case TemperaPassKind.Vignette:
                    shader.Set("uAmount", pass.Amount);
                    break;
                case TemperaPassKind.BlurX:
                case TemperaPassKind.BlurY:
                    shader.Set("uDirection", pass.Kind == TemperaPassKind.BlurX ? 1f : 0f,
                        pass.Kind == TemperaPassKind.BlurY ? 1f : 0f);
                    shader.Set("uStrength", pass.Amount);
                    break;
            }
            DrawQuad(shader);
            if (!last)
            {
                input = flip.Texture!;
                flip = flip == _ping ? _pong : _ping;
            }
        }
        _gpu.SetBlend(GpuBlend.Alpha);
        _primitives.SetViewport(new Vector2(target.Width, target.Height));
    }

    /// <summary>Blends a resolved surface onto whatever target is bound (the control's output).</summary>
    public void Present(TemperaSurface surface, int width, int height)
    {
        _copy.SetTexture("uTexture", surface.ResolvedTexture);
        _copy.Set("uInputSize", (float)surface.Width, surface.Height);
        _copy.Set("uFrame", new Vector4(0, 0, surface.Width, surface.Height));
        _copy.Set("uOutFrame", new Vector4(0, 0, width, height));
        _copy.Set("uOutSize", (float)width, height);
        _gpu.SetBlend(GpuBlend.Alpha);
        DrawQuad(_copy);
    }

    public void Dispose()
    {
        ResetWarmup();
        _glyphs.Clear();
        _mesh.Dispose();
        _invert.Dispose();
        _copy.Dispose();
        foreach (var shader in _passes.Values.Distinct()) shader.Dispose();
        _quad.Dispose();
        _text.Dispose();
        _ping.Dispose();
        _pong.Dispose();
    }
}
