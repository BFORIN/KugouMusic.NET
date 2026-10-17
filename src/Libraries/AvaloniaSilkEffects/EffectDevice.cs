using Avalonia;
using System.Diagnostics;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects;

public sealed class EffectDevice : IDisposable
{
    private readonly PostProcessPipeline _postProcessPipeline;
    private readonly EffectRenderContext _renderContext;
    private bool _disposed;

    internal EffectDevice(GL gl)
    {
        Gl = gl;
        OpenGlVersion = gl.GetStringS(StringName.Version);
        Renderer = gl.GetStringS(StringName.Renderer);
        gl.GetInteger(GetPName.MajorVersion, out var major);
        gl.GetInteger(GetPName.MinorVersion, out var minor);
        var isGles = OpenGlVersion.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
        if ((!isGles && (major < 3 || major == 3 && minor < 3)) ||
            (isGles && (major < 3)))
            throw new NotSupportedException($"AvaloniaSilkEffects requires OpenGL 3.3+ or OpenGL ES 3.0+; current context is {OpenGlVersion}.");
        Primitives = new EffectPrimitiveRenderer(gl);
        Textures = new EffectTextureCache(gl);
        PostProcess = new PostProcessSettings();
        _postProcessPipeline = new PostProcessPipeline(gl);
        _renderContext = new EffectRenderContext(this, Primitives);
    }

    internal GL Gl { get; }
    public string OpenGlVersion { get; }
    public string Renderer { get; }
    public EffectPrimitiveRenderer Primitives { get; }
    public EffectTextureCache Textures { get; }
    public PostProcessSettings PostProcess { get; }
    public EffectDeviceFrameMetrics FrameMetrics { get; private set; }

    internal void Render(
        IEffectScene scene,
        in EffectFrame frame,
        int targetFramebuffer,
        EffectColor clearColor,
        double prepareMilliseconds = 0, long prepareAllocatedBytes = 0)
    {
        ThrowIfDisposed();
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var resources = EffectGpuResources.For(Gl);
        resources.BeginFrame();
        Textures.BeginFrame();
        scene.Update(frame);
        var updated = Stopwatch.GetTimestamp();
        var postProcessingEnabled = PostProcess.IsEnabled;
        Gl.Disable(EnableCap.DepthTest);
        Gl.Disable(EnableCap.CullFace);
        Gl.Disable(EnableCap.ScissorTest);
        Gl.Disable(EnableCap.StencilTest);
        _postProcessPipeline.Begin(
            frame.PixelSize.Width, frame.PixelSize.Height, targetFramebuffer,
            clearColor, postProcessingEnabled, PostProcess.ResolutionScale, PostProcess.MultisampleCount);
        Primitives.Begin(frame.PixelSize.Width, frame.PixelSize.Height);
        _renderContext.PixelSize = frame.PixelSize;
        scene.Render(_renderContext);
        Primitives.Flush();
        var drawn = Stopwatch.GetTimestamp();
        _postProcessPipeline.End(targetFramebuffer, PostProcess);
        var postProcessed = Stopwatch.GetTimestamp();
        Textures.Collect();
        var collected = Stopwatch.GetTimestamp();
        var timings = new EffectFrameTimings(prepareMilliseconds,
            Stopwatch.GetElapsedTime(started, updated).TotalMilliseconds,
            Stopwatch.GetElapsedTime(updated, drawn).TotalMilliseconds - resources.FrameCustomPostMilliseconds,
            Stopwatch.GetElapsedTime(drawn, postProcessed).TotalMilliseconds + resources.FrameCustomPostMilliseconds,
            Stopwatch.GetElapsedTime(postProcessed, collected).TotalMilliseconds,
            resources.FrameTextPreparationMilliseconds,
            prepareMilliseconds + Stopwatch.GetElapsedTime(started, collected).TotalMilliseconds,
            prepareAllocatedBytes + GC.GetAllocatedBytesForCurrentThread() - allocated)
        { TextWork = resources.TextWork };
        FrameMetrics = new EffectDeviceFrameMetrics(
            Primitives.FrameDrawCalls + _postProcessPipeline.FrameDrawCalls,
            Primitives.FrameFlushes,
            resources.FrameUploadedBytes,
            postProcessingEnabled,
            Textures.Count,
            Textures.ResidentBytes)
        {
            MultisampleCount = _postProcessPipeline.MultisampleCount,
            Timings = timings,
            GpuResources = resources.Snapshot,
            CreatedTextures = resources.FrameCreatedTextures,
            ReleasedTextures = resources.FrameReleasedTextures,
        };
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Textures.Dispose();
        Primitives.Dispose();
        _postProcessPipeline.Dispose();
    }

    internal void Abandon()
    {
        if (_disposed)
            return;
        _disposed = true;
        Textures.Abandon();
        EffectGpuResources.For(Gl).Abandon();
    }
}

public readonly record struct EffectDeviceFrameMetrics(
    int DrawCalls,
    int Flushes,
    long UploadedBytes,
    bool PostProcessingEnabled,
    int ResidentTextures,
    long ResidentTextureBytes)
{
    public int MultisampleCount { get; init; } = 1;
    public EffectFrameTimings Timings { get; init; }
    public EffectGpuResourceStatistics GpuResources { get; init; }
    public int CreatedTextures { get; init; }
    public int ReleasedTextures { get; init; }
}
