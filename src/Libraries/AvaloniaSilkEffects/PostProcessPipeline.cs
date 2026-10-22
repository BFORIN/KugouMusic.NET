using System.Numerics;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects;

internal sealed class PostProcessPipeline : IDisposable
{
    private const string FullscreenVertex = """
        #version 330 core
        out vec2 vUv;
        void main() {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            vUv = p;
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    // Dual-filter (Bjørge 2015) downsample: a 5-tap box around the half-texel
    // corners. The first bloom level also applies a soft-knee brightness gate.
    private const string DownFragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTexture;
        uniform vec2 uHalfTexel;
        uniform int uPrefilter;
        uniform float uThreshold;
        uniform float uKnee;
        out vec4 finalColor;
        // Gate each tap before averaging: thin strokes are only a texel or two
        // wide and would fall under the threshold once mixed with the backdrop.
        vec4 tap(vec2 uv) {
            vec4 c = texture(uTexture, uv);
            if (uPrefilter == 0) return c;
            float brightness = max(c.r, max(c.g, c.b));
            float soft = clamp(brightness - uThreshold + uKnee, 0.0, 2.0 * uKnee);
            soft = soft * soft / (4.0 * uKnee + 1e-4);
            float contribution = max(soft, brightness - uThreshold) / max(brightness, 1e-4);
            return vec4(c.rgb * contribution, 0.0);
        }
        void main() {
            vec4 c = tap(vUv) * 4.0;
            c += tap(vUv - uHalfTexel);
            c += tap(vUv + uHalfTexel);
            c += tap(vUv + vec2(uHalfTexel.x, -uHalfTexel.y));
            c += tap(vUv - vec2(uHalfTexel.x, -uHalfTexel.y));
            c *= 0.125;
            finalColor = c;
        }
        """;

    // Dual-filter upsample: an 8-tap tent from the coarser level, blended with
    // the same-resolution down level so every octave contributes (uScatter < 1).
    private const string UpFragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTexture;
        uniform sampler2D uBase;
        uniform vec2 uHalfTexel;
        uniform float uSpread;
        uniform float uScatter;
        out vec4 finalColor;
        void main() {
            vec2 h = uHalfTexel * uSpread;
            vec4 c = texture(uTexture, vUv + vec2(-h.x * 2.0, 0.0));
            c += texture(uTexture, vUv + vec2(-h.x, h.y)) * 2.0;
            c += texture(uTexture, vUv + vec2(0.0, h.y * 2.0));
            c += texture(uTexture, vUv + vec2(h.x, h.y)) * 2.0;
            c += texture(uTexture, vUv + vec2(h.x * 2.0, 0.0));
            c += texture(uTexture, vUv + vec2(h.x, -h.y)) * 2.0;
            c += texture(uTexture, vUv + vec2(0.0, -h.y * 2.0));
            c += texture(uTexture, vUv + vec2(-h.x, -h.y)) * 2.0;
            c /= 12.0;
            finalColor = mix(texture(uBase, vUv), c, uScatter);
        }
        """;

    // Separable horizontal gaussian used to stretch bloom into an anamorphic streak.
    private const string StreakFragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTexture;
        uniform vec2 uStep;
        out vec4 finalColor;
        void main() {
            vec4 c = texture(uTexture, vUv) * 0.2270270;
            c += (texture(uTexture, vUv + uStep * 1.3846154) + texture(uTexture, vUv - uStep * 1.3846154)) * 0.3162162;
            c += (texture(uTexture, vUv + uStep * 3.2307692) + texture(uTexture, vUv - uStep * 3.2307692)) * 0.0702703;
            finalColor = c;
        }
        """;

    private const string CompositeFragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uSource;
        uniform sampler2D uBlurred;
        uniform sampler2D uBloomTexture;
        uniform sampler2D uStreakTexture;
        uniform vec2 uResolution;
        uniform float uBloom;
        uniform vec3 uBloomTint;
        uniform float uStreak;
        uniform float uZoomBlur;
        uniform vec2 uZoomCenter;
        uniform float uBlur;
        uniform float uGlow;
        uniform float uGrain;
        uniform float uContrast;
        uniform float uRgbSplit;
        uniform float uHalftone;
        uniform float uVignette;
        uniform float uLensDistortion;
        uniform float uLensDispersion;
        uniform float uGlitch;
        uniform int uMonoGlitch;
        uniform float uTime;
        uniform float uSeed;
        uniform mat4 uColorMatrix;
        out vec4 finalColor;

        float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233)) + uSeed) * 43758.5453); }

        float glitchHash(vec2 value) { return fract(sin(dot(value, vec2(12.9898, 78.233))) * 43758.5453); }

        vec2 distort(vec2 uv) {
            vec2 p = uv * 2.0 - 1.0;
            float r2 = dot(p, p);
            p *= 1.0 + r2 * uLensDistortion * 0.18;
            return p * 0.5 + 0.5;
        }

        void main() {
            vec2 uv = distort(vUv);
            float glitchGain = 0.0;
            if (uMonoGlitch == 1) {
                // Folia sonnetGlitchFilter: coarse and fine horizontal slices picked by the
                // transition's stepped seed, plus equal-channel brightness tears (no RGB split).
                // Pixi filter coordinates run top-down.
                float y = 1.0 - uv.y;
                float coarseBand = floor(y * 26.0);
                float fineBand = floor(y * 110.0);
                float coarseNoise = glitchHash(vec2(coarseBand, uSeed));
                float fineNoise = glitchHash(vec2(fineBand + 41.0, uSeed * 1.37));
                float coarseGate = step(0.58, coarseNoise);
                float fineGate = step(0.88, fineNoise);
                uv.x += (glitchHash(vec2(coarseBand + 17.0, uSeed)) * 2.0 - 1.0) * coarseGate * uGlitch * 0.095
                    + (glitchHash(vec2(fineBand + 73.0, uSeed)) * 2.0 - 1.0) * fineGate * uGlitch * 0.035;
                glitchGain = (coarseGate * (coarseNoise - 0.58) + fineGate * 0.12) * uGlitch * 0.42;
            } else {
                float band = floor(uv.y * 48.0);
                uv.x += step(0.72, hash(vec2(band, floor(uTime * 18.0)))) *
                    (hash(vec2(band + 17.0, uSeed)) * 2.0 - 1.0) * uGlitch * 0.075;
            }
            vec2 dispersion = vec2((uRgbSplit + uLensDispersion) * 0.006, 0.0);
            vec4 color;
            if (uZoomBlur > 0.001) {
                // Radial smear toward the focus point; channels sample slightly
                // different lengths so the streaks pick up a faint prism fringe.
                vec4 sum = vec4(0.0);
                float total = 0.0;
                vec2 ray = uv - uZoomCenter;
                for (int i = 0; i < 16; i++) {
                    float t = float(i) / 15.0;
                    float w = 1.0 - t * 0.6;
                    float k = 1.0 - uZoomBlur * 0.12 * t;
                    vec4 s = texture(uSource, clamp(uZoomCenter + ray * k, 0.0, 1.0));
                    s.r = texture(uSource, clamp(uZoomCenter + ray * (k + uZoomBlur * 0.004 * t), 0.0, 1.0)).r;
                    sum += s * w;
                    total += w;
                }
                color = sum / total;
            } else {
                color = texture(uSource, clamp(uv, 0.0, 1.0));
                color.r = texture(uSource, clamp(uv + dispersion, 0.0, 1.0)).r;
                color.b = texture(uSource, clamp(uv - dispersion, 0.0, 1.0)).b;
            }
            color.rgb *= 1.0 + glitchGain;
            vec4 blurred = texture(uBlurred, clamp(uv, 0.0, 1.0));
            color = mix(color, blurred, clamp(uBlur, 0.0, 1.0));
            color.rgb += blurred.rgb * max(0.0, uGlow);
            if (uBloom > 0.0 || uStreak > 0.0) {
                vec3 light = texture(uBloomTexture, clamp(uv, 0.0, 1.0)).rgb * uBloom +
                    texture(uStreakTexture, clamp(uv, 0.0, 1.0)).rgb * uStreak;
                light *= uBloomTint;
                // Light is emitted, so it also covers a transparent clear; keep the
                // output a valid premultiplied color by raising alpha with it.
                color.rgb += light;
                color.a = max(color.a, min(1.0, max(light.r, max(light.g, light.b))));
            }
            color = uColorMatrix * color;
            color.rgb = (color.rgb - 0.5 * color.a) * (1.0 + uContrast) + 0.5 * color.a;
            float dots = sin(uv.x * uResolution.x * 0.42) * sin(uv.y * uResolution.y * 0.42);
            color.rgb *= 1.0 - uHalftone * (0.08 + 0.08 * dots);
            float noise = hash(gl_FragCoord.xy + floor(uTime * 60.0)) - 0.5;
            color.rgb += noise * uGrain * 0.12 * color.a;
            vec2 edge = abs(vUv * 2.0 - 1.0);
            float vignette = smoothstep(0.45, 1.18, length(edge));
            color.rgb *= 1.0 - vignette * uVignette * 0.72;
            color = clamp(color, 0.0, 1.0);
            // Fringes and channel splits can push one channel past the coverage
            // they were sampled from; keep the output valid premultiplied color.
            color.rgb = min(color.rgb, vec3(color.a));
            finalColor = color;
        }
        """;

    private readonly GpuDevice _device;
    private readonly GpuRenderTarget _source;
    private readonly GpuRenderTarget _multisampleSource;
    private readonly int _maxSamples;
    private const int BloomLevels = 6;
    private const int BlurLevels = 4;
    private readonly GpuRenderTarget[] _bloomDown;
    private readonly GpuRenderTarget[] _bloomUp;
    private readonly GpuRenderTarget[] _blurDown;
    private readonly GpuRenderTarget[] _blurUp;
    private readonly GpuRenderTarget _streakPing;
    private readonly GpuRenderTarget _streakPong;
    private readonly GpuShader _downShader;
    private readonly GpuShader _upShader;
    private readonly GpuShader _streakShader;
    private readonly GpuShader _compositeShader;
    private GpuRenderTarget? _sonnetPing;
    private GpuRenderTarget? _sonnetPong;
    private GpuShader? _sonnetShader;
    private bool _usesOffscreen;

    public int FrameDrawCalls { get; private set; }
    public int MultisampleCount { get; private set; } = 1;

    public PostProcessPipeline(GpuDevice device)
    {
        _device = device;
        _source = device.CreateRenderTarget(depthStencil: true);
        _multisampleSource = device.CreateRenderTarget(depthStencil: true);
        _maxSamples = device.MaxSamples;
        _bloomDown = CreateChain(device, BloomLevels);
        _bloomUp = CreateChain(device, BloomLevels);
        _blurDown = CreateChain(device, BlurLevels);
        _blurUp = CreateChain(device, BlurLevels);
        _streakPing = device.CreateRenderTarget(depthStencil: true);
        _streakPong = device.CreateRenderTarget(depthStencil: true);
        _downShader = device.CreateShader(FullscreenVertex, DownFragment, "effects-dual-down");
        _upShader = device.CreateShader(FullscreenVertex, UpFragment, "effects-dual-up");
        _streakShader = device.CreateShader(FullscreenVertex, StreakFragment, "effects-streak");
        _compositeShader = device.CreateShader(FullscreenVertex, CompositeFragment, "effects-composite");
    }

    /// <summary>Binds the scene's render target (the output, or an offscreen source) and clears it.</summary>
    public void Begin(
        int width,
        int height,
        EffectColor clearColor,
        bool enabled,
        float resolutionScale,
        int multisampleCount = 1)
    {
        FrameDrawCalls = 0;
        var samples = Math.Clamp(multisampleCount, 1, Math.Max(1, _maxSamples));
        _usesOffscreen = enabled || samples > 1;
        MultisampleCount = 1;
        var renderWidth = width;
        var renderHeight = height;
        if (_usesOffscreen)
        {
            var scale = enabled ? Math.Clamp(resolutionScale, 0.25f, 1f) : 1;
            renderWidth = Math.Max(1, (int)MathF.Ceiling(width * scale));
            renderHeight = Math.Max(1, (int)MathF.Ceiling(height * scale));
            _source.EnsureSize(renderWidth, renderHeight);
        }
        if (samples > 1)
        {
            _multisampleSource.EnsureSize(renderWidth, renderHeight, samples);
            MultisampleCount = _multisampleSource.Samples;
        }
        else
            _multisampleSource.Release();
        if (samples > 1) _device.SetTarget(_multisampleSource);
        else if (_usesOffscreen) _device.SetTarget(_source);
        _device.SetViewport(new GpuRect(0, 0, renderWidth, renderHeight));
        _device.Clear(clearColor.Premultiplied(), stencil: true);
    }

    public void End(GpuRenderTarget output, PostProcessSettings settings)
    {
        if (!_usesOffscreen)
            return;

        var device = _device;
        device.SetBlend(GpuBlend.None);
        device.SetScissor(null);
        device.SetStencil(GpuStencil.Disabled);
        device.SetColorWrite(true);
        // Resolve geometry before any filter samples it. Fullscreen filters
        // then preserve coverage, including premultiplied RGB and alpha.
        if (MultisampleCount > 1)
            device.Resolve(_multisampleSource, _source);

        var sourceTexture = _source.Texture!;
        if (settings.UseSonnetPasses)
        {
            _sonnetShader ??= device.CreateShader(FullscreenVertex, Sonnet.SonnetFilterShader.Fragment, "sonnet-material");
            _sonnetPing ??= device.CreateRenderTarget(depthStencil: true);
            _sonnetPong ??= device.CreateRenderTarget(depthStencil: true);
            ReadOnlySpan<float> amounts = [settings.LensDistortion, settings.Grain, settings.Contrast,
                settings.RgbSplit, settings.Halftone, settings.Vignette];
            var count = 0;
            for (var pass = 0; pass < amounts.Length; pass++)
            {
                if (amounts[pass] <= 0 && !(pass == 0 && settings.LensDispersion > 0)) continue;
                var destination = count++ % 2 == 0 ? _sonnetPing : _sonnetPong;
                destination.EnsureSize(_source.Width, _source.Height);
                device.SetTarget(destination);
                _sonnetShader.SetTexture("uTexture", sourceTexture);
                _sonnetShader.Set("uResolution", (float)_source.Width, (float)_source.Height);
                _sonnetShader.Set("uPass", pass);
                _sonnetShader.Set("uAmount", amounts[pass]);
                _sonnetShader.Set("uDispersion", settings.LensDispersion);
                _sonnetShader.Set("uSeed", settings.SonnetNoiseSeed);
                device.DrawFullscreen(_sonnetShader);
                FrameDrawCalls++;
                sourceTexture = destination.Texture!;
            }
        }

        var blurEnabled = settings.Blur > 0.001f || settings.Glow > 0.001f;
        var blurredTexture = sourceTexture;
        if (blurEnabled)
        {
            // Wider transitions open the tent further instead of switching octave
            // counts, so an animated blur amount never pops between radii.
            var spread = Math.Clamp(0.6f + settings.Blur * 0.9f + settings.Glow * 0.4f, 0.6f, 1.5f);
            blurredTexture = DualFilter(sourceTexture, _source.Width, _source.Height, _blurDown, _blurUp,
                BlurLevels, prefilter: false, threshold: 0, spread, scatter: 1);
        }

        var bloomEnabled = settings.Bloom > 0.001f || settings.BloomStreak > 0.001f;
        var bloomTexture = sourceTexture;
        var streakTexture = sourceTexture;
        if (bloomEnabled)
        {
            var radius = Math.Clamp(settings.BloomRadius, 0, 1);
            bloomTexture = DualFilter(sourceTexture, _source.Width, _source.Height, _bloomDown, _bloomUp,
                BloomLevels, prefilter: true, Math.Clamp(settings.BloomThreshold, 0, 1),
                spread: 1, scatter: 0.45f + radius * 0.45f);
            if (settings.BloomStreak > 0.001f && _bloomDown[1].IsAllocated)
                streakTexture = Streak(_bloomDown[1]);
        }

        device.SetTarget(output);
        var shader = _compositeShader;
        shader.SetTexture("uSource", sourceTexture);
        shader.SetTexture("uBlurred", blurredTexture);
        shader.SetTexture("uBloomTexture", bloomTexture);
        shader.SetTexture("uStreakTexture", streakTexture);
        shader.Set("uBloom", bloomEnabled ? settings.Bloom : 0);
        shader.Set("uStreak", bloomEnabled && settings.BloomStreak > 0.001f ? settings.BloomStreak : 0);
        shader.Set("uBloomTint", settings.BloomTint.R, settings.BloomTint.G, settings.BloomTint.B);
        shader.Set("uZoomBlur", Math.Clamp(settings.ZoomBlur, 0, 2));
        shader.Set("uZoomCenter", settings.ZoomBlurCenter.X, 1 - settings.ZoomBlurCenter.Y);
        shader.Set("uResolution", (float)_source.Width, (float)_source.Height);
        shader.Set("uBlur", settings.Blur);
        shader.Set("uGlow", settings.Glow);
        shader.Set("uGrain", settings.UseSonnetPasses ? 0 : settings.Grain);
        shader.Set("uContrast", settings.UseSonnetPasses ? 0 : settings.Contrast);
        shader.Set("uRgbSplit", settings.UseSonnetPasses ? 0 : settings.RgbSplit);
        shader.Set("uHalftone", settings.UseSonnetPasses ? 0 : settings.Halftone);
        shader.Set("uVignette", settings.UseSonnetPasses ? 0 : settings.Vignette);
        shader.Set("uLensDistortion", settings.UseSonnetPasses ? 0 : settings.LensDistortion);
        shader.Set("uLensDispersion", settings.UseSonnetPasses ? 0 : settings.LensDispersion);
        shader.Set("uGlitch", settings.Glitch);
        shader.Set("uMonoGlitch", settings.UseSonnetPasses ? 1 : 0);
        shader.Set("uTime", settings.Time);
        shader.Set("uSeed", settings.Seed);
        shader.SetMatrixTransposed("uColorMatrix", settings.ColorMatrix);
        device.DrawFullscreen(shader);
        FrameDrawCalls++;
    }

    private static GpuRenderTarget[] CreateChain(GpuDevice device, int levels)
    {
        var chain = new GpuRenderTarget[levels];
        for (var index = 0; index < levels; index++)
            chain[index] = device.CreateRenderTarget(depthStencil: true);
        return chain;
    }

    /// <summary>
    /// Runs a dual-filter pyramid starting at half resolution and returns the
    /// texture of the finest upsampled level.
    /// </summary>
    private GpuTexture DualFilter(GpuTexture source, int width, int height, GpuRenderTarget[] down, GpuRenderTarget[] up,
        int levels, bool prefilter, float threshold, float spread, float scatter)
    {
        var input = source;
        var inputWidth = width;
        var inputHeight = height;
        var used = 0;
        _downShader.Set("uThreshold", threshold);
        _downShader.Set("uKnee", MathF.Max(0.04f, (1 - threshold) * 0.5f));
        for (var level = 0; level < levels; level++)
        {
            var levelWidth = Math.Max(1, inputWidth / 2);
            var levelHeight = Math.Max(1, inputHeight / 2);
            down[level].EnsureSize(levelWidth, levelHeight);
            _device.SetTarget(down[level]);
            _downShader.SetTexture("uTexture", input);
            _downShader.Set("uHalfTexel", 0.5f / inputWidth, 0.5f / inputHeight);
            _downShader.Set("uPrefilter", prefilter && level == 0 ? 1 : 0);
            _device.DrawFullscreen(_downShader);
            FrameDrawCalls++;
            input = down[level].Texture!;
            inputWidth = levelWidth;
            inputHeight = levelHeight;
            used++;
            if (levelWidth <= 8 || levelHeight <= 8) break;
        }

        _upShader.Set("uSpread", spread);
        _upShader.Set("uScatter", scatter);
        var lower = down[used - 1];
        for (var level = used - 2; level >= 0; level--)
        {
            var target = up[level];
            target.EnsureSize(down[level].Width, down[level].Height);
            _device.SetTarget(target);
            _upShader.SetTexture("uTexture", lower.Texture);
            _upShader.SetTexture("uBase", down[level].Texture);
            _upShader.Set("uHalfTexel", 0.5f / lower.Width, 0.5f / lower.Height);
            _device.DrawFullscreen(_upShader);
            FrameDrawCalls++;
            lower = target;
        }
        return lower.Texture!;
    }

    private GpuTexture Streak(GpuRenderTarget bright)
    {
        // Squash vertically so a few horizontal taps cover a wide, thin flare.
        var width = Math.Max(1, bright.Width / 2);
        var height = Math.Max(1, bright.Height / 6);
        _streakPing.EnsureSize(width, height);
        _streakPong.EnsureSize(width, height);
        var input = bright.Texture!;
        ReadOnlySpan<float> steps = [1, 3, 9, 27];
        for (var pass = 0; pass < steps.Length; pass++)
        {
            var target = pass % 2 == 0 ? _streakPing : _streakPong;
            _device.SetTarget(target);
            _streakShader.SetTexture("uTexture", input);
            _streakShader.Set("uStep", steps[pass] / width, 0f);
            _device.DrawFullscreen(_streakShader);
            FrameDrawCalls++;
            input = target.Texture!;
        }
        return input;
    }

    public void Dispose()
    {
        _sonnetShader?.Dispose();
        _sonnetPing?.Dispose();
        _sonnetPong?.Dispose();
        _downShader.Dispose();
        _upShader.Dispose();
        _streakShader.Dispose();
        _compositeShader.Dispose();
        _source.Dispose();
        _multisampleSource.Dispose();
        foreach (var target in _bloomDown.Concat(_bloomUp).Concat(_blurDown).Concat(_blurUp))
            target.Dispose();
        _streakPing.Dispose();
        _streakPong.Dispose();
    }
}
