using System.Numerics;
using System.Runtime.InteropServices;
using SkiaSharp;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects;

public sealed class EffectTextureCache : IDisposable
{
    private const int CollectIntervalFrames = 32;

    private readonly GpuDevice _device;
    private readonly Dictionary<TextTextureKey, EffectTexture> _textTextures = [];
    private readonly Dictionary<LayeredTextTextureKey, EffectTexture> _layeredTextTextures = [];
    private readonly Dictionary<VectorTextureKey, EffectTexture> _vectorTextures = [];
    private readonly Dictionary<EffectTexture, LinkedListNode<CacheEntry>> _ownedTextures = [];
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = new();
    private readonly record struct CacheEntry(EffectTexture Texture, object? Key, ulong LastUsedFrame);
    private bool _collectionPending;
    internal const int CollectionBatchSize = 16;
    private ulong _frame;
    private ulong _lastCollectFrame;
    private long _residentBytes;

    public const long DefaultResidentByteLimit = 256L * 1024 * 1024;
    public const int DefaultResidentTextureLimit = 1024;

    // 常见跨平台中文字体候选列表（优先匹配）
    private static readonly string[] FallbackFontFamilies =
    [
        "Noto Sans CJK SC",
        "Source Han Sans SC",
        "Source Han Sans CN",
        "WenQuanYi Micro Hei",
        "WenQuanYi Zen Hei",
        "Microsoft YaHei",
        "PingFang SC",
        "SimHei",
        "Droid Sans Fallback"
    ];

    internal EffectTextureCache(GpuDevice device) => _device = device;

    internal GpuDevice Device => _device;

    internal EffectGpuResources TextResources => _device.Resources;

    internal EffectGpuResources.TimingScope MeasureTextPreparation() => _device.Resources.MeasureTextPreparation();

    public int Count => _ownedTextures.Count;
    public long ResidentBytes => _residentBytes;
    public long ResidentByteLimit { get; set; } = DefaultResidentByteLimit;
    public int ResidentTextureLimit { get; set; } = DefaultResidentTextureLimit;

    internal void BeginFrame() => _frame++;

    public void Touch(EffectTexture texture)
    {
        if (texture.IsDisposed || !_ownedTextures.TryGetValue(texture, out var node))
            return;
        node.Value = node.Value with { LastUsedFrame = _frame };
        if (!ReferenceEquals(node, _leastRecentlyUsed.Last))
        {
            _leastRecentlyUsed.Remove(node);
            _leastRecentlyUsed.AddLast(node);
        }
    }

    private void Register(EffectTexture texture, object? key = null)
    {
        _ownedTextures.Add(texture, _leastRecentlyUsed.AddLast(new CacheEntry(texture, key, _frame)));
        _residentBytes += texture.EstimatedBytes;
    }

    /// <summary>
    /// 获取能够支持当前文本的 Typeface（包含 Linux 中文 Fallback）
    /// </summary>
    internal static SKTypeface ResolveTypeface(string? fontFamily, SKFontStyle style, string? sampleText = null)
    {
        // 1. 尝试匹配用户指定的 fontFamily
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            var matched = SKFontManager.Default.MatchFamily(fontFamily, style);
            if (matched != null)
            {
                // 如果没有提供采样文本，或该字体包含文本中的首个非空字符，则直接采用
                if (string.IsNullOrEmpty(sampleText) || ContainsGlyph(matched, sampleText))
                    return matched;
            }
        }

        // 2. 如果文本中有中文等特殊字符，优先使用 MatchCharacter 获取支持该字符的系统字体
        if (!string.IsNullOrEmpty(sampleText))
        {
            foreach (var ch in sampleText)
            {
                if (!char.IsWhiteSpace(ch) && ch > 127)
                {
                    var charMatched = SKFontManager.Default.MatchCharacter(ch);
                    if (charMatched != null)
                        return charMatched;
                    break;
                }
            }
        }

        // 3. 尝试常用的中文字体名
        foreach (var fallbackName in FallbackFontFamilies)
        {
            var fallback = SKFontManager.Default.MatchFamily(fallbackName, style);
            if (fallback != null)
                return fallback;
        }

        // 4. 最后降级到系统默认字体
        return SKTypeface.Default;
    }

    private static bool ContainsGlyph(SKTypeface typeface, string text)
    {
        foreach (var ch in text)
        {
            if (ch > 127 && !char.IsWhiteSpace(ch))
            {
                return typeface.ContainsGlyph(ch);
            }
        }
        return true;
    }

    public static Vector2 MeasureText(string text, string fontFamily, float fontSize, int fontWeight)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        var style = fontWeight >= 700 ? SKFontStyle.Bold : SKFontStyle.Normal;
        using var typeface = ResolveTypeface(fontFamily, style, text);
        using var font = new SKFont(typeface, fontSize);

        var width = font.MeasureText(text);
        font.GetFontMetrics(out var metrics);
        return new Vector2(width, metrics.Descent - metrics.Ascent);
    }

    public EffectTexture GetOrCreateText(
        string text,
        string fontFamily,
        float fontSize,
        int fontWeight,
        EffectColor color,
        float rasterScale = 2,
        float strokeWidth = 0)
    {
        rasterScale = Math.Clamp(rasterScale, 1, 4);
        var key = new TextTextureKey(text, fontFamily, fontSize, fontWeight, color, rasterScale, Math.Max(0, strokeWidth));
        if (_textTextures.TryGetValue(key, out var cached))
        {
            Touch(cached);
            return cached;
        }

        using var timing = MeasureTextPreparation();
        var texture = RasterizeText(key);
        _textTextures.Add(key, texture);
        Register(texture, key);
        return texture;
    }

    /// <summary>
    /// Rasterizes the sharp glyph and its soft glow into one texture with the same
    /// typeface and baseline, so the two layers can never drift or spell differently.
    /// </summary>
    public EffectTexture GetOrCreateTextLayer(
        string text,
        string fontFamily,
        float fontSize,
        int fontWeight,
        EffectColor coreColor,
        EffectColor glowColor,
        float glowSigma,
        float rasterScale = 2)
    {
        rasterScale = Math.Clamp(rasterScale, 1, 4);
        var key = new LayeredTextTextureKey(
            text, fontFamily, fontSize, fontWeight, coreColor, glowColor, glowSigma, rasterScale);
        if (_layeredTextTextures.TryGetValue(key, out var cached))
        {
            Touch(cached);
            return cached;
        }

        using var timing = MeasureTextPreparation();
        var texture = RasterizeTextLayer(key);
        _layeredTextTextures.Add(key, texture);
        Register(texture, key);
        return texture;
    }

    public unsafe EffectTexture CreateRgba(ReadOnlySpan<byte> rgba, int width, int height, Vector2? logicalSize = null)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("RGBA data length does not match the texture dimensions.", nameof(rgba));

        var texture = EffectTexture.Upload(_device, rgba, width, height, logicalSize ?? new Vector2(width, height));
        Register(texture);
        return texture;
    }

    public unsafe EffectTexture GetOrCreateVector(
        string cacheKey,
        Vector2 logicalSize,
        float rasterScale,
        Action<SKCanvas> draw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(draw);
        rasterScale = Math.Clamp(rasterScale, 1, 4);
        var key = new VectorTextureKey(cacheKey, logicalSize, rasterScale);
        if (_vectorTextures.TryGetValue(key, out var cached))
        {
            Touch(cached);
            return cached;
        }

        var width = Math.Max(1, (int)Math.Ceiling(logicalSize.X * rasterScale));
        var height = Math.Max(1, (int)Math.Ceiling(logicalSize.Y * rasterScale));
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(rasterScale);
        draw(canvas);
        canvas.Flush();

        using var uploadTiming = TextResources.MeasureTextStage(EffectTextStage.Upload);
        // TexImage2D consumes the pixels synchronously; retain the native bitmap through the call.
        // Avoid a second, potentially multi-megabyte managed allocation and full-image copy.
        var texture = CreateRgba(new ReadOnlySpan<byte>((void*)bitmap.GetPixels(), bitmap.ByteCount), width, height, logicalSize);
        _vectorTextures.Add(key, texture);
        var node = _ownedTextures[texture];
        node.Value = node.Value with { Key = key };
        return texture;
    }

    private unsafe EffectTexture RasterizeText(TextTextureKey key)
    {
        var style = key.FontWeight >= 700 ? SKFontStyle.Bold : SKFontStyle.Normal;
        using var typeface = ResolveTypeface(key.FontFamily, style, key.Text);
        using var font = new SKFont(typeface, key.FontSize * key.RasterScale)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
        };

        var textWidth = font.MeasureText(key.Text);
        font.GetFontMetrics(out var metrics);
        const int padding = 12;
        var width = Math.Max(1, (int)Math.Ceiling(textWidth) + padding * 2);
        var height = Math.Max(1, (int)Math.Ceiling(metrics.Descent - metrics.Ascent) + padding * 2);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(
                (byte)Math.Clamp(key.Color.R * 255, 0, 255),
                (byte)Math.Clamp(key.Color.G * 255, 0, 255),
                (byte)Math.Clamp(key.Color.B * 255, 0, 255),
                (byte)Math.Clamp(key.Color.A * 255, 0, 255)),
        };
        canvas.Clear(SKColors.Transparent);
        if (key.StrokeWidth > 0)
        {
            // Outline-only glyphs, like a Pixi TextStyle with a transparent fill and a stroke.
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = key.StrokeWidth * key.RasterScale;
            paint.StrokeJoin = SKStrokeJoin.Round;
        }

        // 基线 y 坐标计算
        var y = padding - metrics.Ascent;
        canvas.DrawText(key.Text, padding, y, font, paint);
        canvas.Flush();

        return EffectTexture.Upload(_device, new ReadOnlySpan<byte>(bitmap.GetPixels().ToPointer(), bitmap.ByteCount),
            width, height, new Vector2(width / key.RasterScale, height / key.RasterScale), GpuFilter.LinearMipmap);
    }

    private unsafe EffectTexture RasterizeTextLayer(LayeredTextTextureKey key)
    {
        var style = key.FontWeight >= 700 ? SKFontStyle.Bold : SKFontStyle.Normal;
        using var typeface = ResolveTypeface(key.FontFamily, style, key.Text);
        using var font = new SKFont(typeface, key.FontSize * key.RasterScale)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
        };

        var textWidth = font.MeasureText(key.Text);
        font.GetFontMetrics(out var metrics);
        var blurPhysical = Math.Max(0f, key.GlowSigma * key.RasterScale);
        var padding = Math.Max(
            key.GlowSigma > 0 ? 20f : 12f,
            blurPhysical > 0 ? blurPhysical * 3.2f + 6f : 12f);
        padding = MathF.Ceiling(padding);
        var width = Math.Max(1, (int)Math.Ceiling(textWidth) + (int)padding * 2);
        var height = Math.Max(1, (int)Math.Ceiling(metrics.Descent - metrics.Ascent) + (int)padding * 2);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var y = padding - metrics.Ascent;

        if (key.GlowColor.A > 0 && key.GlowSigma > 0)
        {
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Color = ToSkColor(key.GlowColor),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blurPhysical),
            };
            canvas.DrawText(key.Text, padding, y, font, glow);
        }

        using var paint = new SKPaint { IsAntialias = true, Color = ToSkColor(key.CoreColor) };
        canvas.DrawText(key.Text, padding, y, font, paint);
        canvas.Flush();

        return EffectTexture.Upload(_device, new ReadOnlySpan<byte>(bitmap.GetPixels().ToPointer(), bitmap.ByteCount),
            width, height, new Vector2(width / key.RasterScale, height / key.RasterScale), GpuFilter.LinearMipmap);
    }

    private static SKColor ToSkColor(EffectColor color) => new(
        (byte)Math.Clamp(color.R * 255, 0, 255),
        (byte)Math.Clamp(color.G * 255, 0, 255),
        (byte)Math.Clamp(color.B * 255, 0, 255),
        (byte)Math.Clamp(color.A * 255, 0, 255));

    internal void Collect(int maximumIdleFrames = 120)
    {
        var pressureExceeded = IsOverLimit;
        if (!_collectionPending && !pressureExceeded && _frame - _lastCollectFrame < CollectIntervalFrames)
            return;
        _lastCollectFrame = _frame;

        var minimumFrame = _frame > (ulong)Math.Max(0, maximumIdleFrames)
            ? _frame - (ulong)maximumIdleFrames : 0;
        // Ordinary idle/soft-pressure reclamation is bounded. A 25% overshoot drains to the
        // configured limits immediately. Never evict textures referenced by this frame's draws.
        var hardPressure = _ownedTextures.Count > Math.Max(0L, ResidentTextureLimit) * 1.25 ||
            _residentBytes > Math.Max(0L, ResidentByteLimit) * 1.25;
        var released = 0;
        while (_leastRecentlyUsed.First is { } oldest && oldest.Value.LastUsedFrame < _frame)
        {
            if (!IsOverLimit && oldest.Value.LastUsedFrame >= minimumFrame) break;
            if (!hardPressure && released >= CollectionBatchSize) break;
            Remove(oldest.Value.Texture);
            released++;
        }
        _collectionPending = _leastRecentlyUsed.First is { } next && next.Value.LastUsedFrame < _frame &&
            (IsOverLimit || next.Value.LastUsedFrame < minimumFrame);
    }

    private bool IsOverLimit => _ownedTextures.Count > ResidentTextureLimit || _residentBytes > ResidentByteLimit;

    /// <summary>Releases a cached texture. Call only inside the owning device lifecycle.</summary>
    public void Release(EffectTexture texture) => Remove(texture);

    private void Remove(EffectTexture texture)
    {
        if (!_ownedTextures.Remove(texture, out var node)) return;
        switch (node.Value.Key)
        {
            case TextTextureKey key: _textTextures.Remove(key); break;
            case LayeredTextTextureKey key: _layeredTextTextures.Remove(key); break;
            case VectorTextureKey key: _vectorTextures.Remove(key); break;
        }
        _leastRecentlyUsed.Remove(node);
        _residentBytes -= texture.EstimatedBytes;
        texture.Dispose();
    }

    public void Dispose()
    {
        foreach (var texture in _ownedTextures.Keys)
            texture.Dispose();
        _ownedTextures.Clear();
        _leastRecentlyUsed.Clear();
        _textTextures.Clear();
        _layeredTextTextures.Clear();
        _vectorTextures.Clear();
        _collectionPending = false;
        _residentBytes = 0;
    }

    internal void Abandon()
    {
        foreach (var texture in _ownedTextures.Keys)
            texture.Abandon();
        _ownedTextures.Clear();
        _leastRecentlyUsed.Clear();
        _textTextures.Clear();
        _layeredTextTextures.Clear();
        _vectorTextures.Clear();
        _collectionPending = false;
        _residentBytes = 0;
    }

    private readonly struct VectorTextureKey(string cacheKey, Vector2 logicalSize, float rasterScale) : IEquatable<VectorTextureKey>
    {
        public string CacheKey { get; } = cacheKey;
        public Vector2 LogicalSize { get; } = logicalSize;
        public float RasterScale { get; } = rasterScale;

        public bool Equals(VectorTextureKey other) =>
            CacheKey == other.CacheKey &&
            LogicalSize.X.Equals(other.LogicalSize.X) &&
            LogicalSize.Y.Equals(other.LogicalSize.Y) &&
            RasterScale.Equals(other.RasterScale);

        public override bool Equals(object? obj) => obj is VectorTextureKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CacheKey, LogicalSize.X, LogicalSize.Y, RasterScale);
    }
}
