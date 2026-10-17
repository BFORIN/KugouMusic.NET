namespace AvaloniaSilkEffects.Tempera;

public enum TemperaAlign
{
    Center,
    Left,
    Right,
}

public enum TemperaMood
{
    Quiet,
    Neutral,
    Loud,
}

/// <summary>Layout box for the lyric, in fractions of the viewport.</summary>
public readonly record struct TemperaShotRegion(double Cx, double Cy, double W, double H, TemperaAlign Align, double Rotation,
    double FontScale);

/// <summary>Base glyph entrance vector, in multiples of the resolved font size.</summary>
public readonly record struct TemperaEnterVector(double X, double Y);

/// <summary>Camera travel along the shot's flow angle, plus its zoom ramp.</summary>
public readonly record struct TemperaCameraProfile(double Travel, double ZoomStart, double ZoomEnd);

/// <summary>
/// Folia temperaShotProfiles.ts: where the type sits, which way its glyphs fly in, how the camera travels, how loud the
/// composition reads, and whether the shared crossing lines / motif overlay are drawn on top.
/// </summary>
public sealed record TemperaShotProfile(TemperaShotRegion Region, TemperaEnterVector Enter, TemperaCameraProfile Camera,
    TemperaMood Mood, bool SharedDecor);

public static class TemperaShotProfiles
{
    /// <summary>Folia TEMPERA_SHOT_KINDS, in order: the order is what seeded picks index into.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
    [
        "duo-split", "quad-split", "tri-column", "thirds-stack", "checker-quad", "corner-wedge",
        "diagonal-halves", "cross-axis", "offset-halves", "stair-blocks", "pillar-gap", "corner-quad",
        "sliver-stack", "band-strip", "horizon-band", "deep-dive", "tone-ramp", "double-band",
        "tilt-band", "edge-rails", "gradient-wall", "terrace", "frame-window", "double-frame",
        "circle-window", "ladder-frame", "corner-brackets", "inset-box", "bracket-pair", "arch-window",
        "grid-cells", "keyhole", "poster-panel", "diamond-stack", "slash-poster", "arrow-wedge",
        "edge-bleed", "triangle-mass", "ribbon-cross", "half-disc", "stacked-slabs", "wedge-pair",
        "quiet-line", "starfield-dots", "ripple-lines", "hair-grid", "margin-rule", "dot-drift",
        "arc-sweep", "blank-page", "cinema-scope", "cinema-wide", "cinema-academy", "cinema-square",
        "cinema-portrait", "cinema-tall", "cinema-twin", "bubble-drift", "cloud-window", "heart-burst",
        "sparkle-field", "petal-arc", "scallop-band", "ribbon-loop", "round-plate", "halo-burst",
        "iris-hole", "slot-rail", "punch-row", "film-gate", "cross-vent", "louvre-slats",
        "ring-eye", "notch-stack", "wedge-gap", "dot-sieve", "sight-mark", "dial-scale",
        "chevron-run", "tally-column", "grid-focus", "axis-caps", "strobe-slats", "offset-plate",
        "radial-comb", "bracket-target", "flow-channel", "twin-channel", "reed-run", "taper-channel",
        "chain-ports", "dash-channel", "window-run", "bridge-span", "braid-channel", "port-ladder",
        "apex-mass", "ziggurat", "slab-wall", "cantilever", "pylon-pair", "bunker-slit",
        "plinth-stack", "buttress-run", "void-core", "shear-block", "ridge-line", "chasm",
        "overhang", "step-well", "pier-row", "revetment", "tower-crop", "lintel",
        "rubble-fan", "gnomon", "monogatari-card", "monogatari-rule", "monogatari-edge", "monogatari-stack",
        "monogatari-flash",
    ];

    private static readonly Dictionary<string, TemperaShotProfile> Profiles = new()
    {
        ["duo-split"] = new(new(0.5, 0.52, 0.86, 0.46, TemperaAlign.Center, 0, 1), new(0, 1.3), new(0.11, 1.06, 1.13), TemperaMood.Neutral, true),
        ["quad-split"] = new(new(0.5, 0.5, 0.82, 0.4, TemperaAlign.Center, 0, 1), new(0.9, 0.9), new(0.09, 1.08, 1.16), TemperaMood.Loud, true),
        ["tri-column"] = new(new(0.5, 0.5, 0.7, 0.5, TemperaAlign.Center, 0, 0.95), new(-1.2, 0), new(0.12, 1.04, 1.12), TemperaMood.Neutral, true),
        ["thirds-stack"] = new(new(0.5, 0.5, 0.8, 0.28, TemperaAlign.Center, 0, 1), new(0, 1.1), new(0.13, 1.03, 1.11), TemperaMood.Neutral, true),
        ["checker-quad"] = new(new(0.5, 0.5, 0.76, 0.36, TemperaAlign.Center, 0, 1), new(0.8, -0.8), new(0.1, 1.1, 1.02), TemperaMood.Loud, true),
        ["corner-wedge"] = new(new(0.44, 0.56, 0.68, 0.4, TemperaAlign.Left, -0.03, 1), new(-1.4, 0.5), new(0.1, 1.05, 1.14), TemperaMood.Loud, true),
        ["diagonal-halves"] = new(new(0.5, 0.5, 0.78, 0.4, TemperaAlign.Center, -0.075, 1), new(1.1, 1.1), new(0.12, 1.06, 1.14), TemperaMood.Neutral, true),
        ["cross-axis"] = new(new(0.5, 0.5, 0.66, 0.3, TemperaAlign.Center, 0, 1), new(0, 1.2), new(0.08, 1.12, 1.03), TemperaMood.Loud, true),
        ["offset-halves"] = new(new(0.5, 0.5, 0.8, 0.4, TemperaAlign.Center, 0, 1), new(0.9, 0.6), new(0.11, 1.05, 1.13), TemperaMood.Neutral, true),
        ["stair-blocks"] = new(new(0.5, 0.5, 0.72, 0.38, TemperaAlign.Center, -0.03, 1), new(-1, 0.8), new(0.12, 1.06, 1.14), TemperaMood.Loud, true),
        ["pillar-gap"] = new(new(0.5, 0.5, 0.34, 0.62, TemperaAlign.Center, 0, 0.8), new(0, 1.2), new(0.07, 1.1, 1.02), TemperaMood.Loud, true),
        ["corner-quad"] = new(new(0.46, 0.46, 0.68, 0.36, TemperaAlign.Center, 0, 1), new(-0.9, -0.9), new(0.1, 1.07, 1.15), TemperaMood.Neutral, true),
        ["sliver-stack"] = new(new(0.5, 0.5, 0.76, 0.3, TemperaAlign.Center, 0, 1), new(1.2, 0), new(0.13, 1.04, 1.12), TemperaMood.Neutral, true),
        ["band-strip"] = new(new(0.5, 0.52, 0.78, 0.26, TemperaAlign.Center, 0, 0.92), new(0.5, 1.05), new(0.12, 1.03, 1.1), TemperaMood.Neutral, true),
        ["horizon-band"] = new(new(0.5, 0.36, 0.8, 0.3, TemperaAlign.Center, 0, 1), new(0, -1.1), new(0.14, 1.02, 1.1), TemperaMood.Neutral, true),
        ["deep-dive"] = new(new(0.5, 0.58, 0.76, 0.34, TemperaAlign.Center, 0, 1), new(0, 1.6), new(0.16, 1.04, 1.14), TemperaMood.Neutral, true),
        ["tone-ramp"] = new(new(0.5, 0.5, 0.82, 0.38, TemperaAlign.Center, 0, 1), new(1.2, 0.4), new(0.11, 1.05, 1.12), TemperaMood.Neutral, true),
        ["double-band"] = new(new(0.5, 0.5, 0.78, 0.22, TemperaAlign.Center, 0, 0.88), new(0.8, 0), new(0.12, 1.03, 1.11), TemperaMood.Neutral, true),
        ["tilt-band"] = new(new(0.5, 0.5, 0.8, 0.26, TemperaAlign.Center, -0.1, 1), new(-1.1, 0.5), new(0.13, 1.05, 1.13), TemperaMood.Neutral, true),
        ["edge-rails"] = new(new(0.5, 0.5, 0.74, 0.4, TemperaAlign.Center, 0, 1), new(0, 1), new(0.09, 1.02, 1.09), TemperaMood.Quiet, true),
        ["gradient-wall"] = new(new(0.5, 0.44, 0.82, 0.36, TemperaAlign.Center, 0, 1), new(0.5, -0.9), new(0.15, 1.04, 1.13), TemperaMood.Neutral, true),
        ["terrace"] = new(new(0.46, 0.52, 0.72, 0.34, TemperaAlign.Left, 0, 1), new(-1.2, 0.4), new(0.14, 1.05, 1.12), TemperaMood.Neutral, true),
        ["frame-window"] = new(new(0.5, 0.5, 0.64, 0.5, TemperaAlign.Center, 0, 0.95), new(0, 0.95), new(0.05, 1.14, 1.03), TemperaMood.Neutral, true),
        ["double-frame"] = new(new(0.5, 0.5, 0.58, 0.4, TemperaAlign.Center, 0, 0.9), new(0.6, 0.6), new(0.06, 1.12, 1.02), TemperaMood.Neutral, true),
        ["circle-window"] = new(new(0.5, 0.5, 0.5, 0.34, TemperaAlign.Center, 0, 0.88), new(0, 0.8), new(0.05, 1.16, 1.04), TemperaMood.Quiet, true),
        ["ladder-frame"] = new(new(0.52, 0.5, 0.6, 0.4, TemperaAlign.Left, 0, 0.9), new(-0.9, 0.6), new(0.07, 1.1, 1.02), TemperaMood.Quiet, true),
        ["corner-brackets"] = new(new(0.5, 0.5, 0.56, 0.3, TemperaAlign.Center, 0, 0.85), new(0, 0.6), new(0.04, 1.06, 1.01), TemperaMood.Quiet, true),
        ["inset-box"] = new(new(0.5, 0.5, 0.6, 0.42, TemperaAlign.Center, 0, 0.92), new(0, 0.8), new(0.06, 1.1, 1.02), TemperaMood.Quiet, true),
        ["bracket-pair"] = new(new(0.5, 0.5, 0.54, 0.34, TemperaAlign.Center, 0, 0.88), new(1, 0), new(0.05, 1.08, 1.01), TemperaMood.Quiet, true),
        ["arch-window"] = new(new(0.5, 0.54, 0.5, 0.34, TemperaAlign.Center, 0, 0.86), new(0, 0.9), new(0.06, 1.14, 1.03), TemperaMood.Quiet, true),
        ["grid-cells"] = new(new(0.5, 0.5, 0.72, 0.22, TemperaAlign.Center, 0, 0.8), new(0.7, 0.7), new(0.07, 1.06, 1.13), TemperaMood.Neutral, true),
        ["keyhole"] = new(new(0.5, 0.6, 0.42, 0.3, TemperaAlign.Center, 0, 0.78), new(0, 1), new(0.05, 1.12, 1.02), TemperaMood.Quiet, true),
        ["poster-panel"] = new(new(0.4, 0.5, 0.58, 0.62, TemperaAlign.Left, -0.045, 1), new(-1.5, 0.35), new(0.08, 1.05, 1.12), TemperaMood.Loud, true),
        ["diamond-stack"] = new(new(0.54, 0.5, 0.6, 0.42, TemperaAlign.Right, 0.035, 1), new(1.3, -0.5), new(0.09, 1.07, 1.15), TemperaMood.Loud, true),
        ["slash-poster"] = new(new(0.46, 0.5, 0.66, 0.44, TemperaAlign.Left, -0.09, 1), new(-1.2, 1), new(0.11, 1.06, 1.14), TemperaMood.Loud, true),
        ["arrow-wedge"] = new(new(0.5, 0.34, 0.72, 0.26, TemperaAlign.Center, 0, 1), new(0, -1.2), new(0.13, 1.04, 1.13), TemperaMood.Loud, true),
        ["edge-bleed"] = new(new(0.56, 0.5, 0.6, 0.44, TemperaAlign.Left, 0, 1), new(1.4, 0.3), new(0.1, 1.05, 1.13), TemperaMood.Neutral, true),
        ["triangle-mass"] = new(new(0.5, 0.42, 0.68, 0.3, TemperaAlign.Center, 0, 1), new(0.8, -1), new(0.12, 1.05, 1.14), TemperaMood.Loud, true),
        ["ribbon-cross"] = new(new(0.5, 0.5, 0.62, 0.3, TemperaAlign.Center, 0.05, 1), new(-1, -0.8), new(0.11, 1.08, 1.16), TemperaMood.Loud, true),
        ["half-disc"] = new(new(0.44, 0.44, 0.6, 0.34, TemperaAlign.Left, 0, 1), new(-1.3, 0.4), new(0.1, 1.06, 1.14), TemperaMood.Loud, true),
        ["stacked-slabs"] = new(new(0.52, 0.5, 0.62, 0.4, TemperaAlign.Center, -0.05, 1), new(1.1, 0.6), new(0.1, 1.07, 1.15), TemperaMood.Loud, true),
        ["wedge-pair"] = new(new(0.5, 0.5, 0.44, 0.44, TemperaAlign.Center, 0, 0.82), new(0, 1.1), new(0.09, 1.1, 1.02), TemperaMood.Loud, true),
        ["quiet-line"] = new(new(0.5, 0.5, 0.6, 0.28, TemperaAlign.Center, 0, 0.58), new(0, 0.7), new(0.03, 1, 1.04), TemperaMood.Quiet, true),
        ["starfield-dots"] = new(new(0.5, 0.5, 0.62, 0.26, TemperaAlign.Center, 0, 0.66), new(0.4, 0.5), new(0.04, 1.02, 1.08), TemperaMood.Quiet, true),
        ["ripple-lines"] = new(new(0.5, 0.46, 0.66, 0.28, TemperaAlign.Center, 0, 0.72), new(0, 0.9), new(0.06, 1.03, 1.1), TemperaMood.Quiet, true),
        ["hair-grid"] = new(new(0.5, 0.5, 0.64, 0.26, TemperaAlign.Center, 0, 0.62), new(0.4, 0.6), new(0.04, 1.01, 1.06), TemperaMood.Quiet, true),
        ["margin-rule"] = new(new(0.54, 0.5, 0.62, 0.26, TemperaAlign.Left, 0, 0.66), new(-0.8, 0.3), new(0.05, 1.02, 1.07), TemperaMood.Quiet, true),
        ["dot-drift"] = new(new(0.5, 0.48, 0.6, 0.26, TemperaAlign.Center, 0, 0.68), new(0.6, 0.6), new(0.05, 1.03, 1.09), TemperaMood.Quiet, true),
        ["arc-sweep"] = new(new(0.5, 0.5, 0.6, 0.26, TemperaAlign.Center, 0, 0.7), new(0, 0.8), new(0.06, 1.04, 1.1), TemperaMood.Quiet, true),
        ["blank-page"] = new(new(0.5, 0.5, 0.56, 0.24, TemperaAlign.Center, 0, 0.6), new(0, 0.5), new(0.03, 1, 1.03), TemperaMood.Quiet, true),
        ["cinema-scope"] = new(new(0.5, 0.5, 0.74, 0.22, TemperaAlign.Center, 0, 0.86), new(0.9, 0), new(0.09, 1.04, 1.11), TemperaMood.Neutral, true),
        ["cinema-wide"] = new(new(0.5, 0.5, 0.7, 0.3, TemperaAlign.Center, 0, 0.9), new(0, 0.9), new(0.08, 1.06, 1.13), TemperaMood.Neutral, true),
        ["cinema-academy"] = new(new(0.5, 0.5, 0.5, 0.4, TemperaAlign.Center, 0, 0.88), new(0, 0.8), new(0.06, 1.1, 1.02), TemperaMood.Quiet, true),
        ["cinema-square"] = new(new(0.5, 0.5, 0.42, 0.4, TemperaAlign.Center, 0, 0.84), new(0.7, 0.7), new(0.05, 1.12, 1.03), TemperaMood.Quiet, true),
        ["cinema-portrait"] = new(new(0.5, 0.5, 0.32, 0.46, TemperaAlign.Center, 0, 0.8), new(0, 1), new(0.06, 1.08, 1.16), TemperaMood.Neutral, true),
        ["cinema-tall"] = new(new(0.5, 0.5, 0.24, 0.5, TemperaAlign.Center, 0, 0.72), new(0.8, 0.4), new(0.07, 1.05, 1.14), TemperaMood.Neutral, true),
        ["cinema-twin"] = new(new(0.33, 0.5, 0.3, 0.32, TemperaAlign.Center, 0, 0.76), new(-0.9, 0.3), new(0.08, 1.06, 1.13), TemperaMood.Neutral, true),
        ["bubble-drift"] = new(new(0.5, 0.5, 0.72, 0.36, TemperaAlign.Center, 0, 0.92), new(0.5, 1), new(0.09, 1.05, 1.12), TemperaMood.Neutral, true),
        ["cloud-window"] = new(new(0.5, 0.52, 0.46, 0.3, TemperaAlign.Center, 0, 0.84), new(0, 0.8), new(0.05, 1.14, 1.03), TemperaMood.Quiet, true),
        ["heart-burst"] = new(new(0.5, 0.52, 0.5, 0.3, TemperaAlign.Center, 0, 0.95), new(0.8, 0.8), new(0.1, 1.08, 1.16), TemperaMood.Loud, true),
        ["sparkle-field"] = new(new(0.5, 0.48, 0.64, 0.26, TemperaAlign.Center, 0, 0.7), new(0.4, 0.6), new(0.04, 1.02, 1.08), TemperaMood.Quiet, true),
        ["petal-arc"] = new(new(0.5, 0.6, 0.7, 0.3, TemperaAlign.Center, 0, 0.9), new(0, 1.1), new(0.1, 1.04, 1.12), TemperaMood.Neutral, true),
        ["scallop-band"] = new(new(0.5, 0.5, 0.76, 0.24, TemperaAlign.Center, 0, 0.88), new(0.7, 0), new(0.12, 1.03, 1.1), TemperaMood.Neutral, true),
        ["ribbon-loop"] = new(new(0.5, 0.5, 0.68, 0.22, TemperaAlign.Center, 0.04, 0.86), new(-1.1, 0.6), new(0.11, 1.07, 1.15), TemperaMood.Loud, true),
        ["round-plate"] = new(new(0.5, 0.5, 0.56, 0.36, TemperaAlign.Center, 0, 0.86), new(0, 0.7), new(0.05, 1.1, 1.02), TemperaMood.Quiet, true),
        ["halo-burst"] = new(new(0.5, 0.5, 0.46, 0.3, TemperaAlign.Center, 0, 0.95), new(0, 0.9), new(0.08, 1.12, 1.02), TemperaMood.Loud, true),
        ["iris-hole"] = new(new(0.28, 0.68, 0.44, 0.28, TemperaAlign.Center, 0, 0.78), new(-1, 0.6), new(0.09, 1.06, 1.14), TemperaMood.Loud, true),
        ["slot-rail"] = new(new(0.5, 0.66, 0.72, 0.3, TemperaAlign.Center, 0, 0.9), new(0.6, 0.9), new(0.12, 1.04, 1.11), TemperaMood.Neutral, true),
        ["punch-row"] = new(new(0.5, 0.5, 0.76, 0.34, TemperaAlign.Center, 0, 0.92), new(1.1, 0), new(0.13, 1.03, 1.1), TemperaMood.Neutral, true),
        ["film-gate"] = new(new(0.5, 0.82, 0.66, 0.2, TemperaAlign.Center, 0, 0.8), new(0, 1.1), new(0.08, 1.08, 1.02), TemperaMood.Loud, true),
        ["cross-vent"] = new(new(0.5, 0.82, 0.66, 0.2, TemperaAlign.Center, 0, 0.8), new(0, 1), new(0.07, 1.1, 1.02), TemperaMood.Loud, true),
        ["louvre-slats"] = new(new(0.5, 0.8, 0.7, 0.22, TemperaAlign.Center, 0, 0.82), new(0.8, 0.7), new(0.11, 1.05, 1.13), TemperaMood.Neutral, true),
        ["ring-eye"] = new(new(0.5, 0.5, 0.26, 0.18, TemperaAlign.Center, 0, 0.62), new(0, 0.7), new(0.05, 1.14, 1.03), TemperaMood.Quiet, true),
        ["notch-stack"] = new(new(0.32, 0.5, 0.5, 0.34, TemperaAlign.Center, 0, 0.84), new(-1.1, 0.5), new(0.1, 1.05, 1.13), TemperaMood.Neutral, true),
        ["wedge-gap"] = new(new(0.5, 0.78, 0.7, 0.24, TemperaAlign.Center, 0, 0.82), new(0, 1.2), new(0.12, 1.06, 1.14), TemperaMood.Loud, true),
        ["dot-sieve"] = new(new(0.75, 0.5, 0.38, 0.3, TemperaAlign.Center, 0, 0.78), new(0.9, 0.4), new(0.06, 1.03, 1.09), TemperaMood.Quiet, true),
        ["sight-mark"] = new(new(0.5, 0.5, 0.46, 0.2, TemperaAlign.Center, 0, 0.85), new(0, 0.8), new(0.06, 1.12, 1.02), TemperaMood.Loud, true),
        ["dial-scale"] = new(new(0.5, 0.5, 0.36, 0.2, TemperaAlign.Center, 0, 0.8), new(0.7, 0.5), new(0.05, 1.1, 1.02), TemperaMood.Neutral, true),
        ["chevron-run"] = new(new(0.5, 0.52, 0.72, 0.3, TemperaAlign.Center, 0, 0.9), new(0, -1.1), new(0.14, 1.04, 1.12), TemperaMood.Neutral, true),
        ["tally-column"] = new(new(0.38, 0.5, 0.6, 0.32, TemperaAlign.Left, 0, 0.76), new(-0.9, 0.3), new(0.05, 1.02, 1.08), TemperaMood.Quiet, true),
        ["grid-focus"] = new(new(0.5, 0.5, 0.62, 0.22, TemperaAlign.Center, 0, 0.85), new(0.8, 0.6), new(0.09, 1.05, 1.13), TemperaMood.Neutral, true),
        ["axis-caps"] = new(new(0.5, 0.72, 0.66, 0.24, TemperaAlign.Center, 0, 0.82), new(0, 1.2), new(0.13, 1.06, 1.15), TemperaMood.Loud, true),
        ["strobe-slats"] = new(new(0.5, 0.5, 0.3, 0.36, TemperaAlign.Center, 0, 0.8), new(1, 0), new(0.1, 1.08, 1.16), TemperaMood.Loud, true),
        ["offset-plate"] = new(new(0.48, 0.62, 0.4, 0.2, TemperaAlign.Center, 0, 0.85), new(-0.9, -0.6), new(0.09, 1.07, 1.15), TemperaMood.Loud, true),
        ["radial-comb"] = new(new(0.42, 0.4, 0.62, 0.3, TemperaAlign.Center, 0, 0.88), new(-1.2, -0.4), new(0.11, 1.05, 1.13), TemperaMood.Neutral, true),
        ["bracket-target"] = new(new(0.5, 0.28, 0.66, 0.24, TemperaAlign.Center, 0, 0.85), new(0, -0.9), new(0.06, 1.04, 1.1), TemperaMood.Quiet, true),
        ["flow-channel"] = new(new(0.32, 0.5, 0.5, 0.3, TemperaAlign.Center, 0, 0.85), new(-1, 0.5), new(0.13, 1.04, 1.12), TemperaMood.Neutral, true),
        ["twin-channel"] = new(new(0.5, 0.5, 0.4, 0.3, TemperaAlign.Center, 0, 0.85), new(0, 1.2), new(0.14, 1.03, 1.11), TemperaMood.Neutral, true),
        ["reed-run"] = new(new(0.5, 0.5, 0.3, 0.3, TemperaAlign.Center, 0, 0.72), new(0, 0.9), new(0.1, 1.02, 1.09), TemperaMood.Quiet, true),
        ["taper-channel"] = new(new(0.28, 0.5, 0.42, 0.3, TemperaAlign.Center, 0, 0.82), new(-1.1, 0.6), new(0.12, 1.05, 1.14), TemperaMood.Neutral, true),
        ["chain-ports"] = new(new(0.32, 0.5, 0.5, 0.32, TemperaAlign.Center, 0, 0.8), new(-0.8, 0.7), new(0.11, 1.03, 1.1), TemperaMood.Quiet, true),
        ["dash-channel"] = new(new(0.3, 0.5, 0.46, 0.3, TemperaAlign.Center, 0, 0.84), new(0, 1.3), new(0.15, 1.04, 1.13), TemperaMood.Neutral, true),
        ["window-run"] = new(new(0.28, 0.5, 0.42, 0.3, TemperaAlign.Center, 0, 0.82), new(0, 1.2), new(0.14, 1.04, 1.12), TemperaMood.Neutral, true),
        ["bridge-span"] = new(new(0.5, 0.5, 0.72, 0.16, TemperaAlign.Center, 0, 0.85), new(1.2, 0), new(0.12, 1.08, 1.16), TemperaMood.Loud, true),
        ["braid-channel"] = new(new(0.5, 0.5, 0.34, 0.3, TemperaAlign.Center, 0, 0.8), new(0, 1.1), new(0.13, 1.06, 1.15), TemperaMood.Loud, true),
        ["port-ladder"] = new(new(0.3, 0.5, 0.46, 0.3, TemperaAlign.Center, 0, 0.78), new(-0.9, 0.4), new(0.09, 1.02, 1.09), TemperaMood.Quiet, true),
        ["apex-mass"] = new(new(0.5, 0.3, 0.62, 0.2, TemperaAlign.Center, 0, 0.9), new(0, -1), new(0.12, 1.04, 1.13), TemperaMood.Loud, true),
        ["ziggurat"] = new(new(0.5, 0.32, 0.5, 0.18, TemperaAlign.Center, 0, 0.85), new(0, -0.9), new(0.1, 1.06, 1.15), TemperaMood.Loud, true),
        ["slab-wall"] = new(new(0.36, 0.5, 0.5, 0.24, TemperaAlign.Center, 0, 0.88), new(1.2, 0), new(0.11, 1.05, 1.12), TemperaMood.Neutral, true),
        ["cantilever"] = new(new(0.42, 0.42, 0.6, 0.14, TemperaAlign.Center, 0, 0.8), new(-1.3, 0), new(0.13, 1.04, 1.12), TemperaMood.Loud, true),
        ["pylon-pair"] = new(new(0.5, 0.22, 0.7, 0.12, TemperaAlign.Center, 0, 0.75), new(0, -0.8), new(0.09, 1.08, 1.02), TemperaMood.Loud, true),
        ["bunker-slit"] = new(new(0.5, 0.48, 0.72, 0.2, TemperaAlign.Center, 0, 0.86), new(0.9, 0.4), new(0.08, 1.06, 1.13), TemperaMood.Neutral, true),
        ["plinth-stack"] = new(new(0.5, 0.32, 0.44, 0.16, TemperaAlign.Center, 0, 0.82), new(0, -0.8), new(0.1, 1.05, 1.13), TemperaMood.Neutral, true),
        ["buttress-run"] = new(new(0.5, 0.26, 0.7, 0.2, TemperaAlign.Center, 0, 0.86), new(0.8, -0.8), new(0.12, 1.03, 1.11), TemperaMood.Loud, true),
        ["void-core"] = new(new(0.5, 0.2, 0.66, 0.18, TemperaAlign.Center, 0, 0.84), new(0, -1.1), new(0.11, 1.07, 1.15), TemperaMood.Loud, true),
        ["shear-block"] = new(new(0.5, 0.5, 0.56, 0.2, TemperaAlign.Center, 0, 0.88), new(1, 0.5), new(0.12, 1.06, 1.14), TemperaMood.Neutral, true),
        ["ridge-line"] = new(new(0.5, 0.42, 0.72, 0.2, TemperaAlign.Center, 0, 0.9), new(0, -1), new(0.14, 1.03, 1.12), TemperaMood.Neutral, true),
        ["chasm"] = new(new(0.25, 0.5, 0.38, 0.24, TemperaAlign.Center, 0, 0.82), new(-1.2, 0.4), new(0.1, 1.06, 1.14), TemperaMood.Loud, true),
        ["overhang"] = new(new(0.44, 0.58, 0.6, 0.22, TemperaAlign.Center, 0, 0.88), new(0, 1.1), new(0.11, 1.08, 1.02), TemperaMood.Loud, true),
        ["step-well"] = new(new(0.5, 0.26, 0.66, 0.2, TemperaAlign.Center, 0, 0.85), new(0, -0.9), new(0.09, 1.04, 1.12), TemperaMood.Neutral, true),
        ["pier-row"] = new(new(0.5, 0.4, 0.76, 0.14, TemperaAlign.Center, 0, 0.82), new(1.1, 0), new(0.13, 1.03, 1.1), TemperaMood.Neutral, true),
        ["revetment"] = new(new(0.46, 0.3, 0.6, 0.2, TemperaAlign.Center, 0, 0.86), new(-1, -0.6), new(0.12, 1.05, 1.13), TemperaMood.Neutral, true),
        ["tower-crop"] = new(new(0.28, 0.5, 0.44, 0.24, TemperaAlign.Left, 0, 0.74), new(-0.8, 0.3), new(0.06, 1.02, 1.09), TemperaMood.Quiet, true),
        ["lintel"] = new(new(0.5, 0.5, 0.76, 0.16, TemperaAlign.Center, 0, 0.9), new(-1.1, 0), new(0.1, 1.02, 1.08), TemperaMood.Quiet, true),
        ["rubble-fan"] = new(new(0.56, 0.4, 0.5, 0.22, TemperaAlign.Center, 0, 0.84), new(1.2, -0.5), new(0.13, 1.07, 1.16), TemperaMood.Loud, true),
        ["gnomon"] = new(new(0.66, 0.34, 0.46, 0.2, TemperaAlign.Center, 0, 0.78), new(0.9, -0.4), new(0.07, 1.03, 1.1), TemperaMood.Quiet, true),
        ["monogatari-card"] = new(new(0.5, 0.5, 0.78, 0.5, TemperaAlign.Center, 0, 1.15), new(0, 0.7), new(0.03, 1.02, 1.07), TemperaMood.Quiet, false),
        ["monogatari-rule"] = new(new(0.5, 0.46, 0.76, 0.4, TemperaAlign.Center, 0, 1.05), new(0.6, 0), new(0.04, 1.03, 1.09), TemperaMood.Quiet, false),
        ["monogatari-edge"] = new(new(0.52, 0.5, 0.72, 0.46, TemperaAlign.Left, 0, 1.05), new(-0.8, 0), new(0.05, 1.02, 1.08), TemperaMood.Neutral, false),
        ["monogatari-stack"] = new(new(0.5, 0.5, 0.44, 0.62, TemperaAlign.Center, 0, 1), new(0, 0.9), new(0.04, 1.05, 1.12), TemperaMood.Quiet, false),
        ["monogatari-flash"] = new(new(0.5, 0.5, 0.82, 0.44, TemperaAlign.Center, 0, 1.3), new(0, 0.5), new(0.02, 1.08, 1.01), TemperaMood.Loud, false),
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> CandidateCache = [];

    public static TemperaShotProfile Resolve(string kind) =>
        Profiles.TryGetValue(kind, out var profile) ? profile : Profiles["duo-split"];

    /// <summary>Compositions a paragraph of this character may cut to, in TEMPERA_SHOT_KINDS order.</summary>
    public static IReadOnlyList<string> Candidates(params TemperaMood[] moods)
    {
        var key = string.Join(',', moods);
        lock (CandidateCache)
        {
            if (CandidateCache.TryGetValue(key, out var cached)) return cached;
            var candidates = Kinds.Where(kind => moods.Contains(Resolve(kind).Mood)).ToArray();
            IReadOnlyList<string> result = candidates.Length > 0 ? candidates : Kinds;
            CandidateCache[key] = result;
            return result;
        }
    }
}
