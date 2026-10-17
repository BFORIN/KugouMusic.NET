using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia temperaCompositions.ts: the registry of every shot kind's drawer plus the two layers every kind shares — the
/// full-bleed crossing lines and the compile-time motif in one corner.
/// </summary>
internal static class TemperaCompositions
{
    private static readonly Dictionary<string, Action<TemperaCompositionContext>> Registry = Build();

    private static Dictionary<string, Action<TemperaCompositionContext>> Build()
    {
        var registry = new Dictionary<string, Action<TemperaCompositionContext>>();
        TemperaSplitCompositions.Register(registry);
        TemperaBandCompositions.Register(registry);
        TemperaFrameCompositions.Register(registry);
        TemperaPosterCompositions.Register(registry);
        TemperaSparseCompositions.Register(registry);
        TemperaCinemaCompositions.Register(registry);
        TemperaCharmCompositions.Register(registry);
        TemperaApertureCompositions.Register(registry);
        TemperaSignalCompositions.Register(registry);
        TemperaCorridorCompositions.Register(registry);
        TemperaMonolithCompositions.Register(registry);
        TemperaMonogatariCompositions.Register(registry);
        return registry;
    }

    public static IReadOnlyCollection<string> RegisteredKinds => Registry.Keys;

    public static Action<TemperaCompositionContext> Resolve(string kind) =>
        Registry.TryGetValue(kind, out var drawer) ? drawer : Registry["duo-split"];

    private static void AddCrossingLines(TemperaCompositionContext ctx)
    {
        var lines = H.CrossingLines(ctx.Seed, 31, ctx.Width, ctx.Height, ctx.Decor.CrossCount);
        if (lines.Count == 0) return;
        ctx.Add(S.Lines(lines, ctx.Palette.Tone4, 1.3, 0.6), O(delay: 0.14, span: 0.6, enterDX: ctx.Width * 0.25));
    }

    private static void AddMotif(TemperaCompositionContext ctx)
    {
        var (width, height, palette, decor) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Decor);
        var cornerX = Hash01(ctx.Seed, 61, 7) > 0.5 ? width * 0.14 : width * 0.86;
        var cornerY = Hash01(ctx.Seed, 63, 7) > 0.5 ? height * 0.18 : height * 0.82;
        switch (decor.Motif)
        {
            case TemperaDecorMotif.Diamonds:
                ctx.Add(S.ConcentricDiamonds(cornerX, cornerY, 34, 34, 3, palette.Tone4, 0.8), O(delay: 0.34, drift: true));
                return;
            case TemperaDecorMotif.HatchTwin:
            {
                var spec = H.Spec(ctx.Seed, 67) with { Angle = decor.HatchAngle };
                for (var index = 0; index < 2; index++)
                {
                    var box = H.Rect(cornerX - 34 + index * 46, cornerY - 26, 38, 38);
                    ctx.Add(S.HatchFill(box, index == 0 ? spec : spec with { Spacing = spec.Spacing * 0.55 }, palette.Tone4, 0.75),
                        O(delay: 0.34 + index * 0.05, grow: true));
                    ctx.Add(S.PolygonOutline(box, palette.Tone4, 1.2, 0.6), O(delay: 0.38 + index * 0.05));
                }
                return;
            }
            case TemperaDecorMotif.BandCross:
                ctx.Add(S.CrossMarks(H.CrossRow(ctx.Seed, 71, cornerX - width * 0.1, cornerY, 5, width * 0.05, 8, decor.HatchAngle * 0.4),
                    palette.Tone4, 2, 0.8), O(delay: 0.34, span: 0.5));
                return;
            case TemperaDecorMotif.PosterDiamond:
                // Deliberately parked past the edge so the shape bleeds off frame.
                ctx.Add(S.PolygonFill(H.Diamond(cornerX < width / 2 ? -width * 0.04 : width * 1.04, cornerY, width * 0.14, height * 0.2),
                    palette.Tone3, 0.85), O(delay: 0.32, span: 0.55, enterDX: (cornerX < width / 2 ? -1 : 1) * width * 0.1));
                return;
            default:
                ctx.Add(S.Polyline(H.Scribble(decor.ScribbleSeed, 73, cornerX, cornerY, Math.Min(width, height) * 0.08, 3), palette.Tone4, 1.5, 0.72),
                    O(delay: 0.34, span: 0.6));
                return;
        }
    }

    public static void Draw(TemperaCompositionContext ctx)
    {
        Resolve(ctx.Kind)(ctx);
        // Interstitial cards are a bare field by definition; the shared overlays would undo them.
        if (!TemperaShotProfiles.Resolve(ctx.Kind).SharedDecor) return;
        AddCrossingLines(ctx);
        if (ctx.ShowDecor) AddMotif(ctx);
    }
}
