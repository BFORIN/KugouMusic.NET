using AvaloniaSilkEffects.Tempera.Rendering;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia TemperaBlockOptions: entrance offsets, delay / span as fractions of the shot (resolved to seconds by the blocks),
/// the slow deterministic float, and the horizontal open used by hatch reveals.
/// </summary>
internal readonly record struct TemperaBlockOptions(
    double Alpha = 1,
    double EnterDX = 0,
    double EnterDY = 0,
    double Delay = 0,
    double Span = 0.45,
    bool Drift = false,
    bool Grow = false);

/// <summary>Folia temperaCompositionContext.ts: the drawing contract shared by every composition family.</summary>
internal sealed class TemperaCompositionContext
{
    public required string Kind { get; init; }
    public required TemperaPalette Palette { get; init; }
    public required TemperaDecorSpec Decor { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public long Seed { get; init; }
    public bool ShowDecor { get; init; }
    /// <summary>Extra margin every full-bleed shape extends past the viewport, because the shot slides along its flow.</summary>
    public double Bleed { get; init; }
    public double FlowAngle { get; init; }
    /// <summary>Non-null only in gradient colour mode.</summary>
    public TemperaGradientFill? Gradient { get; init; }
    public required Action<TemperaGraphic, TemperaBlockOptions, TemperaGroup?> AddNode { get; init; }
    public required Func<double, double, double, TemperaGroup> CreateGroupNode { get; init; }

    public void Add(TemperaGraphic node, TemperaBlockOptions? options = null, TemperaGroup? parent = null) =>
        AddNode(node, options ?? TemperaBlock.O(), parent);

    public TemperaGroup CreateGroup(double rotation, double x, double y) => CreateGroupNode(rotation, x, y);
}

/// <summary>Shorthand so the composition bodies read like their TypeScript originals.</summary>
internal static class TemperaBlock
{
    public static TemperaBlockOptions O(double delay = 0, double span = 0.45, double enterDX = 0, double enterDY = 0,
        bool drift = false, bool grow = false, double alpha = 1) =>
        new(alpha, enterDX, enterDY, delay, span, drift, grow);
}
