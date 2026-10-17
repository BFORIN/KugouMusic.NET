namespace AvaloniaSilkEffects.Tempera;

/// <summary>
/// Folia temperaPalette.ts: paper / ink, the monotonic paper → ink tone ladder (tone1..4), and in gradient mode the
/// four-colour cover ramp for fills (pulled onto the ladder) and for the lyric tint (hue kept, contrast floor only).
/// </summary>
public sealed record TemperaPalette(
    TemperaRgb Paper,
    TemperaRgb Ink,
    TemperaRgb BlockA,
    TemperaRgb BlockB,
    TemperaRgb BlockC,
    TemperaRgb Accent,
    TemperaRgb Line,
    TemperaRgb Shadow,
    TemperaRgb Tone1,
    TemperaRgb Tone2,
    TemperaRgb Tone3,
    TemperaRgb Tone4,
    TemperaRgb[]? Gradient,
    TemperaRgb[]? TextGradient)
{
    public static readonly double[] ToneStops = [0.12, 0.3, 0.52, 0.72];
    private const double MinInkContrast = 96;
    private const double ThemeHueMix = 0.32;

    private static TemperaRgb ToGray(TemperaRgb color)
    {
        var luminance = TemperaRgb.JsRound(color.Luminance);
        return new TemperaRgb(luminance, luminance, luminance);
    }

    private static TemperaRgb EnsureInkContrast(TemperaRgb paper, TemperaRgb ink)
    {
        if (Math.Abs(ink.Luminance - paper.Luminance) >= MinInkContrast) return ink;
        var target = paper.Luminance < 128 ? TemperaRgb.FromHex(0xf4f4f2) : TemperaRgb.FromHex(0x141414);
        var tinted = TemperaRgb.Mix(target, ink, 0.14);
        return Math.Abs(tinted.Luminance - paper.Luminance) >= MinInkContrast ? tinted : target;
    }

    /// <summary>Rescales a tinted colour back onto the untinted step's luminance; clipping falls back to the neutral step.</summary>
    private static TemperaRgb MatchLuminance(TemperaRgb color, TemperaRgb target)
    {
        var current = color.Luminance;
        var wanted = target.Luminance;
        if (current <= 0.5) return target;
        var gain = wanted / current;
        var scaled = new TemperaRgb(
            TemperaRgb.JsRound(Math.Min(255, color.R * gain)),
            TemperaRgb.JsRound(Math.Min(255, color.G * gain)),
            TemperaRgb.JsRound(Math.Min(255, color.B * gain)));
        return Math.Abs(scaled.Luminance - wanted) > 1.5 ? target : scaled;
    }

    private static (TemperaRgb, TemperaRgb, TemperaRgb, TemperaRgb) ToneLadder(TemperaRgb paper, TemperaRgb ink, TemperaRgb? tintA = null,
        TemperaRgb? tintB = null)
    {
        TemperaRgb Step(int index, TemperaRgb? tint)
        {
            var step = TemperaRgb.Mix(paper, ink, ToneStops[index]);
            return tint is { } hue ? MatchLuminance(TemperaRgb.Mix(step, hue, 0.3), step) : step;
        }
        return (Step(0, tintA), Step(1, tintB), Step(2, tintA), Step(3, null));
    }

    private static TemperaRgb[] GradientRamp(TemperaRgb paper, TemperaRgb ink, IReadOnlyList<TemperaRgb> sources)
    {
        // Array.prototype.sort is stable: equal luminances keep their order.
        var ranked = sources.OrderBy(color => color.Luminance).ToList();
        if (ink.Luminance < paper.Luminance) ranked.Reverse();
        return [.. ToneStops.Select((stop, index) =>
        {
            var rung = TemperaRgb.Mix(paper, ink, stop);
            if (ranked.Count == 0) return rung;
            var hue = ranked[index % ranked.Count];
            return MatchLuminance(TemperaRgb.Mix(rung, hue, 0.78), rung);
        })];
    }

    private static TemperaRgb EnforceReadable(TemperaRgb paper, TemperaRgb color)
    {
        var paperLuminance = paper.Luminance;
        if (Math.Abs(color.Luminance - paperLuminance) >= 88) return color;
        var away = paperLuminance < 128 ? TemperaRgb.FromHex(0xffffff) : TemperaRgb.FromHex(0x101010);
        for (var step = 1; step <= 5; step++)
        {
            var pushed = TemperaRgb.Mix(color, away, step * 0.16);
            if (Math.Abs(pushed.Luminance - paperLuminance) >= 88) return pushed;
        }
        return TemperaRgb.Mix(color, away, 0.8);
    }

    public static TemperaPalette Resolve(TemperaTheme theme, TemperaColorMode mode, IReadOnlyList<TemperaRgb>? coverColors = null)
    {
        if (mode == TemperaColorMode.Mono)
        {
            var monoPaper = ToGray(theme.Background);
            var monoInk = ToGray(theme.Primary);
            if (Math.Abs(monoInk.R - monoPaper.R) < 96)
                monoInk = monoPaper.R < 128 ? TemperaRgb.FromHex(0xf2f2f2) : TemperaRgb.FromHex(0x141414);
            var (t1, t2, t3, t4) = ToneLadder(monoPaper, monoInk);
            return new TemperaPalette(monoPaper, monoInk,
                TemperaRgb.Mix(monoPaper, monoInk, 0.08), TemperaRgb.Mix(monoPaper, monoInk, 0.18),
                TemperaRgb.Mix(monoPaper, monoInk, 0.34), TemperaRgb.Mix(monoPaper, monoInk, 0.85),
                TemperaRgb.Mix(monoPaper, monoInk, 0.55), TemperaRgb.Mix(monoPaper, monoInk, 0.75),
                t1, t2, t3, t4, null, null);
        }
        var paper = theme.Background;
        var ink = EnsureInkContrast(paper, theme.Primary);
        if (mode == TemperaColorMode.Gradient)
        {
            TemperaRgb[] themeHues = [theme.Accent, theme.Secondary, theme.Primary, ink];
            var hues = coverColors is { Count: >= 2 }
                ? coverColors.Take(4).Select((color, index) => TemperaRgb.Mix(color, themeHues[index % themeHues.Length], ThemeHueMix)).ToArray()
                : themeHues;
            var ramp = GradientRamp(paper, ink, hues);
            return new TemperaPalette(paper, ink, ramp[0], ramp[1], ramp[2], theme.Accent,
                TemperaRgb.Mix(paper, ink, 0.6), TemperaRgb.Mix(paper, ink, 0.8),
                ramp[0], ramp[1], ramp[2], ramp[3], ramp,
                hues.Length == 0 ? null : [.. Enumerable.Range(0, 4).Select(index => EnforceReadable(paper, hues[index % hues.Length]))]);
        }
        var (tone1, tone2, tone3, tone4) = ToneLadder(paper, ink, theme.Accent, theme.Secondary);
        return new TemperaPalette(paper, ink,
            TemperaRgb.Mix(paper, theme.Accent, 0.55), TemperaRgb.Mix(paper, theme.Secondary, 0.6), TemperaRgb.Mix(paper, ink, 0.78),
            theme.Accent, TemperaRgb.Mix(paper, ink, 0.6), TemperaRgb.Mix(paper, ink, 0.8),
            tone1, tone2, tone3, tone4, null, null);
    }
}
