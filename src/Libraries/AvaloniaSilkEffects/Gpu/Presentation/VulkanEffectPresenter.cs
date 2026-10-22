using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using AvaloniaSilkEffects.Gpu.Vulkan;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace AvaloniaSilkEffects.Gpu.Presentation;

/// <summary>
/// Vulkan frames shown through exported RGBA8 images (opaque NT handles on Windows, fds on Linux), synchronized with
/// a semaphore pair per image like Avalonia's GPU interop sample: the frame signals "rendered", the compositor waits on
/// it and signals "available", which the next frame drawing into the image waits on. Images rest in
/// TRANSFER_SRC_OPTIMAL, the layout the compositor imports them in.
/// </summary>
internal sealed unsafe class VulkanEffectPresenter : CompositionEffectPresenter
{
    private readonly VulkanGpuDevice _gpu;
    private readonly bool _windows = OperatingSystem.IsWindows();
    private readonly KhrExternalMemoryWin32? _memoryWin32;
    private readonly KhrExternalMemoryFd? _memoryFd;
    private readonly KhrExternalSemaphoreWin32? _semaphoreWin32;
    private readonly KhrExternalSemaphoreFd? _semaphoreFd;

    public VulkanEffectPresenter(IEffectPresenterHost host, Visual visual, Compositor compositor, ICompositionGpuInterop interop)
        : base(host, visual, compositor, interop)
    {
        string[] deviceExtensions = _windows
            ? ["VK_KHR_external_memory", "VK_KHR_external_semaphore", KhrExternalMemoryWin32.ExtensionName,
                KhrExternalSemaphoreWin32.ExtensionName, "VK_KHR_dedicated_allocation", "VK_KHR_get_memory_requirements2"]
            : ["VK_KHR_external_memory", "VK_KHR_external_semaphore", KhrExternalMemoryFd.ExtensionName,
                KhrExternalSemaphoreFd.ExtensionName, "VK_KHR_dedicated_allocation", "VK_KHR_get_memory_requirements2"];
        _gpu = VulkanGpuDevice.Create(new VulkanDeviceOptions
        {
            DeviceUuid = interop.DeviceUuid,
            DeviceLuid = interop.DeviceLuid,
            InstanceExtensions = ["VK_KHR_get_physical_device_properties2", "VK_KHR_external_memory_capabilities",
                "VK_KHR_external_semaphore_capabilities"],
            DeviceExtensions = deviceExtensions,
        });
        var vk = _gpu.Vk;
        var missing = _windows
            ? !vk.TryGetDeviceExtension(_gpu.Instance, _gpu.Handle, out _memoryWin32) ||
              !vk.TryGetDeviceExtension(_gpu.Instance, _gpu.Handle, out _semaphoreWin32)
            : !vk.TryGetDeviceExtension(_gpu.Instance, _gpu.Handle, out _memoryFd) ||
              !vk.TryGetDeviceExtension(_gpu.Instance, _gpu.Handle, out _semaphoreFd);
        if (missing)
        {
            _gpu.Dispose();
            throw new NotSupportedException("The Vulkan device cannot export memory and semaphores.");
        }
    }

    public override EffectGpuBackend Backend => EffectGpuBackend.Vulkan;
    protected override GpuDevice Gpu => _gpu;

    private string ImageHandleType => _windows
        ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;

    private string SemaphoreHandleType => _windows
        ? KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;

    private ExternalMemoryHandleTypeFlags MemoryType => _windows
        ? ExternalMemoryHandleTypeFlags.OpaqueWin32Bit : ExternalMemoryHandleTypeFlags.OpaqueFDBit;

    public static bool CanImport(ICompositionGpuInterop interop, out string? reason)
    {
        reason = null;
        var image = OperatingSystem.IsWindows()
            ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
            : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;
        var semaphore = OperatingSystem.IsWindows()
            ? KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaqueNtHandle
            : KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;
        if (!interop.SupportedImageHandleTypes.Contains(image))
            reason = $"the compositor cannot import {image} images (it is not running on Vulkan)";
        else if (!interop.SupportedSemaphoreTypes.Contains(semaphore))
            reason = $"the compositor cannot import {semaphore} semaphores";
        else if (!interop.GetSynchronizationCapabilities(image)
                     .HasFlag(CompositionGpuImportedImageSynchronizationCapabilities.Semaphores))
            reason = "the compositor has no semaphore synchronization for Vulkan images";
        return reason is null;
    }

    protected override SwapchainImage CreateImage(PixelSize size) => new Image(this, size);

    protected override void WaitIdle()
    {
        _gpu.Submit(wait: true);
        _gpu.Vk.DeviceWaitIdle(_gpu.Handle);
    }

    protected override void DisposeDevice() => _gpu.Dispose();

    private sealed class Image : SwapchainImage
    {
        private readonly VulkanEffectPresenter _owner;
        private readonly Silk.NET.Vulkan.Image _image;
        private readonly DeviceMemory _memory;
        private readonly ulong _memorySize;
        private readonly VulkanRenderTarget _target;
        private readonly VkSemaphore _rendered;
        private readonly VkSemaphore _available;
        private ICompositionImportedGpuImage? _imported;
        private ICompositionImportedGpuSemaphore? _renderedImport;
        private ICompositionImportedGpuSemaphore? _availableImport;
        private bool _presented;

        public Image(VulkanEffectPresenter owner, PixelSize size) : base(size)
        {
            _owner = owner;
            var gpu = owner._gpu;
            var external = new ExternalMemoryImageCreateInfo
            {
                SType = StructureType.ExternalMemoryImageCreateInfo, HandleTypes = owner.MemoryType,
            };
            var info = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                PNext = &external,
                ImageType = ImageType.Type2D,
                Format = VulkanGpuDevice.ColorFormat,
                Extent = new Extent3D((uint)size.Width, (uint)size.Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal,
                // Matches the image Avalonia creates over the imported memory.
                Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit |
                    ImageUsageFlags.TransferSrcBit | ImageUsageFlags.SampledBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined,
                Flags = ImageCreateFlags.CreateMutableFormatBit,
            };
            VulkanGpuDevice.Check(gpu.Vk.CreateImage(gpu.Handle, in info, null, out _image), "exportable image");
            gpu.Vk.GetImageMemoryRequirements(gpu.Handle, _image, out var requirements);
            var dedicated = new MemoryDedicatedAllocateInfo { SType = StructureType.MemoryDedicatedAllocateInfo, Image = _image };
            var export = new ExportMemoryAllocateInfo
            {
                SType = StructureType.ExportMemoryAllocateInfo, HandleTypes = owner.MemoryType, PNext = &dedicated,
            };
            _memory = gpu.Allocate(requirements, MemoryPropertyFlags.DeviceLocalBit, &export);
            _memorySize = requirements.Size;
            VulkanGpuDevice.Check(gpu.Vk.BindImageMemory(gpu.Handle, _image, _memory, 0), "vkBindImageMemory");
            _target = gpu.WrapImage(_image, VulkanGpuDevice.ColorFormat, size.Width, size.Height, ImageLayout.TransferSrcOptimal);
            _rendered = Semaphore();
            _available = Semaphore();
        }

        public override GpuRenderTarget Target => _target;

        public override void BeginDraw()
        {
            if (_presented) _owner._gpu.WaitBeforeNextSubmit(_available);
            _owner._gpu.SignalAfterNextSubmit(_rendered);
        }

        public override void Present(bool drawn)
        {
            var interop = _owner.Interop;
            _imported ??= interop.ImportImage(new PlatformHandle(ExportMemory(), _owner.ImageHandleType),
                new PlatformGraphicsExternalImageProperties
                {
                    Width = Size.Width,
                    Height = Size.Height,
                    Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
                    MemorySize = _memorySize,
                    // Row 0 is the bottom, like an OpenGL framebuffer.
                    TopLeftOrigin = false,
                });
            _renderedImport ??= interop.ImportSemaphore(new PlatformHandle(ExportSemaphore(_rendered), _owner.SemaphoreHandleType));
            _availableImport ??= interop.ImportSemaphore(new PlatformHandle(ExportSemaphore(_available), _owner.SemaphoreHandleType));
            LastPresent = _owner.Surface.UpdateWithSemaphoresAsync(_imported, _renderedImport, _availableImport);
            _presented = true;
        }

        private VkSemaphore Semaphore()
        {
            var gpu = _owner._gpu;
            var export = new ExportSemaphoreCreateInfo
            {
                SType = StructureType.ExportSemaphoreCreateInfo,
                HandleTypes = _owner._windows
                    ? ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit : ExternalSemaphoreHandleTypeFlags.OpaqueFDBit,
            };
            var info = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo, PNext = &export };
            VulkanGpuDevice.Check(gpu.Vk.CreateSemaphore(gpu.Handle, in info, null, out var semaphore), "exportable semaphore");
            return semaphore;
        }

        private nint ExportMemory()
        {
            var gpu = _owner._gpu;
            if (_owner._windows)
            {
                var info = new MemoryGetWin32HandleInfoKHR
                {
                    SType = StructureType.MemoryGetWin32HandleInfoKhr, Memory = _memory,
                    HandleType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit,
                };
                VulkanGpuDevice.Check(_owner._memoryWin32!.GetMemoryWin32Handle(gpu.Handle, in info, out var handle), "memory export");
                return handle;
            }
            var fdInfo = new MemoryGetFdInfoKHR
            {
                SType = StructureType.MemoryGetFDInfoKhr, Memory = _memory, HandleType = ExternalMemoryHandleTypeFlags.OpaqueFDBit,
            };
            VulkanGpuDevice.Check(_owner._memoryFd!.GetMemoryF(gpu.Handle, in fdInfo, out var fd), "memory export");
            return fd;
        }

        private nint ExportSemaphore(VkSemaphore semaphore)
        {
            var gpu = _owner._gpu;
            if (_owner._windows)
            {
                var info = new SemaphoreGetWin32HandleInfoKHR
                {
                    SType = StructureType.SemaphoreGetWin32HandleInfoKhr, Semaphore = semaphore,
                    HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit,
                };
                VulkanGpuDevice.Check(_owner._semaphoreWin32!.GetSemaphoreWin32Handle(gpu.Handle, in info, out var handle), "semaphore export");
                return handle;
            }
            var fdInfo = new SemaphoreGetFdInfoKHR
            {
                SType = StructureType.SemaphoreGetFDInfoKhr, Semaphore = semaphore,
                HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueFDBit,
            };
            VulkanGpuDevice.Check(_owner._semaphoreFd!.GetSemaphoreF(gpu.Handle, in fdInfo, out var fd), "semaphore export");
            return fd;
        }

        public override void Dispose()
        {
            _imported?.DisposeAsync();
            _renderedImport?.DisposeAsync();
            _availableImport?.DisposeAsync();
            _target.Dispose();
            var gpu = _owner._gpu;
            var (image, memory, rendered, available) = (_image, _memory, _rendered, _available);
            gpu.Defer(() =>
            {
                gpu.Vk.DestroySemaphore(gpu.Handle, rendered, null);
                gpu.Vk.DestroySemaphore(gpu.Handle, available, null);
                gpu.Vk.DestroyImage(gpu.Handle, image, null);
                gpu.Vk.FreeMemory(gpu.Handle, memory, null);
            });
        }
    }
}
