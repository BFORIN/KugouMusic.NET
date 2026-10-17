namespace AvaloniaSilkEffects;

/// <summary>Formatting runs only when a diagnostics consumer requests a report.</summary>
public static class EffectPerformanceReport
{
    public static string Format(string name, EffectFrameStatistics frame, EffectPerformanceSnapshot window)
    {
        var gpu = frame.GpuResources;
        return $"{name} · {frame.FramebufferSize.Width}×{frame.FramebufferSize.Height} · {frame.FramesPerSecond:F1} FPS · last {window.SampleCount} frames\n" +
            $"CPU mean/P95/P99/max {Values(window.Cpu)} ms · interval P95/P99 {window.FrameInterval.P95:F2}/{window.FrameInterval.P99:F2} ms\n" +
            $"stage P95 prepare/update/draw/post/collect {window.Prepare.P95:F2}/{window.Update.P95:F2}/{window.Draw.P95:F2}/{window.PostProcess.P95:F2}/{window.Collect.P95:F2} ms\n" +
            $"text preparation P95 {window.TextPreparation.P95:F2} ms (nested) · alloc mean/P99 {window.AllocatedBytes.Mean / 1024:F1}/{window.AllocatedBytes.P99 / 1024:F1} KiB\n" +
            $"text P95 measure/raster/upload {window.TextMeasure.P95:F2}/{window.TextRaster.P95:F2}/{window.TextUpload.P95:F2} ms · warmup P95 {window.TextWarmup.P95:F2} ms\n" +
            $"draw/flush {frame.DrawCalls}/{frame.Flushes} · global MSAA {frame.MultisampleCount}x · upload {frame.UploadedBytes / 1024d:F1} KiB · texture +/- {frame.CreatedTextures}/{frame.ReleasedTextures}\n" +
            $"GPU estimate {gpu.EstimatedBytes / 1048576d:F1} MiB: textures {gpu.Textures}/{gpu.TextureBytes / 1048576d:F1}, targets {gpu.RenderTargets}/{gpu.RenderTargetBytes / 1048576d:F1}, buffers {gpu.Buffers}/{gpu.BufferBytes / 1048576d:F1}";
    }

    public static string FormatWork(string name, EffectPerformanceSnapshot window) =>
        $"{name} · last {window.SampleCount} calls · CPU mean/P95/P99/max {Values(window.Cpu)} ms · alloc mean/P99 {window.AllocatedBytes.Mean / 1024:F1}/{window.AllocatedBytes.P99 / 1024:F1} KiB";

    private static string Values(EffectMetricDistribution value) =>
        $"{value.Mean:F2}/{value.P95:F2}/{value.P99:F2}/{value.Maximum:F2}";
}
