using System.Numerics;

namespace AvaloniaSilkEffects.Tempera.Text;

/// <summary>One renderer's bounded measurement/glyph metadata. Texture ownership remains in EffectTextureCache.</summary>
internal sealed class TemperaGlyphCache(EffectTextureCache textures)
{
    private readonly record struct LayoutKey(string Text, string Family, int Weight, double Size, double Wrap);
    private readonly Dictionary<LayoutKey, TemperaGlyphs.Layout> _layouts = [];
    private readonly Dictionary<(LayoutKey Layout, float Scale), TemperaGlyph> _glyphs = [];

    private TemperaGlyphs.Layout Layout(LayoutKey key)
    {
        if (_layouts.TryGetValue(key, out var cached)) return cached;
        using var preparation = textures.MeasureTextPreparation();
        using var timing = textures.TextResources.MeasureTextStage(EffectTextStage.Measure);
        var layout = TemperaGlyphs.Measure(key.Text, key.Family, key.Weight, key.Size, key.Wrap);
        if (_layouts.Count >= 512) _layouts.Clear();
        _layouts[key] = layout;
        return layout;
    }

    public Vector2 Box(string text, string family, int weight, double size, double wrap) =>
        Layout(new(text, family, weight, size, wrap)).Box;

    public long EstimateBytes(string text, string family, int weight, double size, double wrap, float scale)
    {
        var box = Layout(new(text, family, weight, size, wrap)).Box;
        var pad = MathF.Ceiling((float)size * 0.3f) + 2;
        scale = Math.Clamp(scale, 1, 4);
        var width = Math.Max(1, (int)Math.Ceiling(MathF.Ceiling(box.X + pad * 2) * scale));
        var height = Math.Max(1, (int)Math.Ceiling(MathF.Ceiling(box.Y + pad * 2) * scale));
        return (long)width * height * 4;
    }

    public TemperaGlyph Get(string text, string family, int weight, double size, float scale, double wrap)
    {
        var key = (new LayoutKey(text, family, weight, size, wrap), Math.Clamp(scale, 1, 4));
        if (_glyphs.TryGetValue(key, out var glyph) && !glyph.Texture.IsDisposed)
        {
            textures.Touch(glyph.Texture);
            return glyph;
        }
        glyph = TemperaGlyphs.Get(textures, text, family, weight, size, scale, wrap, Layout(key.Item1));
        if (_glyphs.Count >= 1024) _glyphs.Clear();
        _glyphs[key] = glyph;
        return glyph;
    }

    public void Clear() { _layouts.Clear(); _glyphs.Clear(); }
}
