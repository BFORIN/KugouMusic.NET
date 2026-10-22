using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects.Lumiere.Light;

/// <summary>
/// The runtime's layer frame (Folia lumiereSceneEntry.ts applyLumiereLayerFrame): a paragraph scene — or the credits card —
/// rendered into its own target, then composited with alpha, a scale about the frame center and an optional blur. The blur
/// stands in for Pixi's BlurFilter (quality 3, half resolution): a separable Gaussian at half resolution whose sigma follows
/// the strength, so it reads the same without matching Pixi's pass structure tap for tap.
/// </summary>
internal sealed class LumiereCompositor : IDisposable
{
    private const string Vertex = """
        #version 330 core
        out vec2 vUv;
        void main() {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            vUv = p;
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    private const string Blit = """
        #version 330 core
        in vec2 vUv;
        out vec4 finalColor;
        uniform sampler2D uTexture;
        uniform vec4 uMap;      // uv scale xy, offset zw
        uniform float uAlpha;
        void main() {
            vec2 uv = vUv * uMap.xy + uMap.zw;
            if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) { finalColor = vec4(0.0); return; }
            finalColor = texture(uTexture, uv) * uAlpha;
        }
        """;

    private const string Gauss = """
        #version 330 core
        in vec2 vUv;
        out vec4 finalColor;
        uniform sampler2D uTexture;
        uniform vec2 uStep;     // one sigma along the axis, in uv
        void main() {
            // 9 taps at 0, ±0.75σ, ±1.5σ, ±2.25σ, ±3σ with Gaussian weights.
            float w[5] = float[](0.1784, 0.1543, 0.1006, 0.0494, 0.0182);
            vec4 sum = texture(uTexture, vUv) * w[0];
            for (int i = 1; i < 5; i++) {
                vec2 o = uStep * (0.75 * float(i));
                sum += (texture(uTexture, vUv + o) + texture(uTexture, vUv - o)) * w[i];
            }
            finalColor = sum / (w[0] + 2.0 * (w[1] + w[2] + w[3] + w[4]));
        }
        """;

    private readonly GpuDevice _gpu;
    private readonly GpuShader _blit;
    private readonly GpuShader _gauss;

    public LumiereCompositor(GpuDevice gpu)
    {
        _gpu = gpu;
        _blit = gpu.CreateShader(Vertex, Blit, "lumiere-layer-blit");
        _gauss = gpu.CreateShader(Vertex, Gauss, "lumiere-layer-blur");
        Layer = gpu.CreateRenderTarget(depthStencil: false);
        _blurA = gpu.CreateRenderTarget(depthStencil: false);
        _blurB = gpu.CreateRenderTarget(depthStencil: false);
    }

    public GpuRenderTarget Layer { get; }
    public int DrawCalls { get; private set; }
    private readonly GpuRenderTarget _blurA;
    private readonly GpuRenderTarget _blurB;

    public void Composite(LumiereBinding output, float alpha, float scale, float blurPixels, float offsetY = 0)
    {
        using var timing = _gpu.Resources.MeasurePostProcess();
        DrawCalls = 0;
        var gpu = _gpu;
        var source = Layer.Texture!;
        if (blurPixels > 0.3f)
        {
            _blurA.EnsureSize((Layer.Width + 1) / 2, (Layer.Height + 1) / 2);
            _blurB.EnsureSize(_blurA.Width, _blurA.Height);
            gpu.SetBlend(GpuBlend.None);
            gpu.SetTarget(_blurA);
            _blit.Set("uMap", 1f, 1f, 0f, 0f);
            _blit.Set("uAlpha", 1f);
            _blit.SetTexture("uTexture", Layer.Texture);
            gpu.DrawFullscreen(_blit);
            DrawCalls++;
            var sigma = blurPixels * 0.5f * 0.5f;
            gpu.SetTarget(_blurB);
            _gauss.Set("uStep", sigma / _blurA.Width, 0f);
            _gauss.SetTexture("uTexture", _blurA.Texture);
            gpu.DrawFullscreen(_gauss);
            DrawCalls++;
            gpu.SetTarget(_blurA);
            _gauss.Set("uStep", 0f, sigma / _blurA.Height);
            _gauss.SetTexture("uTexture", _blurB.Texture);
            gpu.DrawFullscreen(_gauss);
            DrawCalls++;
            source = _blurA.Texture!;
        }
        output.Restore(gpu);
        gpu.SetBlend(GpuBlend.Alpha);
        var inverse = 1 / MathF.Max(scale, 1e-3f);
        _blit.Set("uMap", inverse, inverse, 0.5f - 0.5f * inverse, 0.5f - 0.5f * inverse + offsetY * inverse);
        _blit.Set("uAlpha", alpha);
        _blit.SetTexture("uTexture", source);
        gpu.DrawFullscreen(_blit);
        DrawCalls++;
    }

    public void Dispose()
    {
        Layer.Dispose();
        _blurA.Dispose();
        _blurB.Dispose();
        _blit.Dispose();
        _gauss.Dispose();
    }
}
