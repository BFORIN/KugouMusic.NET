using SkiaSharp;

// Tempera 字体度量门面：委托给全局通用的 EffectTextMetrics

namespace AvaloniaSilkEffects.Tempera.Text;

/// <summary>
/// Canvas-style text metrics. Advance widths stand in for pretext's <c>measureText</c>; the vertical box is Pixi's
/// CanvasTextMetrics.measureFont: the actual ascent and descent of "|ÉqÅM", so an anchor-0.5 glyph centres where Pixi's does.
/// </summary>
internal static class TemperaFonts
{
    public static SKFontStyle Style(int weight) => EffectTextMetrics.ResolveFontStyle(weight);

    /// <summary>The typeface for the family at this weight, falling back per character for scripts the family lacks.</summary>
    public static SKTypeface Typeface(string family, int weight, string? sample) =>
        EffectTextMetrics.GetTypeface(family, weight, sample);

    /// <summary>(ascent, descent) per unit font size.</summary>
    public static (float Ascent, float Descent) Box(string family, int weight) =>
        EffectTextMetrics.GetBoxMetrics(family, weight);

    public static double Measure(string text, string family, int weight, double fontSize) =>
        EffectTextMetrics.Measure(text, family, weight, fontSize);

    public static double MeasureGrapheme(string character, string family, int weight, double fontSize) =>
        EffectTextMetrics.MeasureGrapheme(character, family, weight, fontSize);
}
