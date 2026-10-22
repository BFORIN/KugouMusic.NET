using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects.Lumiere.Light;

/// <summary>The target and viewport bound when a Lumiere group starts, restored after it.</summary>
internal readonly record struct LumiereBinding(GpuRenderTarget Target, GpuRect Viewport)
{
    public static LumiereBinding Capture(GpuDevice gpu) =>
        new(gpu.Target ?? throw new InvalidOperationException("No render target is bound."), gpu.Viewport);

    public void Restore(GpuDevice gpu)
    {
        gpu.SetTarget(Target);
        gpu.SetViewport(Viewport);
    }
}
