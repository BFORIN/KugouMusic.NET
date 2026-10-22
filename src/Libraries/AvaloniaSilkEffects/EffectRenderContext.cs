using Avalonia;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects;

public sealed class EffectRenderContext
{
    private readonly GpuDevice _gpu;
    private readonly Stack<Rect> _clips = [];

    internal EffectRenderContext(EffectDevice device, EffectPrimitiveRenderer primitives)
    {
        Device = device;
        Primitives = primitives;
        _gpu = device.Gpu;
    }

    public EffectDevice Device { get; }
    public EffectPrimitiveRenderer Primitives { get; }
    public PixelSize PixelSize { get; internal set; }

    public void Render(EffectNode node) => node.Render(this);

    public IDisposable PushClip(Rect pixelRect)
    {
        Primitives.Flush();
        _clips.Push(pixelRect);
        ApplyClip(pixelRect);
        return new Scope(() =>
        {
            Primitives.Flush();
            _clips.Pop();
            if (_clips.TryPeek(out var previous))
                ApplyClip(previous);
            else
                _gpu.SetScissor(null);
        });
    }

    public IDisposable PushStencilMask(Action<EffectPrimitiveRenderer> drawMask)
    {
        ArgumentNullException.ThrowIfNull(drawMask);
        Primitives.Flush();
        _gpu.Clear(null, stencil: true);
        _gpu.SetColorWrite(false);
        _gpu.SetStencil(GpuStencil.WriteOne);
        drawMask(Primitives);
        Primitives.Flush();
        _gpu.SetColorWrite(true);
        _gpu.SetStencil(GpuStencil.EqualOne);
        return new Scope(() =>
        {
            Primitives.Flush();
            _gpu.SetStencil(GpuStencil.Disabled);
        });
    }

    private void ApplyClip(Rect clip)
    {
        var x = Math.Max(0, (int)Math.Floor(clip.X));
        var y = Math.Max(0, PixelSize.Height - (int)Math.Ceiling(clip.Bottom));
        var width = Math.Max(0, Math.Min(PixelSize.Width - x, (int)Math.Ceiling(clip.Width)));
        var height = Math.Max(0, Math.Min(PixelSize.Height - y, (int)Math.Ceiling(clip.Height)));
        _gpu.SetScissor(new GpuRect(x, y, width, height));
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
