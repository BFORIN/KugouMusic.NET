namespace AvaloniaSilkEffects.Tempera;

public enum TemperaColorMode
{
    /// <summary>Tone ladder tinted with the theme's accent / secondary hues.</summary>
    Duo,
    /// <summary>A grayscale ink ↔ paper ladder.</summary>
    Mono,
    /// <summary>Cover colours as four-stop ramps on every fill, and as the lyric's tint.</summary>
    Gradient,
}

/// <summary>Folia TemperaTuning with DEFAULT_TEMPERA_TUNING values (the user image pool is not ported).</summary>
public sealed record TemperaTuning
{
    public double CameraIntensity { get; init; } = 1;
    /// <summary>Per-glyph entrance motion strength, 0..2.</summary>
    public double GlyphMotion { get; init; } = 1;
    /// <summary>Keep each source lyric line in one shot instead of slicing it into half-phrases.</summary>
    public bool WholeLineLyrics { get; init; }
    /// <summary>逐字入场时序, 0..1: how far each glyph's entrance stretches toward the shot's lyric end.</summary>
    public double GlyphSettleStretch { get; init; } = 0.5;
    public TemperaColorMode ColorMode { get; init; } = TemperaColorMode.Duo;
    public bool ShowBlocks { get; init; } = true;
    public bool ShowDecor { get; init; } = true;
    /// <summary>边角线框.</summary>
    public bool ShowCornerMarks { get; init; } = true;
    /// <summary>文字动态反色: the lyric picks ink or paper per pixel against the artwork under it.</summary>
    public bool TextInversion { get; init; } = true;
    public bool EnableTransitions { get; init; } = true;
    /// <summary>Scene-wide lens / grain / contrast / print passes.</summary>
    public bool PostProcessEnabled { get; init; } = true;
    public double PostProcessGrain { get; init; } = 0.2;
    public double PostProcessContrast { get; init; }
    public double PostProcessRgbShift { get; init; }
    public double PostProcessVignette { get; init; } = 0.85;
    public double PostProcessLensDistortion { get; init; } = 0.3;
}
