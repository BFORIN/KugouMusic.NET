using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using AvaloniaSilkEffects.Gpu.Shaders;
using static AvaloniaSilkEffects.Gpu.Metal.ObjC;

namespace AvaloniaSilkEffects.Gpu.Metal;

/// <summary>
/// The Metal backend. Draws go into one command buffer per frame; a render pass starts lazily on the bound target
/// and ends when the target changes, a copy runs or the frame ends. Full-target clears become the next pass's load
/// action, scissored clears a quad. Vertex shaders flip clip-space y, so targets keep OpenGL's row order.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed unsafe class MetalGpuDevice : GpuDevice
{
    internal const nint PixelFormatRgba8 = 70;
    internal const nint PixelFormatBgra8 = 80;
    // Scenes use stencil masks, never depth testing/writes. A separate S8 attachment
    // preserves those masks without allocating an unused depth plane (including MSAA).
    internal const nint PixelFormatStencil8 = 253;
    private const int InlineBytesLimit = 4096;

    private const string ClearSource = """
        #include <metal_stdlib>
        using namespace metal;
        struct ClearOut { float4 position [[position]]; };
        vertex ClearOut clear_vertex(uint vid [[vertex_id]]) {
            float2 p = float2((vid << 1) & 2, vid & 2);
            ClearOut o;
            o.position = float4(p * 2.0 - 1.0, 0.0, 1.0);
            return o;
        }
        fragment float4 clear_fragment(constant float4& color [[buffer(0)]]) { return color; }
        """;

    private readonly bool _unifiedMemory;
    private readonly Dictionary<GpuCompiledProgram, (nint Vertex, nint Fragment)> _functions = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<PipelineKey, nint> _pipelines = [];
    private readonly Dictionary<(GpuStencil, bool), nint> _depthStencilStates = [];
    private readonly nint[] _samplers = new nint[3];
    private readonly List<ArenaChunk> _chunks = [];
    private readonly nint _clearVertex;
    private readonly nint _clearFragment;
    private readonly MetalTexture _empty;
    private MetalRenderTarget? _frameOutput;
    private nint _commandBuffer;
    private nint _lastCommitted;
    private nint _encoder;
    private MetalRenderTarget? _encoderTarget;
    private PipelineKey? _encoderPipeline;
    private (GpuStencil, bool)? _encoderDepthStencil;
    private GpuRect? _encoderViewport;
    private GpuRect? _encoderScissor;
    private ArenaChunk? _chunk;
    private nint _pool;
    private readonly List<(nint Event, ulong Value)> _waits = [];
    private readonly List<(nint Event, ulong Value)> _signals = [];

    public MetalGpuDevice(nint device = 0, nint queue = 0)
    {
        Device = device != 0 ? Retain(device) : MTLCreateSystemDefaultDevice();
        if (Device == 0) throw new NotSupportedException("No Metal device is available.");
        Queue = queue != 0 ? Retain(queue) : Call(Device, "newCommandQueue");
        using var pool = Pool.Push();
        Renderer = ToManaged(Call(Device, "name")) ?? "Metal";
        _unifiedMemory = CallBool(Device, "hasUnifiedMemory");
        MaxSamples = (Call(Device, "supportsTextureSampleCount:", 8) & 0xFF) != 0 ? 8 : 4;
        (_clearVertex, _clearFragment) = Library(ClearSource, "clear_vertex", "clear_fragment", "effects-clear");
        _samplers[(int)GpuFilter.Nearest] = Sampler(0, 0);
        _samplers[(int)GpuFilter.Linear] = Sampler(1, 0);
        _samplers[(int)GpuFilter.LinearMipmap] = Sampler(1, 2);
        // Bound where a shader's sampler has no texture, like GL's texture object 0.
        _empty = (MetalTexture)CreateTexture(1, 1, [0, 0, 0, 0], GpuFilter.Nearest);
    }

    public nint Device { get; }
    public nint Queue { get; }
    public override EffectGpuBackend Backend => EffectGpuBackend.Metal;
    public override string ApiVersion => "Metal";
    public override string Renderer { get; }
    public override int MaxSamples { get; }

    /// <summary>The command buffer of the frame being recorded, created on demand.</summary>
    public nint CommandBuffer => EnsureCommandBuffer();

    public override GpuTexture CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, GpuFilter filter)
    {
        using var pool = Pool.Push();
        var mipmapped = filter == GpuFilter.LinearMipmap;
        var descriptor = Call(Class("MTLTextureDescriptor"), "texture2DDescriptorWithPixelFormat:width:height:mipmapped:",
            PixelFormatRgba8, width, height, mipmapped ? 1 : 0);
        Call(descriptor, "setUsage:", 1);
        Call(descriptor, "setStorageMode:", _unifiedMemory ? 0 : 1);
        var texture = Call(Device, "newTextureWithDescriptor:", descriptor);
        if (texture == 0) throw new InvalidOperationException($"Metal could not allocate a {width}x{height} texture.");
        if (!rgba.IsEmpty)
            fixed (byte* pixels = rgba)
                Call(texture, "replaceRegion:mipmapLevel:withBytes:bytesPerRow:",
                    new MTLRegion(default, new MTLSize((nuint)width, (nuint)height, 1)), 0, (nint)pixels, width * 4);
        if (mipmapped && width * height > 1)
        {
            // A separate command buffer committed now runs before the frame's, which samples the texture.
            var upload = Call(Queue, "commandBuffer");
            var blit = Call(upload, "blitCommandEncoder");
            Call(blit, "generateMipmapsForTexture:", texture);
            Call(blit, "endEncoding");
            Call(upload, "commit");
        }
        return new MetalTexture(texture, width, height, filter, owns: true);
    }

    public override GpuBuffer CreateBuffer(ReadOnlySpan<byte> data, bool index)
    {
        nint buffer;
        if (data.IsEmpty) buffer = Call(Device, "newBufferWithLength:options:", 4, 0);
        else
            fixed (byte* bytes = data)
                buffer = Call(Device, "newBufferWithBytes:length:options:", (nint)bytes, data.Length, 0);
        if (buffer == 0) throw new InvalidOperationException("Metal could not allocate a buffer.");
        var result = new MetalBuffer(buffer, data.Length);
        Resources.Buffer(result.Id, data.Length);
        Resources.Uploaded(data.Length);
        return result;
    }

    public override GpuShader CreateShader(string vertexSource, string fragmentSource, string name)
    {
        var program = GpuShaderCompiler.Compile(vertexSource, fragmentSource, name, GpuShaderTarget.Msl);
        if (!_functions.ContainsKey(program))
        {
            using var pool = Pool.Push();
            var vertex = Library(program.Vertex.Msl!, program.Vertex.MslEntryPoint, null, name).Vertex;
            var fragment = Library(program.Fragment.Msl!, program.Fragment.MslEntryPoint, null, name).Vertex;
            _functions[program] = (vertex, fragment);
        }
        return new MetalShader(program);
    }

    public override GpuRenderTarget CreateRenderTarget(bool depthStencil) => new MetalRenderTarget(this, depthStencil);

    /// <summary>Wraps a texture another component owns (an IOSurface image shared with the compositor).</summary>
    public MetalRenderTarget WrapTexture(nint texture, int width, int height, nint pixelFormat) =>
        MetalRenderTarget.External(this, texture, width, height, pixelFormat);

    /// <summary>The next frame's GPU work waits until <paramref name="sharedEvent"/> reaches <paramref name="value"/>.</summary>
    public void WaitBeforeNextWork(nint sharedEvent, ulong value) => _waits.Add((sharedEvent, value));

    /// <summary>Signals <paramref name="sharedEvent"/> with <paramref name="value"/> when the current frame's work completes.</summary>
    public void SignalAfterFrame(nint sharedEvent, ulong value) => _signals.Add((sharedEvent, value));

    public override void BeginFrame(GpuRenderTarget output)
    {
        EnsureCommandBuffer();
        _frameOutput = (MetalRenderTarget)output;
        base.BeginFrame(output);
    }

    public override void EndFrame() => Submit(wait: false);

    /// <summary>Commits recorded work; with <paramref name="wait"/>, blocks until the GPU has finished it.</summary>
    public void Submit(bool wait)
    {
        EndEncoder();
        if (_frameOutput is { } output)
        {
            // A clear with nothing drawn after it still has to reach the output.
            if (output.IsAllocated && (output.PendingColor is not null || output.PendingStencil))
            {
                EnsureCommandBuffer();
                BeginPass(output, end: true);
            }
            _frameOutput = null;
        }
        if (_signals.Count > 0) EnsureCommandBuffer();
        if (_commandBuffer != 0)
        {
            foreach (var (sharedEvent, value) in _signals)
                CallEvent(_commandBuffer, "encodeSignalEvent:value:", sharedEvent, value);
            _signals.Clear();
            Call(_commandBuffer, "commit");
            Release(_lastCommitted);
            _lastCommitted = _commandBuffer;
            _commandBuffer = 0;
        }
        if (wait && _lastCommitted != 0) Call(_lastCommitted, "waitUntilCompleted");
        if (_pool != 0)
        {
            AutoreleasePoolPop(_pool);
            _pool = 0;
        }
    }

    /// <summary>Reads a single-sample target back as RGBA8 rows, bottom row first (glReadPixels order).</summary>
    public byte[] ReadPixels(GpuRenderTarget target)
    {
        var source = (MetalRenderTarget)target;
        if (source.Samples != 1 || source.ColorTexture == 0)
            throw new InvalidOperationException("Read back a single-sample target.");
        EnsureCommandBuffer();
        BeginPass(source, end: true);
        var rowBytes = source.Width * 4;
        var buffer = Call(Device, "newBufferWithLength:options:", rowBytes * source.Height, 0);
        try
        {
            var blit = Call(_commandBuffer, "blitCommandEncoder");
            CopyToBuffer(blit, source.ColorTexture, new MTLSize((nuint)source.Width, (nuint)source.Height, 1), buffer, rowBytes);
            Call(blit, "endEncoding");
            Submit(wait: true);
            var pixels = new byte[rowBytes * source.Height];
            new ReadOnlySpan<byte>((void*)Call(buffer, "contents"), pixels.Length).CopyTo(pixels);
            if (source.PixelFormat == PixelFormatBgra8)
                for (var index = 0; index < pixels.Length; index += 4)
                    (pixels[index], pixels[index + 2]) = (pixels[index + 2], pixels[index]);
            return pixels;
        }
        finally
        {
            Release(buffer);
        }
    }

    public override void Clear(EffectColor? color, bool stencil = false)
    {
        if (!ColorWrite) color = null;
        var target = CurrentTarget;
        if (!target.HasDepthStencil) stencil = false;
        if (color is null && !stencil) return;
        var full = Scissor is not { } s || s.X <= 0 && s.Y <= 0 && s.X + s.Width >= target.Width && s.Y + s.Height >= target.Height;
        if (full)
        {
            if (ReferenceEquals(_encoderTarget, target)) EndEncoder();
            if (color is { } c) target.PendingColor = c;
            if (stencil) target.PendingStencil = true;
            return;
        }
        // A partial clear draws its rectangle.
        var encoder = EnsureEncoder(target);
        var key = new PipelineKey(null, null, GpuBlend.None, color is not null, target.PixelFormat, target.Samples,
            target.HasDepthStencil);
        SetPipeline(encoder, key);
        SetDepthStencil(encoder, target, stencil ? GpuStencil.WriteOne : GpuStencil.Disabled, reference: 0);
        ApplyViewport(encoder, new GpuRect(0, 0, target.Width, target.Height), target);
        var value = color ?? EffectColor.Transparent;
        var data = stackalloc float[] { value.R, value.G, value.B, value.A };
        Call(encoder, "setFragmentBytes:length:atIndex:", (nint)data, 16, 0);
        Call(encoder, "drawPrimitives:vertexStart:vertexCount:", 3, 0, 3);
    }

    public override void Draw(GpuShader shader, GpuTopology topology, int vertexCount)
    {
        var encoder = Prepare((MetalShader)shader, null);
        Call(encoder, "drawPrimitives:vertexStart:vertexCount:", topology == GpuTopology.TriangleStrip ? 4 : 3, 0, vertexCount);
    }

    public override void Draw(GpuShader shader, GpuVertexLayout layout, ReadOnlySpan<byte> vertices, int vertexCount)
    {
        if (vertexCount == 0) return;
        var encoder = Prepare((MetalShader)shader, layout);
        Bind(encoder, vertices, GpuCompiledProgram.MslVertexBufferIndex, vertex: true);
        Call(encoder, "drawPrimitives:vertexStart:vertexCount:", 3, 0, vertexCount);
    }

    public override void Draw(GpuShader shader, GpuVertexLayout layout, GpuBuffer vertices, int firstVertex, int vertexCount)
    {
        var encoder = Prepare((MetalShader)shader, layout);
        Call(encoder, "setVertexBuffer:offset:atIndex:", ((MetalBuffer)vertices).Handle, 0, GpuCompiledProgram.MslVertexBufferIndex);
        Call(encoder, "drawPrimitives:vertexStart:vertexCount:", 3, firstVertex, vertexCount);
    }

    public override void DrawIndexed(GpuShader shader, GpuVertexLayout layout, GpuBuffer vertices, GpuBuffer indices,
        int firstIndex, int indexCount)
    {
        var encoder = Prepare((MetalShader)shader, layout);
        Call(encoder, "setVertexBuffer:offset:atIndex:", ((MetalBuffer)vertices).Handle, 0, GpuCompiledProgram.MslVertexBufferIndex);
        Call(encoder, "drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:", 3, indexCount, 1,
            ((MetalBuffer)indices).Handle, firstIndex * sizeof(uint));
    }

    public override void Resolve(GpuRenderTarget source, GpuRenderTarget destination)
    {
        var from = (MetalRenderTarget)source;
        var to = (MetalRenderTarget)destination;
        if (!from.IsAllocated || !to.IsAllocated || to.Samples != 1 || from.Width != to.Width || from.Height != to.Height)
            throw new InvalidOperationException("MSAA resolve requires an equally sized single-sample destination.");
        EnsureCommandBuffer();
        EndEncoder();
        if (from.Samples > 1)
        {
            using var pool = Pool.Push();
            var pass = Call(Class("MTLRenderPassDescriptor"), "renderPassDescriptor");
            var color = Call(Call(pass, "colorAttachments"), "objectAtIndexedSubscript:", 0);
            Call(color, "setTexture:", from.ColorTexture);
            Call(color, "setResolveTexture:", to.ColorTexture);
            ColorLoad(color, from);
            Call(color, "setStoreAction:", 3);
            var encoder = Call(_commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
            Call(encoder, "endEncoding");
        }
        else
        {
            BeginPass(from, end: true);
            var blit = Call(_commandBuffer, "blitCommandEncoder");
            Call(blit, "copyFromTexture:toTexture:", from.ColorTexture, to.ColorTexture);
            Call(blit, "endEncoding");
        }
        to.PendingColor = null;
        SetTarget(destination);
    }

    protected override void OnTargetChanged(GpuRenderTarget target)
    {
        // The pass on the previous target stays open until something else needs the command buffer.
    }

    internal nint NewTexture(int width, int height, nint format, int samples, bool renderTarget)
    {
        using var pool = Pool.Push();
        var descriptor = Call(Class("MTLTextureDescriptor"), "texture2DDescriptorWithPixelFormat:width:height:mipmapped:",
            format, width, height, 0);
        if (samples > 1)
        {
            Call(descriptor, "setTextureType:", 4);
            Call(descriptor, "setSampleCount:", samples);
        }
        Call(descriptor, "setUsage:", renderTarget && samples == 1 ? 1 | 4 : 4);
        Call(descriptor, "setStorageMode:", 2);
        var texture = Call(Device, "newTextureWithDescriptor:", descriptor);
        if (texture == 0) throw new InvalidOperationException($"Metal could not allocate a {width}x{height}, {samples}-sample target.");
        return texture;
    }

    internal void Forget(MetalRenderTarget target)
    {
        if (ReferenceEquals(_encoderTarget, target)) EndEncoder();
    }

    private MetalRenderTarget CurrentTarget =>
        (MetalRenderTarget)(Target ?? throw new InvalidOperationException("No render target is bound."));

    private nint Prepare(MetalShader shader, GpuVertexLayout? layout)
    {
        var target = CurrentTarget;
        var encoder = EnsureEncoder(target);
        SetPipeline(encoder, new PipelineKey(shader.Program, layout, Blend, ColorWrite, target.PixelFormat,
            target.Samples, target.HasDepthStencil));
        SetDepthStencil(encoder, target, Stencil, reference: 1);
        ApplyViewport(encoder, Viewport, target);
        if (shader.VertexUniforms.Length > 0)
            Bind(encoder, shader.VertexUniforms, GpuCompiledProgram.MslUniformBufferIndex, vertex: true);
        if (shader.FragmentUniforms.Length > 0)
            Bind(encoder, shader.FragmentUniforms, GpuCompiledProgram.MslUniformBufferIndex, vertex: false);
        var textures = shader.Textures;
        for (var index = 0; index < textures.Length; index++)
        {
            var texture = textures[index] is MetalTexture { IsDisposed: false } t ? t : _empty;
            Call(encoder, "setFragmentTexture:atIndex:", texture.Handle, index);
            Call(encoder, "setFragmentSamplerState:atIndex:", _samplers[(int)texture.Filter], index);
        }
        return encoder;
    }

    private void Bind(nint encoder, ReadOnlySpan<byte> data, int index, bool vertex)
    {
        if (data.Length <= InlineBytesLimit)
        {
            fixed (byte* bytes = data)
                Call(encoder, vertex ? "setVertexBytes:length:atIndex:" : "setFragmentBytes:length:atIndex:",
                    (nint)bytes, data.Length, index);
            return;
        }
        var (buffer, offset) = Allocate(data);
        Call(encoder, vertex ? "setVertexBuffer:offset:atIndex:" : "setFragmentBuffer:offset:atIndex:", buffer, offset, index);
    }

    private nint EnsureCommandBuffer()
    {
        if (_commandBuffer != 0) return _commandBuffer;
        if (_pool == 0) _pool = AutoreleasePoolPush();
        _commandBuffer = Retain(Call(Queue, "commandBuffer"));
        foreach (var (sharedEvent, value) in _waits)
            CallEvent(_commandBuffer, "encodeWaitForEvent:value:", sharedEvent, value);
        _waits.Clear();
        _chunk = null;
        return _commandBuffer;
    }

    private nint EnsureEncoder(MetalRenderTarget target)
    {
        if (_encoder != 0 && ReferenceEquals(_encoderTarget, target)) return _encoder;
        EnsureCommandBuffer();
        EndEncoder();
        BeginPass(target, end: false);
        return _encoder;
    }

    /// <summary>Opens a render pass on <paramref name="target"/> that applies its pending clears.</summary>
    private void BeginPass(MetalRenderTarget target, bool end)
    {
        if (end && target.PendingColor is null && !target.PendingStencil) return;
        if (!target.IsAllocated) throw new InvalidOperationException("Render target has no storage.");
        var pass = Call(Class("MTLRenderPassDescriptor"), "renderPassDescriptor");
        var color = Call(Call(pass, "colorAttachments"), "objectAtIndexedSubscript:", 0);
        Call(color, "setTexture:", target.ColorTexture);
        ColorLoad(color, target);
        Call(color, "setStoreAction:", 1);
        if (target.HasDepthStencil)
        {
            var stencil = Call(pass, "stencilAttachment");
            Call(stencil, "setTexture:", target.StencilTexture);
            Call(stencil, "setLoadAction:", target.PendingStencil || !target.StencilInitialized ? 2 : 1);
            Call(stencil, "setClearStencil:", 0);
            Call(stencil, "setStoreAction:", 1);
            target.StencilInitialized = true;
            target.PendingStencil = false;
        }
        var encoder = Call(_commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (end)
        {
            Call(encoder, "endEncoding");
            return;
        }
        _encoder = Retain(encoder);
        _encoderTarget = target;
        _encoderPipeline = null;
        _encoderDepthStencil = null;
        _encoderViewport = null;
        _encoderScissor = null;
    }

    private static void ColorLoad(nint attachment, MetalRenderTarget target)
    {
        if (target.PendingColor is { } c)
        {
            Call(attachment, "setLoadAction:", 2);
            Call(attachment, "setClearColor:", new MTLClearColor(c.R, c.G, c.B, c.A));
            target.PendingColor = null;
        }
        else
            Call(attachment, "setLoadAction:", 1);
    }

    private void EndEncoder()
    {
        if (_encoder == 0) return;
        Call(_encoder, "endEncoding");
        Release(_encoder);
        _encoder = 0;
        _encoderTarget = null;
    }

    private void SetPipeline(nint encoder, PipelineKey key)
    {
        if (_encoderPipeline == key) return;
        if (!_pipelines.TryGetValue(key, out var pipeline))
        {
            pipeline = CreatePipeline(key);
            _pipelines.Add(key, pipeline);
        }
        Call(encoder, "setRenderPipelineState:", pipeline);
        _encoderPipeline = key;
    }

    private nint CreatePipeline(PipelineKey key)
    {
        using var pool = Pool.Push();
        var descriptor = Call(Call(Class("MTLRenderPipelineDescriptor"), "alloc"), "init");
        try
        {
            var (vertex, fragment) = key.Program is null ? (_clearVertex, _clearFragment) : _functions[key.Program];
            Call(descriptor, "setVertexFunction:", vertex);
            Call(descriptor, "setFragmentFunction:", fragment);
            Call(descriptor, "setRasterSampleCount:", key.Samples);
            var color = Call(Call(descriptor, "colorAttachments"), "objectAtIndexedSubscript:", 0);
            Call(color, "setPixelFormat:", key.PixelFormat);
            Call(color, "setWriteMask:", key.ColorWrite ? 0xF : 0);
            if (key.Blend != GpuBlend.None)
            {
                var (source, destination) = key.Blend switch
                {
                    GpuBlend.Additive => (1, 1),
                    GpuBlend.Screen => (1, 3),
                    GpuBlend.Multiply => (6, 5),
                    _ => (1, 5),
                };
                Call(color, "setBlendingEnabled:", 1);
                Call(color, "setSourceRGBBlendFactor:", source);
                Call(color, "setSourceAlphaBlendFactor:", source);
                Call(color, "setDestinationRGBBlendFactor:", destination);
                Call(color, "setDestinationAlphaBlendFactor:", destination);
            }
            if (key.HasDepthStencil)
            {
                Call(descriptor, "setStencilAttachmentPixelFormat:", PixelFormatStencil8);
            }
            if (key.Layout is { } layout)
            {
                var vertexDescriptor = Call(Class("MTLVertexDescriptor"), "vertexDescriptor");
                var attributes = Call(vertexDescriptor, "attributes");
                foreach (var attribute in layout.Attributes)
                {
                    var entry = Call(attributes, "objectAtIndexedSubscript:", attribute.Location);
                    Call(entry, "setFormat:", 27 + attribute.Components);
                    Call(entry, "setOffset:", attribute.Offset);
                    Call(entry, "setBufferIndex:", GpuCompiledProgram.MslVertexBufferIndex);
                }
                var bufferLayout = Call(Call(vertexDescriptor, "layouts"), "objectAtIndexedSubscript:",
                    GpuCompiledProgram.MslVertexBufferIndex);
                Call(bufferLayout, "setStride:", layout.Stride);
                Call(bufferLayout, "setStepFunction:", 1);
                Call(descriptor, "setVertexDescriptor:", vertexDescriptor);
            }
            nint error = 0;
            var pipeline = Call(Device, "newRenderPipelineStateWithDescriptor:error:", descriptor, (nint)(&error));
            if (pipeline == 0)
                throw new InvalidOperationException($"Metal pipeline '{key.Program?.Name ?? "clear"}' failed: {Describe(error)}");
            return pipeline;
        }
        finally
        {
            Release(descriptor);
        }
    }

    private void SetDepthStencil(nint encoder, MetalRenderTarget target, GpuStencil stencil, uint reference)
    {
        var key = (target.HasDepthStencil ? stencil : GpuStencil.Disabled, reference == 0);
        if (_encoderDepthStencil == key) return;
        if (!_depthStencilStates.TryGetValue(key, out var state))
        {
            using var pool = Pool.Push();
            var descriptor = Call(Call(Class("MTLDepthStencilDescriptor"), "alloc"), "init");
            Call(descriptor, "setDepthCompareFunction:", 7);
            Call(descriptor, "setDepthWriteEnabled:", 0);
            if (key.Item1 != GpuStencil.Disabled)
            {
                var face = Call(Call(Class("MTLStencilDescriptor"), "alloc"), "init");
                Call(face, "setStencilCompareFunction:", key.Item1 == GpuStencil.EqualOne ? 2 : 7);
                Call(face, "setDepthStencilPassOperation:", key.Item1 == GpuStencil.WriteOne ? 2 : 0);
                Call(face, "setReadMask:", 0xFF);
                Call(face, "setWriteMask:", 0xFF);
                Call(descriptor, "setFrontFaceStencil:", face);
                Call(descriptor, "setBackFaceStencil:", face);
                Release(face);
            }
            state = Call(Device, "newDepthStencilStateWithDescriptor:", descriptor);
            Release(descriptor);
            _depthStencilStates.Add(key, state);
        }
        Call(encoder, "setDepthStencilState:", state);
        Call(encoder, "setStencilReferenceValue:", (nint)reference);
        _encoderDepthStencil = key;
    }

    private void ApplyViewport(nint encoder, GpuRect viewport, MetalRenderTarget target)
    {
        if (_encoderViewport != viewport)
        {
            Call(encoder, "setViewport:", new MTLViewport(viewport.X, viewport.Y, viewport.Width, viewport.Height, 0, 1));
            _encoderViewport = viewport;
        }
        var scissor = Scissor ?? new GpuRect(0, 0, target.Width, target.Height);
        var x = Math.Clamp(scissor.X, 0, target.Width);
        var y = Math.Clamp(scissor.Y, 0, target.Height);
        var clipped = new GpuRect(x, y, Math.Clamp(scissor.X + scissor.Width, x, target.Width) - x,
            Math.Clamp(scissor.Y + scissor.Height, y, target.Height) - y);
        if (_encoderScissor == clipped) return;
        Call(encoder, "setScissorRect:", new MTLScissorRect((nuint)clipped.X, (nuint)clipped.Y, (nuint)clipped.Width, (nuint)clipped.Height));
        _encoderScissor = clipped;
    }

    private (nint Buffer, nint Offset) Allocate(ReadOnlySpan<byte> data)
    {
        var size = (data.Length + 255) & ~255;
        if (_chunk is null || _chunk.Offset + size > _chunk.Size)
        {
            _chunk = _chunks.FirstOrDefault(chunk => chunk.Size >= size && chunk.IsIdle);
            if (_chunk is null)
            {
                var capacity = Math.Max(4 * 1024 * 1024, size);
                _chunk = new ArenaChunk(Call(Device, "newBufferWithLength:options:", capacity, 0), capacity);
                _chunks.Add(_chunk);
                Resources.Buffer(_chunk.Ledger.Id, capacity);
            }
            _chunk.Claim(_commandBuffer);
        }
        var offset = _chunk.Offset;
        fixed (byte* bytes = data)
            Buffer.MemoryCopy(bytes, (byte*)_chunk.Contents + offset, _chunk.Size - offset, data.Length);
        _chunk.Offset += size;
        return (_chunk.Buffer, offset);
    }

    private (nint Vertex, nint Fragment) Library(string source, string vertexName, string? fragmentName, string name)
    {
        nint error = 0;
        var library = Call(Device, "newLibraryWithSource:options:error:", String(source), 0, (nint)(&error));
        if (library == 0) throw new InvalidOperationException($"Metal shader '{name}' failed to compile: {Describe(error)}");
        try
        {
            var vertex = Call(library, "newFunctionWithName:", String(vertexName));
            var fragment = fragmentName is null ? 0 : Call(library, "newFunctionWithName:", String(fragmentName));
            return (vertex, fragment);
        }
        finally
        {
            Release(library);
        }
    }

    private nint Sampler(nint filter, nint mipFilter)
    {
        using var pool = Pool.Push();
        var descriptor = Call(Call(Class("MTLSamplerDescriptor"), "alloc"), "init");
        Call(descriptor, "setMinFilter:", filter);
        Call(descriptor, "setMagFilter:", filter);
        Call(descriptor, "setMipFilter:", mipFilter);
        Call(descriptor, "setSAddressMode:", 0);
        Call(descriptor, "setTAddressMode:", 0);
        var sampler = Call(Device, "newSamplerStateWithDescriptor:", descriptor);
        Release(descriptor);
        return sampler;
    }

    public override void Abandon()
    {
        base.Abandon();
        _commandBuffer = _encoder = _lastCommitted = 0;
    }

    protected override void DisposeCore()
    {
        Submit(wait: true);
        Release(_lastCommitted);
        _lastCommitted = 0;
        foreach (var pipeline in _pipelines.Values) Release(pipeline);
        foreach (var state in _depthStencilStates.Values) Release(state);
        foreach (var (vertex, fragment) in _functions.Values) { Release(vertex); Release(fragment); }
        foreach (var sampler in _samplers) Release(sampler);
        foreach (var chunk in _chunks)
        {
            chunk.Release();
            Resources.ReleaseBuffer(chunk.Ledger.Id);
        }
        _empty.Dispose();
        Release(_clearVertex);
        Release(_clearFragment);
        Release(Queue);
        Release(Device);
    }

    private readonly record struct PipelineKey(GpuCompiledProgram? Program, GpuVertexLayout? Layout, GpuBlend Blend,
        bool ColorWrite, nint PixelFormat, int Samples, bool HasDepthStencil)
    {
        public bool Equals(PipelineKey other) => ReferenceEquals(Program, other.Program) &&
            ReferenceEquals(Layout, other.Layout) && Blend == other.Blend && ColorWrite == other.ColorWrite &&
            PixelFormat == other.PixelFormat && Samples == other.Samples && HasDepthStencil == other.HasDepthStencil;
        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Program),
            RuntimeHelpers.GetHashCode(Layout), Blend, ColorWrite, PixelFormat, Samples, HasDepthStencil);
    }

    /// <summary>A shared upload buffer, reused once the command buffer that last wrote it has completed.</summary>
    private sealed class ArenaChunk(nint buffer, int size)
    {
        private nint _owner;
        public nint Buffer { get; } = buffer;
        public nint Contents { get; } = Call(buffer, "contents");
        public int Size { get; } = size;
        public int Offset { get; set; }
        public LedgerEntry Ledger { get; } = new();

        public bool IsIdle => _owner == 0 || CallULong(_owner, "status") >= 4;

        public void Claim(nint commandBuffer)
        {
            ObjC.Release(_owner);
            _owner = Retain(commandBuffer);
            Offset = 0;
        }

        public void Release()
        {
            ObjC.Release(_owner);
            ObjC.Release(Buffer);
        }
    }

    internal sealed class LedgerEntry : GpuResource
    {
        protected override void DisposeCore() { }
    }
}

[SupportedOSPlatform("macos")]
internal sealed class MetalTexture : GpuTexture
{
    private readonly bool _owns;

    public MetalTexture(nint handle, int width, int height, GpuFilter filter, bool owns)
    {
        Handle = handle;
        Width = width;
        Height = height;
        Filter = filter;
        _owns = owns;
    }

    public nint Handle { get; }

    protected override void DisposeCore()
    {
        if (_owns) ObjC.Release(Handle);
    }
}

[SupportedOSPlatform("macos")]
internal sealed class MetalBuffer(nint handle, long size) : GpuBuffer
{
    public nint Handle { get; } = handle;
    public long Length { get; } = size;

    protected override void DisposeCore() => ObjC.Release(Handle);
}

[SupportedOSPlatform("macos")]
internal sealed class MetalShader(GpuCompiledProgram program) : GpuBlockShader(program)
{
    protected override void DisposeCore() { }
}

[SupportedOSPlatform("macos")]
internal sealed class MetalRenderTarget : GpuRenderTarget
{
    private readonly MetalGpuDevice _device;
    private readonly bool _external;
    private MetalTexture? _texture;
    private int _requestedSamples;

    public MetalRenderTarget(MetalGpuDevice device, bool depthStencil)
    {
        _device = device;
        HasDepthStencil = depthStencil;
        PixelFormat = MetalGpuDevice.PixelFormatRgba8;
    }

    private MetalRenderTarget(MetalGpuDevice device, nint texture, int width, int height, nint pixelFormat)
    {
        _device = device;
        _external = true;
        HasDepthStencil = true;
        PixelFormat = pixelFormat;
        ColorTexture = texture;
        Width = width;
        Height = height;
        StencilTexture = device.NewTexture(width, height, MetalGpuDevice.PixelFormatStencil8, 1, renderTarget: true);
        _texture = new MetalTexture(texture, width, height, GpuFilter.Linear, owns: false);
    }

    public static MetalRenderTarget External(MetalGpuDevice device, nint texture, int width, int height, nint pixelFormat) =>
        new(device, texture, width, height, pixelFormat);

    public nint PixelFormat { get; }
    public nint ColorTexture { get; private set; }
    public nint StencilTexture { get; private set; }
    public override GpuTexture? Texture => Samples == 1 ? _texture : null;

    /// <summary>A full clear waiting for the next pass's load action.</summary>
    internal EffectColor? PendingColor { get; set; }
    internal bool PendingStencil { get; set; }
    internal bool StencilInitialized { get; set; }

    public override void EnsureSize(int width, int height, int samples = 1)
    {
        if (_external) throw new InvalidOperationException("An external target cannot be resized.");
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        samples = Math.Clamp(samples, 1, _device.MaxSamples);
        if (samples > 1 && !HasDepthStencil)
            throw new InvalidOperationException("Multisampled targets carry a depth/stencil attachment.");
        if (width == Width && height == Height && samples == _requestedSamples && ColorTexture != 0) return;
        Release();
        ColorTexture = _device.NewTexture(width, height, PixelFormat, samples, renderTarget: true);
        if (HasDepthStencil)
            StencilTexture = _device.NewTexture(width, height, MetalGpuDevice.PixelFormatStencil8, samples, renderTarget: true);
        Width = width;
        Height = height;
        Samples = samples;
        _requestedSamples = samples;
        if (samples == 1) _texture = new MetalTexture(ColorTexture, width, height, GpuFilter.Linear, owns: false);
        _device.Resources.Target(Id, (long)width * height * (HasDepthStencil ? 5 : 4) * samples);
    }

    public override void Release()
    {
        _device.Forget(this);
        if (_external)
        {
            ObjC.Release(StencilTexture);
            StencilTexture = 0;
            _texture?.Abandon();
            return;
        }
        _device.Resources.ReleaseTarget(Id);
        _texture?.Abandon();
        _texture = null;
        ObjC.Release(ColorTexture);
        ObjC.Release(StencilTexture);
        ColorTexture = StencilTexture = 0;
        Width = Height = 0;
        Samples = 1;
        _requestedSamples = 0;
        PendingColor = null;
        PendingStencil = false;
        StencilInitialized = false;
    }
}
