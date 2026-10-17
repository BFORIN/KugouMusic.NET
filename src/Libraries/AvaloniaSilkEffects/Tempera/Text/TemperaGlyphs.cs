using System.Numerics;
using SkiaSharp;

namespace AvaloniaSilkEffects.Tempera.Text;

/// <summary>A rasterized white glyph and where its Pixi text box centre sits inside the texture (logical units).</summary>
internal readonly record struct TemperaGlyph(EffectTexture Texture, Vector2 Center);

/// <summary>
/// Pixi Text with anchor 0.5, rasterized once per (text, font, size, wrap, raster scale) in white so every copy — the glyph, its
/// ghost, its echoes, the filtered layer — tints the same texture. The box is the advance width by the |ÉqÅM ascent +
/// descent per line; the texture adds padding around it so overhanging ink is not clipped. Word wrap follows Pixi's default
/// (break at spaces only, never inside a word), with lines centred.
/// </summary>
internal static class TemperaGlyphs
{
    private readonly record struct Layout(string[] Lines, double[] Widths, Vector2 Box);

    private static Layout Measure(string text, string family, int weight, double fontSize, double wrapWidth)
    {
        var (ascent, descent) = TemperaFonts.Box(family, weight);
        var lineHeight = fontSize * (ascent + descent);
        var lines = wrapWidth > 0 ? Wrap(text, family, weight, fontSize, wrapWidth) : [text];
        var widths = lines.Select(line => TemperaFonts.Measure(line, family, weight, fontSize)).ToArray();
        return new Layout(lines, widths, new Vector2((float)widths.Max(), (float)(lineHeight * lines.Length)));
    }

    private static string[] Wrap(string text, string family, int weight, double fontSize, double wrapWidth)
    {
        var output = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var current = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && TemperaFonts.Measure(candidate, family, weight, fontSize) > wrapWidth)
                {
                    output.Add(current);
                    current = word;
                }
                else current = candidate;
            }
            output.Add(current);
        }
        return [.. output];
    }

    /// <summary>Pixi's text box for anchor maths and filter bounds.</summary>
    public static Vector2 Box(string text, string family, int weight, double fontSize, double wrapWidth = 0) =>
        Measure(text, family, weight, fontSize, wrapWidth).Box;

    public static TemperaGlyph Get(EffectTextureCache cache, string text, string family, int weight, double fontSize, float rasterScale,
        double wrapWidth = 0)
    {
        var layout = Measure(text, family, weight, fontSize, wrapWidth);
        var box = layout.Box;
        var (ascent, descent) = TemperaFonts.Box(family, weight);
        var lineHeight = fontSize * (ascent + descent);
        var pad = MathF.Ceiling((float)fontSize * 0.3f) + 2;
        var logical = new Vector2(MathF.Ceiling(box.X + pad * 2), MathF.Ceiling(box.Y + pad * 2));
        var scale = Math.Clamp(rasterScale, 1, 4);
        var key = $"tempera:{family}:{weight}:{fontSize:R}:{wrapWidth:R}:{text}";
        var texture = cache.GetOrCreateVector(key, logical, scale, canvas =>
        {
            using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
            for (var index = 0; index < layout.Lines.Length; index++)
            {
                var line = layout.Lines[index];
                if (line.Length == 0) continue;
                using var font = new SKFont(TemperaFonts.Typeface(family, weight, line), (float)fontSize)
                {
                    Edging = SKFontEdging.Antialias,
                    Subpixel = true,
                };
                var x = pad + (float)((box.X - layout.Widths[index]) / 2);
                canvas.DrawText(line, x, pad + (float)(index * lineHeight + fontSize * ascent), font, paint);
            }
        });
        return new TemperaGlyph(texture, new Vector2(pad + box.X / 2, pad + box.Y / 2));
    }
}
