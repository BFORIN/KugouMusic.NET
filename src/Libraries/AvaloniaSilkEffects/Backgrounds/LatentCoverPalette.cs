using SkiaSharp;
namespace AvaloniaSilkEffects.Backgrounds;

public static class LatentCoverPalette
{
    public static IReadOnlyList<EffectColor> ExtractEncoded(byte[] encoded)
    {
        using var decoded = SKBitmap.Decode(encoded);
        if (decoded == null) return [];
        using var sample = decoded.Resize(new SKImageInfo(50,50,SKColorType.Rgba8888,SKAlphaType.Unpremul),
            new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.None));
        if (sample == null) return [];
        var pixels = new byte[50*50*4];
        for (var y=0;y<50;y++)
        for (var x=0;x<50;x++)
        {
            var color = sample.GetPixel(x,y);
            var index = (y*50+x)*4;
            pixels[index] = color.Red;
            pixels[index+1] = color.Green;
            pixels[index+2] = color.Blue;
            pixels[index+3] = color.Alpha;
        }
        return PaletteExtractor.Extract(pixels);
    }
}
