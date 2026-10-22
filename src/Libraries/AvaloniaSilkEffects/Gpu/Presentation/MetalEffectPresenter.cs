using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using AvaloniaSilkEffects.Gpu.Metal;
using static AvaloniaSilkEffects.Gpu.Metal.ObjC;

namespace AvaloniaSilkEffects.Gpu.Presentation;

/// <summary>
/// Metal frames shown through IOSurface-backed BGRA8 images. Each image has a MTLSharedEvent timeline: a frame's
/// command buffer signals <c>n + 1</c>, the compositor waits for it, copies the image and signals <c>n + 2</c>,
/// which the next frame drawing into the same image waits for on the GPU.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MetalEffectPresenter : CompositionEffectPresenter
{
    private const int PixelFormatBgra = 0x42475241; // 'BGRA'
    private readonly MetalGpuDevice _gpu;

    public MetalEffectPresenter(IEffectPresenterHost host, Visual visual, Compositor compositor, ICompositionGpuInterop interop)
        : base(host, visual, compositor, interop) =>
        _gpu = new MetalGpuDevice(MatchingDevice(interop.DeviceLuid));

    public override EffectGpuBackend Backend => EffectGpuBackend.Metal;
    protected override GpuDevice Gpu => _gpu;

    public static bool CanImport(ICompositionGpuInterop interop, out string? reason)
    {
        reason = null;
        if (!interop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef))
            reason = "the compositor cannot import IOSurfaces";
        else if (!interop.SupportedSemaphoreTypes.Contains(KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent))
            reason = "the compositor cannot wait on Metal shared events";
        else if (!interop.GetSynchronizationCapabilities(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef)
                     .HasFlag(CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores))
            reason = "the compositor has no timeline synchronization for IOSurfaces";
        return reason is null;
    }

    /// <summary>The Metal device whose registryID is the compositor's LUID, so images never cross GPUs.</summary>
    private static nint MatchingDevice(byte[]? luid)
    {
        if (luid is not { Length: 8 }) return 0;
        var registry = BitConverter.ToUInt64(luid);
        using var pool = Pool.Push();
        var devices = MTLCopyAllDevices();
        try
        {
            var count = (int)Call(devices, "count");
            for (var index = 0; index < count; index++)
            {
                var device = Call(devices, "objectAtIndex:", index);
                if (CallULong(device, "registryID") == registry) return device;
            }
            return 0;
        }
        finally
        {
            Release(devices);
        }
    }

    protected override SwapchainImage CreateImage(PixelSize size) => new Image(this, size);

    protected override void WaitIdle() => _gpu.Submit(wait: true);

    protected override void DisposeDevice() => _gpu.Dispose();

    private sealed class Image : SwapchainImage
    {
        private readonly MetalEffectPresenter _owner;
        private readonly nint _surface;
        private readonly nint _texture;
        private readonly MetalRenderTarget _target;
        private readonly nint _event;
        private ICompositionImportedGpuImage? _imported;
        private ICompositionImportedGpuSemaphore? _semaphore;
        private ulong _counter;
        private ulong _released;
        private ulong _rendered;

        public Image(MetalEffectPresenter owner, PixelSize size) : base(size)
        {
            _owner = owner;
            using var pool = Pool.Push();
            var properties = Call(Class("NSMutableDictionary"), "dictionary");
            void Put(string key, long value) => Call(properties, "setObject:forKey:", Number(value), String(key));
            Put("IOSurfaceWidth", size.Width);
            Put("IOSurfaceHeight", size.Height);
            Put("IOSurfaceBytesPerElement", 4);
            Put("IOSurfacePixelFormat", PixelFormatBgra);
            _surface = IOSurfaceCreate(properties);
            if (_surface == 0) throw new InvalidOperationException($"IOSurface {size.Width}x{size.Height} could not be created.");
            var descriptor = Call(Class("MTLTextureDescriptor"), "texture2DDescriptorWithPixelFormat:width:height:mipmapped:",
                MetalGpuDevice.PixelFormatBgra8, size.Width, size.Height, 0);
            Call(descriptor, "setUsage:", 1 | 4);
            Call(descriptor, "setStorageMode:", CallBool(owner._gpu.Device, "hasUnifiedMemory") ? 0 : 1);
            _texture = Call(owner._gpu.Device, "newTextureWithDescriptor:iosurface:plane:", descriptor, _surface, 0);
            if (_texture == 0)
            {
                CFRelease(_surface);
                throw new InvalidOperationException("Metal could not wrap the IOSurface.");
            }
            _target = owner._gpu.WrapTexture(_texture, size.Width, size.Height, MetalGpuDevice.PixelFormatBgra8);
            _event = Call(owner._gpu.Device, "newSharedEvent");
        }

        public override GpuRenderTarget Target => _target;

        public override void BeginDraw()
        {
            if (_released != 0) _owner._gpu.WaitBeforeNextWork(_event, _released);
            _rendered = ++_counter;
            _owner._gpu.SignalAfterFrame(_event, _rendered);
        }

        public override void Present(bool drawn)
        {
            _imported ??= _owner.Interop.ImportImage(
                new PlatformHandle(_surface, KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef),
                new PlatformGraphicsExternalImageProperties
                {
                    Width = Size.Width,
                    Height = Size.Height,
                    Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,
                    // Row 0 is the bottom, like an OpenGL framebuffer.
                    TopLeftOrigin = false,
                });
            _semaphore ??= _owner.Interop.ImportSemaphore(new PlatformHandle(_event,
                KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent));
            _released = ++_counter;
            LastPresent = _owner.Surface.UpdateWithTimelineSemaphoresAsync(_imported, _semaphore, _rendered,
                _semaphore, _released);
        }

        public override void Dispose()
        {
            _imported?.DisposeAsync();
            _semaphore?.DisposeAsync();
            Release(_event);
            _target.Dispose();
            Release(_texture);
            CFRelease(_surface);
        }
    }
}
