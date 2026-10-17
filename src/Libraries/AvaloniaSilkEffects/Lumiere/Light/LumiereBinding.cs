using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects.Lumiere.Light;

/// <summary>The framebuffer and viewport bound when a Lumiere group starts, restored after it.</summary>
internal readonly record struct LumiereBinding(uint Framebuffer, int X, int Y, int Width, int Height)
{
    public static unsafe LumiereBinding Capture(GL gl)
    {
        gl.GetInteger(GetPName.DrawFramebufferBinding, out var framebuffer);
        var viewport = stackalloc int[4];
        gl.GetInteger(GetPName.Viewport, viewport);
        return new LumiereBinding((uint)framebuffer, viewport[0], viewport[1], viewport[2], viewport[3]);
    }

    public void Restore(GL gl)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Framebuffer);
        gl.Viewport(X, Y, (uint)Width, (uint)Height);
    }
}
