using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects;

/// <summary>A single-sample RGBA8 color target (no depth / stencil), like a Pixi filter texture.</summary>
internal sealed class EffectColorTarget(GL gl) : IDisposable
{
    public uint Framebuffer { get; private set; }
    public uint Texture { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public unsafe void EnsureSize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (Framebuffer != 0 && width == Width && height == Height)
            return;
        Dispose();
        Width = width;
        Height = height;
        Texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, Texture);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        Framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Framebuffer);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, Texture, 0);
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            Dispose();
            throw new InvalidOperationException($"Effect color target {width}x{height} is incomplete: {status}.");
        }
        EffectGpuResources.For(gl).Target(Framebuffer, (long)width * height * 4);
    }

    public void Bind(bool clear = false)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Framebuffer);
        gl.Viewport(0, 0, (uint)Width, (uint)Height);
        if (!clear) return;
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void Dispose()
    {
        EffectGpuResources.For(gl).ReleaseTarget(Framebuffer);
        if (Framebuffer != 0) gl.DeleteFramebuffer(Framebuffer);
        if (Texture != 0) gl.DeleteTexture(Texture);
        Framebuffer = 0;
        Texture = 0;
        Width = 0;
        Height = 0;
    }
}

