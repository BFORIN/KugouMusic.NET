using System.Numerics;
using System.Text.RegularExpressions;
using AvaloniaSilkEffects.Gpu;
using SkiaSharp;

namespace AvaloniaSilkEffects.Lumiere.Text;

/// <summary>One glyph's cut of a line texture.</summary>
public readonly record struct LumiereGlyphSlice(
    string Char,
    // Left edge of the cut within the line and its width (logical px; the first / last cut include the padding).
    float X,
    float Width,
    // The glyph itself (difference of prefix widths) within the line.
    float CharX,
    float CharWidth,
    // Glyph center inside the cut (sprite anchor), so a glyph can turn about its own center.
    float AnchorX,
    float AnchorY,
    // Cut size (logical px) and its uv rectangle (u0, v0, u1, v1) in the line texture.
    Vector2 Size,
    Vector4 Uv,
    bool Blank,
    // Upright in vertical text (CJK, full-width); otherwise turned 90°.
    bool Upright);

/// <summary>
/// Folia text/glyphLine.ts: a line laid out on one canvas (white, tinted later), cut per glyph at prefix widths so
/// kerning survives. Rendered at <c>resolution</c>; very large lines are capped at <c>maxCanvasPx</c>.
/// </summary>
internal sealed partial class LumiereGlyphLine : IDisposable
{
    [GeneratedRegex(@"[ᄀ-ᇿ⺀-⿿　-〿぀-ヿ㄀-ㇿ㈀-鿿가-힯豈-﫿︰-﹏＀-￯]|[\uD840-\uD8C4][\uDC00-\uDFFF]")]
    private static partial Regex UprightPattern();

    public static bool IsUpright(string glyph) => UprightPattern().IsMatch(glyph);

    public LumiereGlyphLine(GpuDevice gpu, string text, float fontPx, string fontFamily, int weight, float resolution,
        float letterSpacing = 0, float outline = 0, int maxCanvasPx = 8192)
    {
        using var timing = gpu.Resources.MeasureTextPreparation();
        var measureTiming = gpu.Resources.MeasureTextStage(EffectTextStage.Measure);
        Text = text;
        FontPx = fontPx;
        var graphemes = LumiereReveal.Graphemes(text);
        var typeface = EffectTextMetrics.GetTypeface(fontFamily, weight, text);
        using var font = new SKFont(typeface, fontPx) { Edging = SKFontEdging.Antialias, Subpixel = true };
        var spacing = letterSpacing * fontPx;

        // Prefix widths keep kerning; per-glyph boundaries fall on them.
        var offsets = new float[graphemes.Count + 1];
        var prefix = "";
        for (var index = 0; index < graphemes.Count; index++)
        {
            prefix += graphemes[index];
            offsets[index + 1] = font.MeasureText(prefix) + spacing * (index + 1);
        }
        var width = MathF.Max(1, offsets[^1]);
        var height = MathF.Ceiling(fontPx * 1.5f);
        var pad = MathF.Ceiling(fontPx * 0.25f);
        Width = width;
        Height = height;
        Pad = pad;

        var scale = MathF.Min(resolution, MathF.Min(maxCanvasPx / (width + pad * 2), maxCanvasPx / (height + pad * 2)));
        var canvasWidth = Math.Max(1, (int)MathF.Ceiling((width + pad * 2) * scale));
        var canvasHeight = Math.Max(1, (int)MathF.Ceiling((height + pad * 2) * scale));
        measureTiming.Dispose();
        var rasterTiming = gpu.Resources.MeasureTextStage(EffectTextStage.Raster);
        using var bitmap = new SKBitmap(new SKImageInfo(canvasWidth, canvasHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            font.GetFontMetrics(out var metrics);
            // Canvas textBaseline 'middle': the ascent/descent box is centered on the line.
            var baseline = pad + height / 2 - (metrics.Ascent + metrics.Descent) / 2;
            using var fill = new SKPaint { IsAntialias = true, Color = SKColors.White };
            using var stroke = new SKPaint
            {
                IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Stroke,
                StrokeWidth = fontPx * outline, StrokeJoin = SKStrokeJoin.Miter,
            };
            void Draw(string glyph, float x)
            {
                if (outline > 0)
                {
                    // Hollow glyph: thin stroke over a faint fill.
                    fill.Color = SKColors.White.WithAlpha((byte)Math.Round(0.14 * 255));
                    canvas.DrawText(glyph, x, baseline, font, fill);
                    canvas.DrawText(glyph, x, baseline, font, stroke);
                }
                else canvas.DrawText(glyph, x, baseline, font, fill);
            }
            if (spacing == 0) Draw(text, pad);
            else for (var index = 0; index < graphemes.Count; index++) Draw(graphemes[index], pad + offsets[index]);
        }
        rasterTiming.Dispose();
        Texture = Upload(gpu, bitmap, new Vector2(width + pad * 2, height + pad * 2));

        var totalWidth = width + pad * 2;
        var totalHeight = height + pad * 2;
        var uScale = (totalWidth * scale) / canvasWidth / totalWidth;
        var vMax = totalHeight * scale / canvasHeight;
        Glyphs = [.. graphemes.Select((glyph, index) =>
        {
            var x = offsets[index];
            var w = MathF.Max(0.5f, offsets[index + 1] - x);
            var left = index == 0 ? 0 : pad + x;
            var right = index == graphemes.Count - 1 ? totalWidth : pad + x + w;
            return new LumiereGlyphSlice(glyph, left - pad, right - left, x, w,
                (pad + x + w / 2 - left) / (right - left), 0.5f,
                new Vector2(right - left, totalHeight),
                new Vector4(left * uScale, 0, right * uScale, vMax),
                string.IsNullOrWhiteSpace(glyph), IsUpright(glyph));
        })];
    }

    internal static long EstimateBytes(string text, float fontPx, string family, int weight, float resolution,
        int graphemeCount, float letterSpacing)
    {
        using var font = new SKFont(EffectTextMetrics.GetTypeface(family, weight, text), fontPx)
            { Edging = SKFontEdging.Antialias, Subpixel = true };
        var width = MathF.Max(1, font.MeasureText(text) + letterSpacing * fontPx * graphemeCount);
        var height = MathF.Ceiling(fontPx * 1.5f);
        var pad = MathF.Ceiling(fontPx * 0.25f);
        var scale = MathF.Min(resolution, MathF.Min(8192 / (width + pad * 2), 8192 / (height + pad * 2)));
        return (long)Math.Max(1, (int)MathF.Ceiling((width + pad * 2) * scale)) *
            Math.Max(1, (int)MathF.Ceiling((height + pad * 2) * scale)) * 4;
    }

    public string Text { get; }
    public float FontPx { get; }
    /// <summary>Line width and height (logical px), without padding.</summary>
    public float Width { get; }
    public float Height { get; }
    public float Pad { get; }
    public EffectTexture Texture { get; }
    public IReadOnlyList<LumiereGlyphSlice> Glyphs { get; }

    private static unsafe EffectTexture Upload(GpuDevice gpu, SKBitmap bitmap, Vector2 logical)
    {
        using var uploadTiming = gpu.Resources.MeasureTextStage(EffectTextStage.Upload);
        return EffectTexture.Upload(gpu, new ReadOnlySpan<byte>((void*)bitmap.GetPixels(), bitmap.ByteCount),
            bitmap.Width, bitmap.Height, logical);
    }

    public void Dispose() => Texture.Dispose();
}
