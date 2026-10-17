namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>
/// GLSL for the Tempera renderer. Pass shaders run in the coordinate frame Pixi filters use: <c>uFrame</c> is the filter's
/// bounds in the input texture (GL pixels), <c>uOutFrame</c> the same bounds in the output, and every distance a Pixi filter
/// expresses through <c>uInputSize.zw</c> is a CSS (logical) pixel, scaled here by <c>uPixelScale</c>.
/// </summary>
internal static class TemperaShaders
{
    public const string MeshVertex = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in float aRamp;
        uniform mat3 uMatrix;
        uniform vec2 uViewport;
        out float vRamp;
        void main() {
            vec3 p = uMatrix * vec3(aPosition, 1.0);
            gl_Position = vec4(p.x * 2.0 / uViewport.x - 1.0, 1.0 - p.y * 2.0 / uViewport.y, 0.0, 1.0);
            vRamp = aRamp;
        }
        """;

    public const string MeshFragment = """
        #version 330 core
        in float vRamp;
        uniform vec4 uColor;
        uniform int uGradient;
        uniform vec3 uStops[4];
        uniform float uAlpha;
        out vec4 finalColor;
        void main() {
            if (uGradient == 1) {
                float s = clamp(vRamp, 0.0, 1.0) * 3.0;
                vec3 c = s < 1.0 ? mix(uStops[0], uStops[1], s)
                    : s < 2.0 ? mix(uStops[1], uStops[2], s - 1.0)
                    : mix(uStops[2], uStops[3], s - 2.0);
                finalColor = vec4(c * uAlpha, uAlpha);
            } else {
                finalColor = uColor;
            }
        }
        """;

    /// <summary>A unit quad stretched over <c>uOutFrame</c> of an output of <c>uOutSize</c>.</summary>
    public const string PassVertex = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        uniform vec4 uOutFrame;
        uniform vec2 uOutSize;
        void main() {
            vec2 p = uOutFrame.xy + aPosition * uOutFrame.zw;
            gl_Position = vec4(p / uOutSize * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    private const string PassHead = """
        #version 330 core
        uniform sampler2D uTexture;
        uniform vec2 uInputSize;
        uniform vec4 uFrame;
        uniform vec4 uOutFrame;
        uniform float uPixelScale;
        out vec4 finalColor;
        vec2 frameUv() { return (gl_FragCoord.xy - uOutFrame.xy) / uOutFrame.zw; }
        vec2 toInput(vec2 rel) { return (uFrame.xy + rel * uFrame.zw) / uInputSize; }
        vec4 sampleInside(vec2 rel) {
            if (rel.x < 0.0 || rel.y < 0.0 || rel.x > 1.0 || rel.y > 1.0) return vec4(0.0);
            return texture(uTexture, toInput(rel));
        }
        vec4 sampleClamped(vec2 inputPx) {
            vec2 px = clamp(inputPx, uFrame.xy + 0.5, uFrame.xy + uFrame.zw - 0.5);
            return texture(uTexture, px / uInputSize);
        }
        """;

    public const string CopyFragment = PassHead + """
        void main() { finalColor = sampleInside(frameUv()); }
        """;

    /// <summary>Folia sonnetLensFilter.ts (dispersion fixed at 0 for Tempera).</summary>
    public const string LensFragment = PassHead + """
        uniform float uDistortion;
        uniform float uDispersion;
        void main() {
            vec2 screenUv = frameUv();
            vec2 centered = screenUv - 0.5;
            float aspect = uFrame.z / max(uFrame.w, 1.0);
            centered.x *= aspect;
            float radiusSquared = dot(centered, centered);
            float curvature = uDistortion * 0.32;
            float radialScale = 1.0 - curvature * radiusSquared + curvature * 0.16 * radiusSquared * radiusSquared;
            vec2 lensCentered = centered * radialScale;
            lensCentered.x /= aspect;
            vec2 lensUv = lensCentered + 0.5;
            float radius = sqrt(radiusSquared);
            vec2 radialDirection = radius > 0.0001 ? centered / radius : vec2(0.0);
            float edgeWeight = smoothstep(0.12, 0.9, radius);
            vec2 dispersion = radialDirection * uDispersion * 0.012 * edgeWeight;
            dispersion.x /= aspect;
            vec4 center = sampleInside(lensUv);
            vec4 redSample = sampleInside(lensUv + dispersion);
            vec4 blueSample = sampleInside(lensUv - dispersion);
            float alpha = max(center.a, max(redSample.a, blueSample.a));
            float coreWeight = 0.84 - clamp(uDispersion, 0.0, 1.0) * 0.18;
            vec3 core = center.rgb * coreWeight;
            vec3 separated = vec3(redSample.r, center.g, blueSample.b);
            finalColor = vec4(max(core, separated), alpha);
        }
        """;

    /// <summary>Pixi NoiseFilter: static grain keyed on the pass's own pixel grid.</summary>
    public const string NoiseFragment = PassHead + """
        uniform float uNoise;
        uniform float uSeed;
        float rand(vec2 co) { return fract(sin(dot(co.xy, vec2(12.9898, 78.233))) * 43758.5453); }
        void main() {
            vec4 color = sampleInside(frameUv());
            float randomValue = rand((gl_FragCoord.xy - uOutFrame.xy) * uSeed);
            float diff = (randomValue - 0.5) * uNoise;
            if (color.a > 0.0) color.rgb /= color.a;
            color.rgb += diff;
            color.rgb *= color.a;
            finalColor = color;
        }
        """;

    /// <summary>Pixi ColorMatrixFilter.contrast(amount, false).</summary>
    public const string ContrastFragment = PassHead + """
        uniform float uContrast;
        void main() {
            vec4 c = sampleInside(frameUv());
            if (c.a > 0.0) c.rgb /= c.a;
            float v = uContrast + 1.0;
            float o = -0.5 * (v - 1.0);
            vec3 result = c.rgb * v + o;
            finalColor = vec4(result * c.a, c.a);
        }
        """;

    /// <summary>Folia sonnetPrintFilters.ts rgbShiftFragment.</summary>
    public const string RgbShiftFragment = PassHead + """
        uniform float uAmount;
        void main() {
            vec2 rel = frameUv();
            vec2 offset = vec2(0.9063, 0.4226) * uAmount * 3.0 * uPixelScale / uFrame.zw;
            vec4 redSample = sampleInside(rel + offset);
            vec4 center = sampleInside(rel);
            vec4 blueSample = sampleInside(rel - offset);
            float alpha = max(center.a, max(redSample.a, blueSample.a));
            float coreWeight = 0.84 - clamp(uAmount, 0.0, 1.0) * 0.18;
            vec3 core = center.rgb * coreWeight;
            vec3 separated = vec3(redSample.r, center.g, blueSample.b);
            finalColor = vec4(max(core, separated), alpha);
        }
        """;

    /// <summary>Folia sonnetPrintFilters.ts vignetteFragment: toward opaque black, in the filter's own frame.</summary>
    public const string VignetteFragment = PassHead + """
        uniform float uAmount;
        void main() {
            vec2 screenUv = frameUv();
            vec4 color = sampleInside(screenUv);
            vec2 centered = screenUv - 0.5;
            centered.x *= uFrame.z / uFrame.w;
            float vignette = clamp(smoothstep(0.52, 1.08, length(centered)) * uAmount * 0.6, 0.0, 1.0);
            finalColor = mix(color, vec4(0.0, 0.0, 0.0, 1.0), vignette);
        }
        """;

    /// <summary>Pixi BlurFilterPass, kernel 5, one pass per axis; edge pixels repeat (repeatEdgePixels).</summary>
    public const string BlurFragment = PassHead + """
        uniform vec2 uDirection;
        uniform float uStrength;
        void main() {
            vec2 px = uFrame.xy + frameUv() * uFrame.zw;
            vec2 step = uDirection * uStrength * uPixelScale;
            finalColor = sampleClamped(px - 2.0 * step) * 0.153388
                + sampleClamped(px - step) * 0.221461
                + sampleClamped(px) * 0.250301
                + sampleClamped(px + step) * 0.221461
                + sampleClamped(px + 2.0 * step) * 0.153388;
        }
        """;

    /// <summary>
    /// Folia temperaDifferenceFilter.ts. The text layer and the backdrop are aligned full-size textures, so both are sampled
    /// at the fragment's own pixel; the 5-tap average sits 1.5 logical pixels out, and the tint ramp runs across the layer's
    /// bounds.
    /// </summary>
    public const string InvertFragment = """
        #version 330 core
        uniform sampler2D uText;
        uniform sampler2D uBack;
        uniform vec2 uSize;
        uniform vec4 uOutFrame;
        uniform float uPixelScale;
        uniform vec3 uInkColor;
        uniform vec3 uPaperColor;
        uniform float uInkLuminance;
        uniform float uPaperLuminance;
        uniform float uBias;
        uniform vec3 uTint[4];
        uniform float uTintAmount;
        uniform int uInversion;
        out vec4 finalColor;

        vec3 sampleTint(float position) {
            float scaled = clamp(position, 0.0, 1.0) * 3.0;
            if (scaled < 1.0) return mix(uTint[0], uTint[1], scaled);
            if (scaled < 2.0) return mix(uTint[1], uTint[2], scaled - 1.0);
            return mix(uTint[2], uTint[3], scaled - 2.0);
        }

        float backLuminance(vec2 uv) {
            vec4 back = texture(uBack, uv);
            vec3 straight = back.rgb / max(back.a, 1e-4);
            float lum = dot(straight, vec3(0.2126, 0.7152, 0.0722));
            return mix(uPaperLuminance, lum, clamp(back.a * 3.0, 0.0, 1.0));
        }

        void main() {
            vec2 uv = gl_FragCoord.xy / uSize;
            vec4 front = texture(uText, uv);
            float tintPosition = clamp((gl_FragCoord.x - uOutFrame.x) / max(uOutFrame.z, 1.0), 0.0, 1.0);
            vec3 tone;
            if (uInversion == 1) {
                vec2 texel = vec2(1.5 * uPixelScale) / uSize;
                float lum = backLuminance(uv) * 0.4
                    + (backLuminance(uv + texel)
                        + backLuminance(uv - texel)
                        + backLuminance(uv + vec2(texel.x, -texel.y))
                        + backLuminance(uv + vec2(-texel.x, texel.y))) * 0.15;
                float distanceToPaper = abs(lum - uPaperLuminance);
                float distanceToInk = abs(lum - uInkLuminance);
                tone = mix(uInkColor, uPaperColor, step(distanceToInk + uBias, distanceToPaper));
                if (uTintAmount > 0.0) {
                    vec3 tint = sampleTint(tintPosition);
                    float tintLuminance = max(dot(tint, vec3(0.2126, 0.7152, 0.0722)), 1e-3);
                    float toneLuminance = dot(tone, vec3(0.2126, 0.7152, 0.0722));
                    vec3 matched = clamp(tint * (toneLuminance / tintLuminance), 0.0, 1.0);
                    tone = mix(tone, matched, uTintAmount);
                }
            } else {
                tone = uTintAmount > 0.0 ? sampleTint(tintPosition) : uInkColor;
            }
            finalColor = vec4(tone * front.a, front.a);
        }
        """;
}
