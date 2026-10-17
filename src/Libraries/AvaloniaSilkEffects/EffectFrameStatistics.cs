using Avalonia;

namespace AvaloniaSilkEffects;

public readonly record struct EffectFrameStatistics(
    double FramesPerSecond,
    double CpuMilliseconds,
    ulong SubmittedFrames,
    ulong SkippedFrames,
    int DrawCalls,
    int Flushes,
    long UploadedBytes,
    PixelSize FramebufferSize,
    bool PostProcessingEnabled,
    string OpenGlVersion,
    string Renderer,
    int ResidentTextures = 0,
    long ResidentTextureBytes = 0)
{
    public int MultisampleCount { get; init; } = 1;
    public EffectFrameTimings Timings { get; init; }
    public EffectGpuResourceStatistics GpuResources { get; init; }
    public int CreatedTextures { get; init; }
    public int ReleasedTextures { get; init; }
}
