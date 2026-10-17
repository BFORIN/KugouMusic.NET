using AvaloniaSilkEffects.Lumiere.Text;

namespace AvaloniaSilkEffects.Tempera;

/// <summary>
/// A Folia colour string reduced to what Pixi keeps of it: 0..255 integer channels. Folia's mixColors rounds every result
/// to integers and Pixi's <c>Color.toNumber()</c> drops alpha, so a colour's alpha never reaches a fill or a stroke.
/// </summary>
public readonly record struct TemperaRgb(int R, int G, int B)
{
    public static TemperaRgb FromHex(uint rgb) => new((int)(rgb >> 16) & 0xFF, (int)(rgb >> 8) & 0xFF, (int)rgb & 0xFF);

    public static TemperaRgb FromEffect(EffectColor color) =>
        new(JsRound(Math.Clamp(color.R, 0, 1) * 255), JsRound(Math.Clamp(color.G, 0, 1) * 255), JsRound(Math.Clamp(color.B, 0, 1) * 255));

    /// <summary>Rec.709 luminance in 0..255, like Folia's luminanceOf.</summary>
    public double Luminance => R * 0.2126 + G * 0.7152 + B * 0.0722;

    /// <summary>Folia colorMix.ts mixColors: per-channel lerp, rounded.</summary>
    public static TemperaRgb Mix(TemperaRgb from, TemperaRgb to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        return new TemperaRgb(
            JsRound(from.R + (to.R - from.R) * t),
            JsRound(from.G + (to.G - from.G) * t),
            JsRound(from.B + (to.B - from.B) * t));
    }

    public EffectColor ToEffect(float alpha = 1) => new(R / 255f, G / 255f, B / 255f, alpha);

    public System.Numerics.Vector3 ToVector3() => new(R / 255f, G / 255f, B / 255f);

    internal static int JsRound(double value) => (int)Math.Floor(value + 0.5);

    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
}

public enum TemperaAnimationIntensity
{
    Calm,
    Normal,
    Chaotic,
}

/// <summary>The Folia theme side Tempera reads: four colours, the font and the animation intensity.</summary>
public sealed record TemperaTheme(
    TemperaRgb Background,
    TemperaRgb Primary,
    TemperaRgb Secondary,
    TemperaRgb Accent,
    string FontFamily = "PingFang SC",
    int FontWeight = 600,
    TemperaAnimationIntensity AnimationIntensity = TemperaAnimationIntensity.Normal)
{
    /// <summary>Folia services/baseThemes.ts DEFAULT_THEME, 「午夜墨染」.</summary>
    public static TemperaTheme MidnightDefault { get; } = new(
        TemperaRgb.FromHex(0x09090b), TemperaRgb.FromHex(0xf4f4f5), TemperaRgb.FromHex(0x71717a), TemperaRgb.FromHex(0xf4f4f5));

    /// <summary>Theme keywords and their colours (Folia theme.wordColors).</summary>
    public IReadOnlyList<LumiereWordColor>? WordColors { get; init; }

    internal double AnimationScale => AnimationIntensity switch
    {
        TemperaAnimationIntensity.Calm => 0.65,
        TemperaAnimationIntensity.Chaotic => 1.35,
        _ => 1,
    };
}
