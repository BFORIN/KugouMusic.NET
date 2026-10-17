using System.Diagnostics;

namespace AvaloniaSilkEffects;

/// <summary>
/// Opportunistic GL-thread work only: one new texture per frame, at most 2 MiB, with storage headroom.
/// Stop starting work after 8 ms of frame submission. A synchronous raster/upload cannot be preempted.
/// </summary>
internal static class EffectTextWarmup
{
    internal const long MaximumTextureBytes = 2 * 1024 * 1024;
    internal const double LookAheadSeconds = 1.2;
    public static bool CanStart(EffectGpuResources resources) => resources.FrameCreatedTextures == 0 &&
        Stopwatch.GetElapsedTime(resources.FrameStartedAt).TotalMilliseconds < 8;

    public static bool Fits(long bytes, EffectTextureCache cache) => bytes > 0 && bytes <= MaximumTextureBytes &&
        cache.TextResources.Snapshot.Textures < cache.ResidentTextureLimit &&
        bytes <= cache.ResidentByteLimit - cache.TextResources.Snapshot.TextureBytes;
}
