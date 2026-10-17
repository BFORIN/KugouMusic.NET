using System.Collections.Concurrent;
using SkiaSharp;

namespace AvaloniaSilkEffects.Tempera.Text;

/// <summary>
/// Canvas-style text metrics. Advance widths stand in for pretext's <c>measureText</c>; the vertical box is Pixi's
/// CanvasTextMetrics.measureFont: the actual ascent and descent of "|ÉqÅM", so an anchor-0.5 glyph centres where Pixi's does.
/// </summary>
internal static class TemperaFonts
{
    private const string MetricsString = "|ÉqÅM";
    private const int MeasureCacheLimit = 20000;
    private static readonly ConcurrentDictionary<(string Family, int Weight, string? Sample), SKTypeface> Typefaces = new();
    private static readonly ConcurrentDictionary<(string Family, int Weight), (float Ascent, float Descent)> BoxMetrics = new();
    private static readonly Dictionary<(string, int, double, string), double> MeasureCache = [];
    private static readonly Queue<(string, int, double, string)> MeasureOrder = new();

    public static SKFontStyle Style(int weight) => new(Math.Clamp(weight, 100, 1000), (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    /// <summary>The typeface for the family at this weight, falling back per character for scripts the family lacks.</summary>
    public static SKTypeface Typeface(string family, int weight, string? sample)
    {
        // Only a non-ASCII sample can change the pick; key on its first such character.
        string? key = null;
        if (!string.IsNullOrEmpty(sample))
            foreach (var character in sample)
                if (character > 127 && !char.IsWhiteSpace(character))
                {
                    key = character.ToString();
                    break;
                }
        return Typefaces.GetOrAdd((family, weight, key), static k => EffectTextureCache.ResolveTypeface(k.Family, Style(k.Weight), k.Sample));
    }

    /// <summary>(ascent, descent) per unit font size.</summary>
    public static (float Ascent, float Descent) Box(string family, int weight) =>
        BoxMetrics.GetOrAdd((family, weight), static key =>
        {
            using var font = new SKFont(Typeface(key.Family, key.Weight, null), 100);
            font.MeasureText(MetricsString, out var bounds);
            var ascent = -bounds.Top / 100f;
            var descent = bounds.Bottom / 100f;
            if (ascent + descent <= 0) return (1, 0);
            return (ascent, descent);
        });

    public static double Measure(string text, string family, int weight, double fontSize)
    {
        var key = (family, weight, fontSize, text);
        lock (MeasureCache)
        {
            if (MeasureCache.TryGetValue(key, out var cached)) return cached;
        }
        double measured;
        try
        {
            using var font = new SKFont(Typeface(family, weight, text), (float)fontSize) { Subpixel = true };
            measured = font.MeasureText(text);
        }
        catch
        {
            measured = text.Length * fontSize * 0.6;
        }
        var width = Math.Max(fontSize * 0.08, measured);
        lock (MeasureCache)
        {
            if (MeasureCache.Count >= MeasureCacheLimit && MeasureOrder.TryDequeue(out var oldest)) MeasureCache.Remove(oldest);
            if (MeasureCache.TryAdd(key, width)) MeasureOrder.Enqueue(key);
        }
        return width;
    }

    public static double MeasureGrapheme(string character, string family, int weight, double fontSize) =>
        character.Trim().Length == 0 ? fontSize * 0.3 : Measure(character, family, weight, fontSize);
}
