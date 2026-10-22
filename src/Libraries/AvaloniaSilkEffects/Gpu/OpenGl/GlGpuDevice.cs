using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects.Gpu.OpenGl;

/// <summary>
/// The OpenGL 3.3 / ES 3.0 backend. GL state is cached between draws and invalidated at every frame start,
/// because the platform (Avalonia's compositor, a test harness) owns the context in between.
/// </summary>
internal sealed class GlGpuDevice : GpuDevice
{
    private readonly uint _emptyVao;
    private readonly uint _streamVbo;
    private readonly Dictionary<GpuVertexLayout, uint> _streamVaos = [];
    private nuint _streamCapacity;
    private readonly GlGpuResource _streamLedger = new();

    // Applied-state cache; null / -1 means unknown.
    private uint? _boundFramebuffer;
    private GpuRect? _appliedViewport;
    private GpuRect? _appliedScissor;
    private bool? _scissorEnabled;
    private GpuBlend? _appliedBlend;
    private bool? _appliedColorWrite;
    private GpuStencil? _appliedStencil;
    private uint? _program;
    private uint? _vao;
    private readonly uint?[] _units = new uint?[16];
    private int _activeUnit = -1;

    public GlGpuDevice(GL gl)
    {
        Gl = gl;
        Version = gl.GetStringS(StringName.Version);
        Renderer = gl.GetStringS(StringName.Renderer);
        gl.GetInteger(GetPName.MajorVersion, out var major);
        gl.GetInteger(GetPName.MinorVersion, out var minor);
        IsGles = Version.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
        if ((!IsGles && (major < 3 || major == 3 && minor < 3)) || (IsGles && major < 3))
            throw new NotSupportedException($"AvaloniaSilkEffects requires OpenGL 3.3+ or OpenGL ES 3.0+; current context is {Version}.");
        gl.GetInteger(GLEnum.MaxSamples, out var maxSamples);
        MaxSamples = Math.Max(1, maxSamples);
        _emptyVao = gl.GenVertexArray();
        _streamVbo = gl.GenBuffer();
    }

    public GL Gl { get; }
    public string Version { get; }
    public override EffectGpuBackend Backend => EffectGpuBackend.OpenGl;
    public override string ApiVersion => Version;
    public override string Renderer { get; }
    public override int MaxSamples { get; }
    public override bool IsGles { get; }

    public override GpuTexture CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, GpuFilter filter)
    {
        InvalidateUnit();
        return GlTexture.Create(Gl, width, height, rgba, filter);
    }

    public override unsafe GpuBuffer CreateBuffer(ReadOnlySpan<byte> data, bool index)
    {
        var buffer = new GlBuffer(this, data.Length, index);
        _vao = null;
        var target = index ? BufferTargetARB.ElementArrayBuffer : BufferTargetARB.ArrayBuffer;
        if (index) Gl.BindVertexArray(0);
        Gl.BindBuffer(target, buffer.Handle);
        fixed (byte* pointer = data)
            Gl.BufferData(target, (nuint)data.Length, pointer, BufferUsageARB.StaticDraw);
        Resources.Buffer(buffer.Id, data.Length);
        Resources.Uploaded(data.Length);
        return buffer;
    }

    public override GpuShader CreateShader(string vertexSource, string fragmentSource, string name)
    {
        _program = null;
        return new GlShader(this, vertexSource, fragmentSource, name);
    }

    public override GpuRenderTarget CreateRenderTarget(bool depthStencil) => new GlRenderTarget(this, depthStencil);

    /// <summary>Wraps a framebuffer the platform owns, such as Avalonia's per-frame surface.</summary>
    public GpuRenderTarget WrapFramebuffer(int framebuffer, int width, int height) =>
        GlRenderTarget.External(this, (uint)framebuffer, width, height);

    public override void BeginFrame(GpuRenderTarget output)
    {
        InvalidateState();
        Gl.Disable(EnableCap.DepthTest);
        Gl.Disable(EnableCap.CullFace);
        // GLES always rasterizes multisampled attachments with MSAA; desktop GL needs the capability.
        if (!IsGles) Gl.Enable(EnableCap.Multisample);
        base.BeginFrame(output);
    }

    public override void EndFrame()
    {
        Gl.BindVertexArray(0);
        Gl.UseProgram(0);
        Gl.ActiveTexture(TextureUnit.Texture0);
        Gl.Disable(EnableCap.ScissorTest);
        Gl.Disable(EnableCap.StencilTest);
        Gl.ColorMask(true, true, true, true);
        InvalidateState();
    }

    public override void Clear(EffectColor? color, bool stencil = false)
    {
        if (color is null && !stencil) return;
        ApplyTarget();
        ApplyScissor();
        ApplyColorWrite();
        var mask = (ClearBufferMask)0;
        if (color is { } c)
        {
            Gl.ClearColor(c.R, c.G, c.B, c.A);
            mask |= ClearBufferMask.ColorBufferBit;
        }
        if (stencil)
        {
            Gl.StencilMask(0xFF);
            Gl.ClearStencil(0);
            mask |= ClearBufferMask.StencilBufferBit;
        }
        Gl.Clear(mask);
    }

    public override void Draw(GpuShader shader, GpuTopology topology, int vertexCount)
    {
        Prepare((GlShader)shader);
        BindVao(_emptyVao);
        Gl.DrawArrays(Primitive(topology), 0, (uint)vertexCount);
    }

    public override unsafe void Draw(GpuShader shader, GpuVertexLayout layout, ReadOnlySpan<byte> vertices, int vertexCount)
    {
        if (vertexCount == 0) return;
        Prepare((GlShader)shader);
        if (!_streamVaos.TryGetValue(layout, out var vao))
        {
            vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, _streamVbo);
            ConfigureAttributes(layout);
            _streamVaos.Add(layout, vao);
            _vao = vao;
        }
        BindVao(vao);
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, _streamVbo);
        var bytes = (nuint)vertices.Length;
        if (bytes > _streamCapacity)
        {
            _streamCapacity = EffectPrimitiveRenderer.GrowCapacity(bytes);
            Gl.BufferData(BufferTargetARB.ArrayBuffer, _streamCapacity, null, BufferUsageARB.StreamDraw);
            Resources.Buffer(_streamLedger.Id, (long)_streamCapacity);
        }
        fixed (byte* data = vertices)
            Gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, bytes, data);
        Gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)vertexCount);
    }

    public override void Draw(GpuShader shader, GpuVertexLayout layout, GpuBuffer vertices, int firstVertex, int vertexCount)
    {
        Prepare((GlShader)shader);
        BindVao(((GlBuffer)vertices).VaoFor(layout, null));
        Gl.DrawArrays(PrimitiveType.Triangles, firstVertex, (uint)vertexCount);
    }

    public override unsafe void DrawIndexed(GpuShader shader, GpuVertexLayout layout, GpuBuffer vertices, GpuBuffer indices,
        int firstIndex, int indexCount)
    {
        Prepare((GlShader)shader);
        BindVao(((GlBuffer)vertices).VaoFor(layout, (GlBuffer)indices));
        Gl.DrawElements(PrimitiveType.Triangles, (uint)indexCount, DrawElementsType.UnsignedInt,
            (void*)(firstIndex * sizeof(uint)));
    }

    public override void Resolve(GpuRenderTarget source, GpuRenderTarget destination)
    {
        var from = (GlRenderTarget)source;
        var to = (GlRenderTarget)destination;
        if (!from.IsAllocated || !to.IsAllocated || to.Samples != 1 ||
            from.Width != to.Width || from.Height != to.Height)
            throw new InvalidOperationException("MSAA resolve requires an equally sized single-sample destination.");
        // Blits honour the scissor test; a resolve always covers the whole target.
        Gl.Disable(EnableCap.ScissorTest);
        _scissorEnabled = false;
        Gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from.Framebuffer);
        Gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to.Framebuffer);
        Gl.BlitFramebuffer(0, 0, from.Width, from.Height, 0, 0, to.Width, to.Height,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        _boundFramebuffer = null;
        SetTarget(destination);
    }

    internal void ConfigureAttributes(GpuVertexLayout layout)
    {
        foreach (var attribute in layout.Attributes)
        {
            unsafe
            {
                Gl.VertexAttribPointer((uint)attribute.Location, attribute.Components, VertexAttribPointerType.Float, false,
                    (uint)layout.Stride, (void*)attribute.Offset);
            }
            Gl.EnableVertexAttribArray((uint)attribute.Location);
        }
    }

    internal void ForgetVao(uint vao)
    {
        if (_vao == vao) _vao = null;
    }

    internal void ForgetProgram(uint program)
    {
        if (_program == program) _program = null;
    }

    internal void ForgetFramebuffer(uint framebuffer)
    {
        if (_boundFramebuffer == framebuffer) _boundFramebuffer = null;
    }

    internal void InvalidateBindings()
    {
        _vao = null;
        _boundFramebuffer = null;
    }

    internal void InvalidateUnit()
    {
        _activeUnit = -1;
        Array.Clear(_units);
    }

    internal void UseProgram(uint program)
    {
        if (_program == program) return;
        Gl.UseProgram(program);
        _program = program;
    }

    private void Prepare(GlShader shader)
    {
        ApplyTarget();
        ApplyScissor();
        ApplyColorWrite();
        if (_appliedBlend != Blend)
        {
            ApplyBlend(Blend);
            _appliedBlend = Blend;
        }
        if (_appliedStencil != Stencil)
        {
            ApplyStencil(Stencil);
            _appliedStencil = Stencil;
        }
        UseProgram(shader.Handle);
        shader.Apply();
        var textures = shader.Textures;
        for (var unit = 0; unit < textures.Length; unit++)
        {
            var handle = textures[unit] is { IsDisposed: false } texture ? ((GlTexture)texture).Handle : 0u;
            if (_units[unit] == handle) continue;
            if (_activeUnit != unit)
            {
                Gl.ActiveTexture(TextureUnit.Texture0 + unit);
                _activeUnit = unit;
            }
            Gl.BindTexture(TextureTarget.Texture2D, handle);
            _units[unit] = handle;
        }
    }

    private void ApplyTarget()
    {
        var target = (GlRenderTarget)(Target ?? throw new InvalidOperationException("No render target is bound."));
        if (_boundFramebuffer != target.Framebuffer)
        {
            Gl.BindFramebuffer(FramebufferTarget.Framebuffer, target.Framebuffer);
            _boundFramebuffer = target.Framebuffer;
        }
        if (_appliedViewport != Viewport)
        {
            var v = Viewport;
            Gl.Viewport(v.X, v.Y, (uint)Math.Max(0, v.Width), (uint)Math.Max(0, v.Height));
            _appliedViewport = v;
        }
    }

    private void ApplyScissor()
    {
        if (Scissor is { } scissor)
        {
            if (_scissorEnabled != true)
            {
                Gl.Enable(EnableCap.ScissorTest);
                _scissorEnabled = true;
            }
            if (_appliedScissor != scissor)
            {
                Gl.Scissor(scissor.X, scissor.Y, (uint)Math.Max(0, scissor.Width), (uint)Math.Max(0, scissor.Height));
                _appliedScissor = scissor;
            }
        }
        else if (_scissorEnabled != false)
        {
            Gl.Disable(EnableCap.ScissorTest);
            _scissorEnabled = false;
        }
    }

    private void ApplyColorWrite()
    {
        if (_appliedColorWrite == ColorWrite) return;
        Gl.ColorMask(ColorWrite, ColorWrite, ColorWrite, ColorWrite);
        _appliedColorWrite = ColorWrite;
    }

    private void ApplyBlend(GpuBlend blend)
    {
        if (blend == GpuBlend.None)
        {
            Gl.Disable(EnableCap.Blend);
            return;
        }
        Gl.Enable(EnableCap.Blend);
        switch (blend)
        {
            case GpuBlend.Additive:
                Gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
                break;
            case GpuBlend.Screen:
                Gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcColor);
                break;
            case GpuBlend.Multiply:
                Gl.BlendFunc(BlendingFactor.DstColor, BlendingFactor.OneMinusSrcAlpha);
                break;
            default:
                Gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                break;
        }
    }

    private void ApplyStencil(GpuStencil stencil)
    {
        switch (stencil)
        {
            case GpuStencil.WriteOne:
                Gl.Enable(EnableCap.StencilTest);
                Gl.StencilMask(0xFF);
                Gl.StencilFunc(StencilFunction.Always, 1, 0xFF);
                Gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
                break;
            case GpuStencil.EqualOne:
                Gl.Enable(EnableCap.StencilTest);
                Gl.StencilFunc(StencilFunction.Equal, 1, 0xFF);
                Gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
                break;
            default:
                Gl.Disable(EnableCap.StencilTest);
                break;
        }
    }

    private void BindVao(uint vao)
    {
        if (_vao == vao) return;
        Gl.BindVertexArray(vao);
        _vao = vao;
    }

    private static PrimitiveType Primitive(GpuTopology topology) =>
        topology == GpuTopology.TriangleStrip ? PrimitiveType.TriangleStrip : PrimitiveType.Triangles;

    private void InvalidateState()
    {
        _boundFramebuffer = null;
        _appliedViewport = null;
        _appliedScissor = null;
        _scissorEnabled = null;
        _appliedBlend = null;
        _appliedColorWrite = null;
        _appliedStencil = null;
        _program = null;
        _vao = null;
        InvalidateUnit();
    }

    protected override void DisposeCore()
    {
        foreach (var vao in _streamVaos.Values) Gl.DeleteVertexArray(vao);
        _streamVaos.Clear();
        Gl.DeleteVertexArray(_emptyVao);
        Gl.DeleteBuffer(_streamVbo);
        Resources.ReleaseBuffer(_streamLedger.Id);
    }

    /// <summary>A ledger identity for storage the device owns directly.</summary>
    private sealed class GlGpuResource : GpuResource
    {
        protected override void DisposeCore() { }
    }
}

internal sealed class GlTexture : GpuTexture
{
    private readonly GL? _gl;

    private GlTexture(GL? gl, uint handle, int width, int height, GpuFilter filter)
    {
        _gl = gl;
        Handle = handle;
        Width = width;
        Height = height;
        Filter = filter;
    }

    public uint Handle { get; }

    public static unsafe GlTexture Create(GL gl, int width, int height, ReadOnlySpan<byte> rgba, GpuFilter filter)
    {
        var handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, handle);
        Parameters(gl, filter);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        fixed (byte* pixels = rgba)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, rgba.IsEmpty ? null : pixels);
        if (filter == GpuFilter.LinearMipmap)
            gl.GenerateMipmap(TextureTarget.Texture2D);
        return new GlTexture(gl, handle, width, height, filter);
    }

    /// <summary>A render target's color attachment; the target deletes it.</summary>
    public static GlTexture Attachment(uint handle, int width, int height) => new(null, handle, width, height, GpuFilter.Linear);

    internal static void Parameters(GL gl, GpuFilter filter)
    {
        var min = filter switch
        {
            GpuFilter.Nearest => GLEnum.Nearest,
            GpuFilter.LinearMipmap => GLEnum.LinearMipmapLinear,
            _ => GLEnum.Linear,
        };
        var mag = filter == GpuFilter.Nearest ? GLEnum.Nearest : GLEnum.Linear;
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mag);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
    }

    protected override void DisposeCore() => _gl?.DeleteTexture(Handle);
}

internal sealed class GlBuffer : GpuBuffer
{
    private readonly GlGpuDevice _device;
    private readonly bool _index;
    private readonly Dictionary<(GpuVertexLayout, long), uint> _vaos = [];

    public GlBuffer(GlGpuDevice device, long size, bool index)
    {
        _device = device;
        _index = index;
        Size = size;
        Handle = device.Gl.GenBuffer();
    }

    public uint Handle { get; }

    public uint VaoFor(GpuVertexLayout layout, GlBuffer? indices)
    {
        var key = (layout, indices?.Id ?? 0);
        if (_vaos.TryGetValue(key, out var vao)) return vao;
        var gl = _device.Gl;
        vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Handle);
        _device.ConfigureAttributes(layout);
        if (indices is not null) gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indices.Handle);
        gl.BindVertexArray(0);
        _device.InvalidateBindings();
        _vaos.Add(key, vao);
        return vao;
    }

    protected override void DisposeCore()
    {
        foreach (var vao in _vaos.Values)
        {
            _device.ForgetVao(vao);
            _device.Gl.DeleteVertexArray(vao);
        }
        _vaos.Clear();
        _device.Gl.DeleteBuffer(Handle);
        _device.Resources.ReleaseBuffer(Id);
    }
}

internal sealed class GlRenderTarget : GpuRenderTarget
{
    private readonly GlGpuDevice _device;
    private readonly bool _external;
    private GlTexture? _texture;
    private uint _colorBuffer;
    private uint _depthStencil;
    private int _requestedSamples;

    public GlRenderTarget(GlGpuDevice device, bool depthStencil)
    {
        _device = device;
        HasDepthStencil = depthStencil;
    }

    private GlRenderTarget(GlGpuDevice device, uint framebuffer, int width, int height)
    {
        _device = device;
        _external = true;
        Framebuffer = framebuffer;
        Width = width;
        Height = height;
        HasDepthStencil = true;
    }

    public static GlRenderTarget External(GlGpuDevice device, uint framebuffer, int width, int height) =>
        new(device, framebuffer, width, height);

    public uint Framebuffer { get; private set; }
    public override GpuTexture? Texture => _texture;

    public override unsafe void EnsureSize(int width, int height, int samples = 1)
    {
        if (_external) throw new InvalidOperationException("An external framebuffer cannot be resized.");
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        samples = Math.Max(1, samples);
        if (width == Width && height == Height && samples == _requestedSamples && Framebuffer != 0)
            return;
        Release();
        if (samples > 1 && !HasDepthStencil)
            throw new InvalidOperationException("Multisampled targets carry a depth/stencil attachment.");
        var gl = _device.Gl;
        Width = width;
        Height = height;
        _requestedSamples = samples;
        _device.InvalidateUnit();
        Framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Framebuffer);
        _device.InvalidateBindings();
        if (samples > 1)
        {
            _colorBuffer = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorBuffer);
            gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples,
                InternalFormat.Rgba8, (uint)width, (uint)height);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                RenderbufferTarget.Renderbuffer, _colorBuffer);
        }
        else
        {
            var handle = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, handle);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
            GlTexture.Parameters(gl, GpuFilter.Linear);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, handle, 0);
            _texture = GlTexture.Attachment(handle, width, height);
        }
        if (HasDepthStencil)
        {
            _depthStencil = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthStencil);
            if (samples > 1)
                gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples,
                    InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
            else
                gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, _depthStencil);
        }
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            Release();
            throw new InvalidOperationException($"AvaloniaSilkEffects framebuffer {width}x{height}, {samples} samples is incomplete: {status}.");
        }
        gl.GetInteger(GetPName.Samples, out var actualSamples);
        Samples = Math.Max(1, actualSamples);
        _device.Resources.Target(Id, (long)width * height * (HasDepthStencil ? 8 : 4) * Samples);
    }

    public override void Release()
    {
        if (_external) return;
        var gl = _device.Gl;
        _device.Resources.ReleaseTarget(Id);
        if (Framebuffer != 0)
        {
            _device.ForgetFramebuffer(Framebuffer);
            gl.DeleteFramebuffer(Framebuffer);
        }
        if (_texture is not null)
        {
            gl.DeleteTexture(_texture.Handle);
            _texture.Abandon();
            _device.InvalidateUnit();
        }
        if (_colorBuffer != 0) gl.DeleteRenderbuffer(_colorBuffer);
        if (_depthStencil != 0) gl.DeleteRenderbuffer(_depthStencil);
        Framebuffer = 0;
        _texture = null;
        _colorBuffer = 0;
        _depthStencil = 0;
        Width = 0;
        Height = 0;
        Samples = 1;
        _requestedSamples = 0;
    }
}

internal sealed class GlShader : GpuShader
{
    private readonly GlGpuDevice _device;
    private readonly Dictionary<string, Uniform?> _uniforms = [];
    private readonly Dictionary<string, int> _samplers = [];
    private readonly List<Uniform> _dirty = [];

    public GlShader(GlGpuDevice device, string vertexSource, string fragmentSource, string name)
    {
        _device = device;
        Name = name;
        var gl = device.Gl;
        var vertex = Compile(ShaderType.VertexShader, vertexSource);
        uint fragment;
        try { fragment = Compile(ShaderType.FragmentShader, fragmentSource); }
        catch { gl.DeleteShader(vertex); throw; }
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, vertex);
        gl.AttachShader(Handle, fragment);
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var linked);
        var log = gl.GetProgramInfoLog(Handle);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        if (linked == 0)
        {
            gl.DeleteProgram(Handle);
            throw new InvalidOperationException($"OpenGL program '{name}' failed to link: {log}");
        }

        gl.GetProgram(Handle, ProgramPropertyARB.ActiveUniforms, out var count);
        var samplers = new List<(string Name, int Location)>();
        for (var index = 0u; index < count; index++)
        {
            var uniformName = gl.GetActiveUniform(Handle, index, out var size, out var type);
            var location = gl.GetUniformLocation(Handle, uniformName);
            if (location < 0) continue;
            var baseName = uniformName.EndsWith("[0]", StringComparison.Ordinal) ? uniformName[..^3] : uniformName;
            if (type == UniformType.Sampler2D)
            {
                samplers.Add((baseName, location));
                continue;
            }
            var uniform = new Uniform(location, type);
            _uniforms[baseName] = uniform;
            if (baseName != uniformName) _uniforms[uniformName] = uniform;
        }
        Textures = new GpuTexture?[samplers.Count];
        device.UseProgram(Handle);
        for (var unit = 0; unit < samplers.Count; unit++)
        {
            gl.Uniform1(samplers[unit].Location, unit);
            _samplers[samplers[unit].Name] = unit;
        }
    }

    public uint Handle { get; }
    public GpuTexture?[] Textures { get; }

    public override void SetFloats(string name, ReadOnlySpan<float> values, int components)
    {
        if (Find(name) is not { } uniform) return;
        if (uniform.Floats.Length < values.Length) uniform.Floats = new float[values.Length];
        values.CopyTo(uniform.Floats);
        uniform.Count = values.Length;
        uniform.IntValue = values.IsEmpty ? 0 : (int)values[0];
        MarkDirty(uniform);
    }

    public override void SetInt(string name, int value)
    {
        if (Find(name) is not { } uniform) return;
        if (uniform.Floats.Length < 1) uniform.Floats = new float[1];
        uniform.Floats[0] = value;
        uniform.Count = 1;
        uniform.IntValue = value;
        MarkDirty(uniform);
    }

    public override void SetTexture(string name, GpuTexture? texture)
    {
        if (_samplers.TryGetValue(name, out var unit)) Textures[unit] = texture;
    }

    /// <summary>Uploads changed values; the program is bound.</summary>
    internal unsafe void Apply()
    {
        if (_dirty.Count == 0) return;
        var gl = _device.Gl;
        foreach (var uniform in _dirty)
        {
            uniform.Dirty = false;
            fixed (float* values = uniform.Floats)
            {
                var location = uniform.Location;
                switch (uniform.Type)
                {
                    case UniformType.Int:
                    case UniformType.Bool:
                        gl.Uniform1(location, uniform.IntValue);
                        break;
                    case UniformType.Float:
                        gl.Uniform1(location, (uint)uniform.Count, values);
                        break;
                    case UniformType.FloatVec2:
                        gl.Uniform2(location, (uint)(uniform.Count / 2), values);
                        break;
                    case UniformType.FloatVec3:
                        gl.Uniform3(location, (uint)(uniform.Count / 3), values);
                        break;
                    case UniformType.FloatVec4:
                        gl.Uniform4(location, (uint)(uniform.Count / 4), values);
                        break;
                    case UniformType.FloatMat3:
                        gl.UniformMatrix3(location, (uint)(uniform.Count / 9), false, values);
                        break;
                    case UniformType.FloatMat4:
                        gl.UniformMatrix4(location, (uint)(uniform.Count / 16), false, values);
                        break;
                }
            }
        }
        _dirty.Clear();
    }

    private Uniform? Find(string name)
    {
        if (_uniforms.TryGetValue(name, out var uniform)) return uniform;
        // An element of an array ("uColors[2]") has its own location.
        uniform = null;
        var bracket = name.IndexOf('[');
        if (bracket > 0 && _uniforms.TryGetValue(name[..bracket], out var array) && array is not null)
        {
            var location = _device.Gl.GetUniformLocation(Handle, name);
            if (location >= 0) uniform = new Uniform(location, array.Type);
        }
        _uniforms[name] = uniform;
        return uniform;
    }

    private void MarkDirty(Uniform uniform)
    {
        if (uniform.Dirty) return;
        uniform.Dirty = true;
        _dirty.Add(uniform);
    }

    private uint Compile(ShaderType type, string source)
    {
        var gl = _device.Gl;
        if (_device.IsGles)
            source = source.Replace("#version 330 core", "#version 300 es\nprecision highp float;");
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        var log = gl.GetShaderInfoLog(shader);
        if (compiled != 0)
            return shader;
        gl.DeleteShader(shader);
        throw new InvalidOperationException($"OpenGL shader '{Name}' failed to compile: {log}");
    }

    protected override void DisposeCore()
    {
        _device.ForgetProgram(Handle);
        _device.Gl.DeleteProgram(Handle);
    }

    private sealed class Uniform(int location, UniformType type)
    {
        public int Location { get; } = location;
        public UniformType Type { get; } = type;
        public float[] Floats { get; set; } = [];
        public int Count { get; set; }
        public int IntValue { get; set; }
        public bool Dirty { get; set; }
    }
}
