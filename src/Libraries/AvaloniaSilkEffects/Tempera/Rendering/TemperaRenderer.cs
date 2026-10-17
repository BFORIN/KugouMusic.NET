using System.Numerics;
using Silk.NET.OpenGL;
using AvaloniaSilkEffects.Tempera.Text;

namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>One scene-wide post pass, in chain order (Folia createSonnetPostProcessFilters + the transition blur).</summary>
internal enum TemperaPassKind
{
    Lens,
    Noise,
    Contrast,
    RgbShift,
    Vignette,
    BlurX,
    BlurY,
}

internal readonly record struct TemperaPass(TemperaPassKind Kind, float Amount, float Seed = 0);

/// <summary>
/// Draws Tempera node trees with Pixi's semantics: container alpha multiplies into descendants, Graphics are static meshes,
/// a text layer with the difference filter renders offscreen and is coloured against a copy of what is already drawn, and a
/// scene with filters renders into its own surface and runs its pass chain over the scene's on-screen bounds.
/// </summary>
internal sealed class TemperaRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly EffectPrimitiveRenderer _primitives;
    private readonly EffectTextureCache _textures;
    private readonly EffectShaderProgram _mesh;
    private readonly EffectShaderProgram _invert;
    private readonly Dictionary<TemperaPassKind, EffectShaderProgram> _passes = [];
    private readonly EffectShaderProgram _copy;
    private readonly uint _quadVao;
    private readonly uint _quadVbo;
    private readonly TemperaTarget _text;
    private readonly TemperaTarget _ping;
    private readonly TemperaTarget _pong;
    private readonly Vector3[] _stops = new Vector3[4];

    public TemperaRenderer(GL gl, EffectPrimitiveRenderer primitives, EffectTextureCache textures)
    {
        _gl = gl;
        _primitives = primitives;
        _textures = textures;
        _mesh = new EffectShaderProgram(gl, TemperaShaders.MeshVertex, TemperaShaders.MeshFragment, "tempera-mesh");
        _invert = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.InvertFragment, "tempera-difference");
        _copy = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.CopyFragment, "tempera-copy");
        _passes[TemperaPassKind.Lens] = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.LensFragment, "tempera-lens");
        _passes[TemperaPassKind.Noise] = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.NoiseFragment, "tempera-noise");
        _passes[TemperaPassKind.Contrast] = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.ContrastFragment, "tempera-contrast");
        _passes[TemperaPassKind.RgbShift] = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.RgbShiftFragment, "tempera-rgb-shift");
        _passes[TemperaPassKind.Vignette] = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.VignetteFragment, "tempera-vignette");
        var blur = new EffectShaderProgram(gl, TemperaShaders.PassVertex, TemperaShaders.BlurFragment, "tempera-blur");
        _passes[TemperaPassKind.BlurX] = blur;
        _passes[TemperaPassKind.BlurY] = blur;
        _text = new TemperaTarget(gl);
        _ping = new TemperaTarget(gl);
        _pong = new TemperaTarget(gl);
        unsafe
        {
            _quadVao = gl.GenVertexArray();
            _quadVbo = gl.GenBuffer();
            gl.BindVertexArray(_quadVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
            float[] quad = [0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1];
            fixed (float* data = quad)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), data, BufferUsageARB.StaticDraw);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.BindVertexArray(0);
        }
    }

    public int DrawCalls { get; private set; }

    public void BeginFrame() => DrawCalls = 0;

    // ---- Tree traversal -------------------------------------------------------------------------------------------------

    /// <summary>Draws <paramref name="root"/> into <paramref name="surface"/>; <paramref name="parent"/> maps logical pixels to the surface.</summary>
    public void Draw(TemperaNode root, TemperaSurface surface, Matrix3x2 parent, double parentAlpha = 1)
    {
        _primitives.SetViewport(new Vector2(surface.Width, surface.Height));
        DrawNode(root, surface, parent, parentAlpha);
        _primitives.Flush();
    }

    private void DrawNode(TemperaNode node, TemperaSurface surface, Matrix3x2 parent, double parentAlpha)
    {
        if (!node.Visible) return;
        var world = node.LocalMatrix * parent;
        var alpha = parentAlpha * node.Alpha;
        switch (node)
        {
            case TemperaInvertLayer layer:
                DrawInvertLayer(layer, surface, world, alpha);
                return;
            case TemperaGroup group:
                foreach (var child in group.Children) DrawNode(child, surface, world, alpha);
                return;
            case TemperaGraphic graphic:
                if (alpha > 0.0005) DrawGraphic(graphic, world, (float)alpha, surface.Width, surface.Height);
                return;
            case TemperaText text:
                if (alpha > 0.0005) DrawText(text, world, (float)alpha, surface.Scale);
                return;
        }
    }

    private void DrawGraphic(TemperaGraphic graphic, Matrix3x2 world, float alpha, int width, int height)
    {
        if (graphic.Parts.Count == 0) return;
        _primitives.Flush();
        if (graphic.Buffer is null)
        {
            graphic.Buffer = new TemperaMeshBuffer(_gl);
            graphic.Buffer.Upload(graphic);
        }
        _mesh.Use();
        Span<float> matrix = [world.M11, world.M12, 0, world.M21, world.M22, 0, world.M31, world.M32, 1];
        _gl.UniformMatrix3(_mesh.Uniform("uMatrix"), 1, false, matrix);
        _gl.Uniform2(_mesh.Uniform("uViewport"), (float)width, height);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        _gl.BindVertexArray(graphic.Buffer.Vao);
        foreach (var part in graphic.Parts)
        {
            if (part.Indices.Length == 0) continue;
            var a = part.Alpha * alpha;
            if (part.Stops is { } stops)
            {
                _gl.Uniform1(_mesh.Uniform("uGradient"), 1);
                for (var index = 0; index < 4; index++) _stops[index] = stops[index];
                unsafe
                {
                    fixed (Vector3* data = _stops)
                        _gl.Uniform3(_mesh.Uniform("uStops"), 4, (float*)data);
                }
                _gl.Uniform1(_mesh.Uniform("uAlpha"), a);
            }
            else
            {
                _gl.Uniform1(_mesh.Uniform("uGradient"), 0);
                var rgb = part.Color.ToVector3() * a;
                _gl.Uniform4(_mesh.Uniform("uColor"), rgb.X, rgb.Y, rgb.Z, a);
            }
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)part.Indices.Length, DrawElementsType.UnsignedInt,
                    (void*)(part.IndexOffset * sizeof(uint)));
            }
            DrawCalls++;
        }
        _gl.BindVertexArray(0);
    }

    private void DrawText(TemperaText text, Matrix3x2 world, float alpha, float rasterScale)
    {
        if (text.Glyph is not { } glyph || glyph.Texture.IsDisposed || text.GlyphScale != rasterScale)
        {
            glyph = TemperaGlyphs.Get(_textures, text.Text, text.FontFamily, text.FontWeight, text.FontSize, rasterScale, text.WrapWidth);
            text.Glyph = glyph;
            text.GlyphScale = rasterScale;
        }
        _textures.Touch(glyph.Texture);
        var transform = Matrix3x2.CreateTranslation(-glyph.Center) * world;
        _primitives.DrawTexture(glyph.Texture, transform, glyph.Texture.LogicalSize, alpha, EffectBlendMode.Alpha, text.Color.ToEffect());
    }

    // ---- The difference filter ------------------------------------------------------------------------------------------

    private void CollectTexts(TemperaGroup group, Matrix3x2 parent, List<(TemperaText Text, Matrix3x2 World)> output)
    {
        foreach (var child in group.Children)
        {
            if (!child.Visible) continue;
            var world = child.LocalMatrix * parent;
            if (child is TemperaGroup inner) CollectTexts(inner, world, output);
            else if (child is TemperaText text) output.Add((text, world));
        }
    }

    private readonly List<(TemperaText Text, Matrix3x2 World)> _layerTexts = [];

    private void DrawInvertLayer(TemperaInvertLayer layer, TemperaSurface surface, Matrix3x2 world, double alpha)
    {
        // Bounds of the visible children, like getFastGlobalBounds: alpha does not shrink them.
        _layerTexts.Clear();
        CollectTexts(layer, world, _layerTexts);
        if (_layerTexts.Count == 0) return;
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var (text, textWorld) in _layerTexts)
        {
            var box = text.BoxSize ??= TemperaGlyphs.Box(text.Text, text.FontFamily, text.FontWeight, text.FontSize, text.WrapWidth);
            Span<Vector2> corners = [new(-box.X / 2, -box.Y / 2), new(box.X / 2, -box.Y / 2), new(box.X / 2, box.Y / 2), new(-box.X / 2, box.Y / 2)];
            foreach (var corner in corners)
            {
                var point = Vector2.Transform(corner, textWorld);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }
        var bounds = TemperaPixelRect.FromBounds(minX, minY, maxX, maxY, surface.Width, surface.Height);
        if (bounds.IsEmpty) return;

        _primitives.Flush();
        var back = layer.Inversion ? surface.Resolve() : 0u;

        _text.EnsureSize(surface.Width, surface.Height);
        _text.Bind();
        var glBounds = bounds.ToGl(surface.Height);
        _gl.Enable(EnableCap.ScissorTest);
        _gl.Scissor((int)glBounds.X, (int)glBounds.Y, (uint)glBounds.Z, (uint)glBounds.W);
        _gl.ClearColor(0, 0, 0, 0);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        foreach (var child in layer.Children) DrawNode(child, surface, world, alpha);
        _primitives.Flush();
        _gl.Disable(EnableCap.ScissorTest);

        surface.Bind();
        _invert.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _text.Texture);
        _gl.Uniform1(_invert.Uniform("uText"), 0);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, back);
        _gl.Uniform1(_invert.Uniform("uBack"), 1);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.Uniform2(_invert.Uniform("uSize"), (float)surface.Width, surface.Height);
        _gl.Uniform2(_invert.Uniform("uOutSize"), (float)surface.Width, surface.Height);
        _gl.Uniform4(_invert.Uniform("uOutFrame"), glBounds);
        _gl.Uniform1(_invert.Uniform("uPixelScale"), surface.Scale);
        var ink = layer.Ink.ToVector3();
        var paper = layer.Paper.ToVector3();
        _gl.Uniform3(_invert.Uniform("uInkColor"), ink);
        _gl.Uniform3(_invert.Uniform("uPaperColor"), paper);
        _gl.Uniform1(_invert.Uniform("uInkLuminance"), Luminance(ink));
        _gl.Uniform1(_invert.Uniform("uPaperLuminance"), Luminance(paper));
        _gl.Uniform1(_invert.Uniform("uBias"), 0f);
        var tint = layer.Tint is { Length: >= 2 } stops ? stops : null;
        for (var index = 0; index < 4; index++)
            _stops[index] = tint is null ? ink : tint[Math.Min(index, tint.Length - 1)].ToVector3();
        unsafe
        {
            fixed (Vector3* data = _stops)
                _gl.Uniform3(_invert.Uniform("uTint"), 4, (float*)data);
        }
        _gl.Uniform1(_invert.Uniform("uTintAmount"), tint is null ? 0f : 1f);
        _gl.Uniform1(_invert.Uniform("uInversion"), layer.Inversion ? 1 : 0);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        DrawQuad();
        _primitives.SetViewport(new Vector2(surface.Width, surface.Height));
    }

    private static float Luminance(Vector3 rgb) => rgb.X * 0.2126f + rgb.Y * 0.7152f + rgb.Z * 0.0722f;

    private void DrawQuad()
    {
        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
        DrawCalls++;
    }

    // ---- Scene post chain -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs <paramref name="passes"/> over <paramref name="frame"/> of <paramref name="source"/> (its resolved texture) and blends
    /// the last one onto <paramref name="target"/>. Intermediate passes overwrite, like Pixi's flip/flop filter textures.
    /// </summary>
    public void RunChain(TemperaSurface source, TemperaPixelRect frame, IReadOnlyList<TemperaPass> passes, TemperaSurface target)
    {
        _primitives.Flush();
        var input = source.Resolve();
        var inputWidth = source.Width;
        var inputHeight = source.Height;
        var inputFrame = frame.ToGl(source.Height);
        var outputRect = frame.Scaled(target.Width / (double)source.Width, target.Width, target.Height);
        if (passes.Count == 0) passes = [new TemperaPass(TemperaPassKind.Lens, 0)];
        var flip = _ping;
        for (var index = 0; index < passes.Count; index++)
        {
            var last = index == passes.Count - 1;
            Vector4 outFrame;
            int outWidth, outHeight;
            if (last)
            {
                target.Bind();
                outFrame = outputRect.ToGl(target.Height);
                outWidth = target.Width;
                outHeight = target.Height;
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            }
            else
            {
                flip.EnsureSize(inputWidth, inputHeight);
                flip.Bind();
                outFrame = inputFrame;
                outWidth = inputWidth;
                outHeight = inputHeight;
                _gl.Disable(EnableCap.Blend);
                _gl.Enable(EnableCap.ScissorTest);
                _gl.Scissor((int)inputFrame.X, (int)inputFrame.Y, (uint)inputFrame.Z, (uint)inputFrame.W);
                _gl.ClearColor(0, 0, 0, 0);
                _gl.Clear(ClearBufferMask.ColorBufferBit);
                _gl.Disable(EnableCap.ScissorTest);
            }
            var pass = passes[index];
            var shader = pass.Amount == 0 && pass.Kind == TemperaPassKind.Lens ? _copy : _passes[pass.Kind];
            shader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, input);
            _gl.Uniform1(shader.Uniform("uTexture"), 0);
            _gl.Uniform2(shader.Uniform("uInputSize"), (float)inputWidth, inputHeight);
            _gl.Uniform4(shader.Uniform("uFrame"), inputFrame);
            _gl.Uniform4(shader.Uniform("uOutFrame"), outFrame);
            _gl.Uniform2(shader.Uniform("uOutSize"), (float)outWidth, outHeight);
            _gl.Uniform1(shader.Uniform("uPixelScale"), source.Scale);
            switch (pass.Kind)
            {
                case TemperaPassKind.Lens:
                    _gl.Uniform1(shader.Uniform("uDistortion"), pass.Amount);
                    _gl.Uniform1(shader.Uniform("uDispersion"), 0f);
                    break;
                case TemperaPassKind.Noise:
                    _gl.Uniform1(shader.Uniform("uNoise"), pass.Amount);
                    _gl.Uniform1(shader.Uniform("uSeed"), pass.Seed);
                    break;
                case TemperaPassKind.Contrast:
                    _gl.Uniform1(shader.Uniform("uContrast"), pass.Amount);
                    break;
                case TemperaPassKind.RgbShift:
                case TemperaPassKind.Vignette:
                    _gl.Uniform1(shader.Uniform("uAmount"), pass.Amount);
                    break;
                case TemperaPassKind.BlurX:
                case TemperaPassKind.BlurY:
                    _gl.Uniform2(shader.Uniform("uDirection"), pass.Kind == TemperaPassKind.BlurX ? 1f : 0f,
                        pass.Kind == TemperaPassKind.BlurY ? 1f : 0f);
                    _gl.Uniform1(shader.Uniform("uStrength"), pass.Amount);
                    break;
            }
            DrawQuad();
            if (!last)
            {
                input = flip.Texture;
                flip = flip == _ping ? _pong : _ping;
            }
        }
        _gl.Enable(EnableCap.Blend);
        _primitives.SetViewport(new Vector2(target.Width, target.Height));
    }

    /// <summary>Blends a resolved surface onto whatever framebuffer is bound (the control's output).</summary>
    public void Present(TemperaSurface surface, int width, int height)
    {
        _copy.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, surface.ResolvedTexture);
        _gl.Uniform1(_copy.Uniform("uTexture"), 0);
        _gl.Uniform2(_copy.Uniform("uInputSize"), (float)surface.Width, surface.Height);
        _gl.Uniform4(_copy.Uniform("uFrame"), new Vector4(0, 0, surface.Width, surface.Height));
        _gl.Uniform4(_copy.Uniform("uOutFrame"), new Vector4(0, 0, width, height));
        _gl.Uniform2(_copy.Uniform("uOutSize"), (float)width, height);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        DrawQuad();
    }

    public void Dispose()
    {
        _mesh.Dispose();
        _invert.Dispose();
        _copy.Dispose();
        foreach (var shader in _passes.Values.Distinct()) shader.Dispose();
        _gl.DeleteVertexArray(_quadVao);
        _gl.DeleteBuffer(_quadVbo);
        _text.Dispose();
        _ping.Dispose();
        _pong.Dispose();
    }
}
