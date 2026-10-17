using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaCutout;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaMonolithKit;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using C = AvaloniaSilkEffects.Tempera.Geometry.TemperaCurves;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaMonolithCompositions.ts + temperaTerrainCompositions.ts: brutalist masses and ground.</summary>
internal static class TemperaMonolithCompositions
{
    private static void ApexMass(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var apexX = width * (0.46 + Hash01(ctx.Seed, 3, 227) * 0.12);
        var apexY = height * 0.38;
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, [-bleed, height + bleed, apexX, apexY, width + bleed, height + bleed], palette.Tone3, enterDY: height * 0.1);
        AddFaceRuling(ctx, [apexX, apexY, width + bleed, height + bleed, apexX + width * 0.24, height + bleed], 229);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
        AddWireTrace(ctx, width * 0.12, height * 0.84, Math.Min(width, height) * 0.09);
    }

    private static void Ziggurat(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        TemperaRgb[] tones = [palette.Tone4, palette.Tone3, palette.Tone3, palette.Tone2];
        for (var course = 0; course < 4; course++)
        {
            var half = width * (0.44 - course * 0.09);
            var top = height * (0.9 - course * 0.13) - height * 0.13;
            AddMass(ctx, H.Rect(width / 2 - half, top, half * 2, height * 0.13 + bleed), tones[course], delay: 0.04 + course * 0.05,
                enterDY: height * 0.08, edgeWidth: 2);
        }
        AddSurveyLines(ctx, 2);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
    }

    private static void SlabWall(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var edge = width * 0.36;
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, H.Rect(edge, -bleed, width - edge + bleed, height + bleed * 2), palette.Tone3, edge: false);
        ctx.Add(S.Lines([new TemperaLine(edge, -bleed, edge, height + bleed)], palette.Ink, 3.5, 0.9), O(delay: 0.1, span: 0.55, enterDX: width * 0.06));
        AddFaceRuling(ctx, H.Rect(width * 0.62, height * 0.1, width * 0.3, height * 0.8), 233);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddWireTrace(ctx, width * 0.16, height * 0.24, Math.Min(width, height) * 0.08);
    }

    private static void Cantilever(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, H.Rect(0.16 * width, height * 0.5, width * 0.1, height * 0.5 + bleed), palette.Tone4, delay: 0.02, enterDY: height * 0.1,
            edge: false);
        AddMass(ctx, H.Rect(-bleed, height * 0.34, width * 0.78 + bleed, height * 0.16), palette.Tone3, delay: 0.08, enterDX: -width * 0.14);
        AddFaceRuling(ctx, H.Rect(width * 0.3, height * 0.36, width * 0.44, height * 0.12), 239, palette.Paper, 0.35);
        AddSurveyLines(ctx, 2);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
    }

    private static void PylonPair(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var bay = H.Rect(width * 0.26, height * 0.3, width * 0.48, height * 0.7 + bleed);
        AddCutField(ctx, palette.Tone1, [bay], alpha: 0.94);
        AddHoleLip(ctx, bay, 2, 0.6, 0.12);
        double[] pylons = [0.1, 0.74];
        for (var index = 0; index < pylons.Length; index++)
            AddMass(ctx, H.Rect(width * pylons[index], height * 0.28, width * 0.16, height * 0.72 + bleed), palette.Tone4,
                delay: 0.04 + index * 0.05, enterDY: height * 0.08, edgeWidth: 2);
        AddMass(ctx, H.Rect(width * 0.04, height * 0.14, width * 0.92, height * 0.16), palette.Tone3, delay: 0.14, enterDY: -height * 0.1);
        AddSurveyLines(ctx, 2);
    }

    private static void BunkerSlit(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var slit = H.Rect(-bleed, height * 0.62, width + bleed * 2, height * 0.06);
        AddCutField(ctx, palette.Tone1, [slit], alpha: 0.94);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, height * 0.34, width + bleed * 2, height * 0.28), palette.Tone4, 0.95, ctx.Gradient),
            O(delay: 0.05, span: 0.6, enterDY: -height * 0.08));
        ctx.Add(S.PolygonFill(H.Rect(-bleed, height * 0.68, width + bleed * 2, height * 0.32 + bleed), palette.Tone4, 0.95, ctx.Gradient),
            O(delay: 0.08, span: 0.6, enterDY: height * 0.1));
        ctx.Add(S.Lines([new TemperaLine(-bleed, height * 0.34, width + bleed, height * 0.34)], palette.Ink, 3, 0.85), O(delay: 0.14, span: 0.5));
        AddFaceRuling(ctx, H.Rect(width * 0.08, height * 0.72, width * 0.36, height * 0.2), 241, palette.Paper, 0.3);
        AddSurveyLines(ctx, 2);
    }

    private static void PlinthStack(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, H.Rect(width * 0.06, height * 0.72, width * 0.88, height * 0.28 + bleed), palette.Tone4, edgeWidth: 2);
        AddMass(ctx, H.Rect(width * 0.16, height * 0.52, width * 0.68, height * 0.2), palette.Tone3, delay: 0.08, edgeWidth: 2);
        AddMass(ctx, H.Rect(width * 0.28, height * 0.34, width * 0.44, height * 0.18), palette.Tone2, delay: 0.14, enterDY: height * 0.06);
        AddFaceRuling(ctx, H.Rect(width * 0.2, height * 0.76, width * 0.6, height * 0.16), 243);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
    }

    private static void ButtressRun(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var band = H.Rect(-bleed, height * 0.42, width + bleed * 2, height * 0.58 + bleed);
        AddCutField(ctx, palette.Tone1, [band], alpha: 0.94);
        for (var index = 0; index < 5; index++)
        {
            var left = width * (index * 0.2 + 0.01);
            AddMass(ctx, [left, height + bleed, left + width * 0.09, height * 0.42, left + width * 0.17, height + bleed],
                index % 2 == 0 ? palette.Tone4 : palette.Tone3, delay: 0.04 + index * 0.04, enterDY: height * 0.12, edgeWidth: 2);
        }
        ctx.Add(S.Lines([new TemperaLine(-bleed, height * 0.42, width + bleed, height * 0.42)], palette.Ink, 2.4, 0.8),
            O(delay: 0.2, span: 0.5, enterDX: -width * 0.1));
        AddSurveyLines(ctx, 2);
    }

    private static void VoidCore(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var core = H.Rect(width * 0.22, height * 0.34, width * 0.56, height * 0.46);
        AddCutField(ctx, palette.Tone3, [core], alpha: 0.95);
        AddHoleLip(ctx, core, 3.5, 0.9, 0.08);
        AddFaceRuling(ctx, H.Rect(width * 0.04, height * 0.36, width * 0.14, height * 0.42), 247, palette.Tone4, 0.45);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
        AddWireTrace(ctx, width * 0.86, height * 0.86, Math.Min(width, height) * 0.08);
    }

    private static void ShearBlock(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var left = width * 0.14;
        var right = width * 0.86;
        var top = height * 0.2;
        var bottom = height * 0.8;
        var cutTop = width * 0.56;
        var cutBottom = width * 0.44;
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, [left, top, cutTop, top, cutBottom, bottom, left, bottom], palette.Tone3, enterDX: -width * 0.08, enterDY: -height * 0.03,
            edgeWidth: 2);
        AddMass(ctx, [cutTop + width * 0.03, top + height * 0.04, right, top + height * 0.04, right, bottom + height * 0.04,
            cutBottom + width * 0.03, bottom + height * 0.04], palette.Tone4, delay: 0.1, enterDX: width * 0.08, enterDY: height * 0.03, edgeWidth: 2);
        AddSurveyLines(ctx, 2);
        if (!ctx.ShowDecor) return;
        ctx.Add(S.PolygonOutline(H.Rect(width * 0.1, height * 0.16, width * 0.8, height * 0.68), palette.Paper, 1.2, 0.35), O(delay: 0.28, span: 0.55));
    }

    // Terrain: the same language read as ground and structure.

    private static void RidgeLine(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var lift = Hash01(ctx.Seed, 5, 251) * 0.06;
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, [
            -bleed, height * (0.68 + lift),
            width * 0.34, height * (0.4 + lift),
            width * 0.62, height * (0.58 + lift),
            width + bleed, height * (0.34 + lift),
            width + bleed, height + bleed,
            -bleed, height + bleed,
        ], palette.Tone3, enterDY: height * 0.08);
        AddFaceRuling(ctx, [
            width * 0.34, height * (0.42 + lift),
            width * 0.62, height * (0.6 + lift),
            width * 0.62, height + bleed,
            width * 0.34, height + bleed,
        ], 253, palette.Tone4, 0.45);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
        AddWireTrace(ctx, width * 0.1, height * 0.86, Math.Min(width, height) * 0.08);
    }

    private static void Chasm(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var gap = H.Rect(width * 0.46, -bleed, width * 0.1, height + bleed * 2);
        AddCutField(ctx, palette.Tone1, [gap], alpha: 0.94);
        AddMass(ctx, H.Rect(-bleed, height * 0.3, width * 0.46 + bleed, height * 0.7 + bleed), palette.Tone3, enterDX: -width * 0.1, edgeWidth: 2);
        AddMass(ctx, H.Rect(width * 0.56, height * 0.2, width * 0.44 + bleed, height * 0.8 + bleed), palette.Tone4, delay: 0.08,
            enterDX: width * 0.1, edgeWidth: 2);
        AddSurveyLines(ctx, 2);
    }

    private static void Overhang(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, H.Rect(-bleed, -bleed, width + bleed * 2, height * 0.34 + bleed), palette.Tone4, enterDY: -height * 0.1, edgeWidth: 3);
        AddMass(ctx, H.Rect(width * 0.72, height * 0.34, width * 0.28 + bleed, height * 0.66 + bleed), palette.Tone3, delay: 0.08,
            enterDX: width * 0.1, edge: false);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, height * 0.34, width + bleed * 2, height * 0.14), palette.Tone2, 0.5, ctx.Gradient),
            O(delay: 0.16, span: 0.6));
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
    }

    private static void StepWell(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var shaft = H.Rect(width * 0.36, height * 0.6, width * 0.28, height * 0.4 + bleed);
        AddCutField(ctx, palette.Tone2, [shaft], alpha: 0.94);
        (double X, double Y, double W, TemperaRgb Tone)[] courses = [(0.14, 0.3, 0.72, palette.Tone3), (0.24, 0.44, 0.52, palette.Tone4)];
        for (var index = 0; index < courses.Length; index++)
        {
            var (x, y, w, tone) = courses[index];
            ctx.Add(S.PolygonFillWithHoles(H.Rect(width * x, height * y, width * w, height * (1 - y) + bleed), [shaft], tone, 0.95, ctx.Gradient),
                O(delay: 0.06 + index * 0.06, span: 0.6, enterDY: height * 0.06));
        }
        AddHoleLip(ctx, shaft, 3, 0.85, 0.18);
        AddSurveyLines(ctx, 2);
    }

    private static void PierRow(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var water = H.Rect(-bleed, height * 0.46, width + bleed * 2, height * 0.54 + bleed);
        AddCutField(ctx, palette.Tone1, [water], alpha: 0.94);
        double[] piers = [0.04, 0.28, 0.52, 0.76];
        for (var index = 0; index < piers.Length; index++)
            AddMass(ctx, H.Rect(width * piers[index], height * 0.46, width * 0.14, height * 0.54 + bleed), palette.Tone4,
                delay: 0.06 + index * 0.04, enterDY: height * 0.1, edge: false);
        AddMass(ctx, H.Rect(-bleed, height * 0.32, width + bleed * 2, height * 0.16), palette.Tone3, delay: 0.02, enterDX: -width * 0.12,
            edgeWidth: 2);
        AddSurveyLines(ctx, 2);
    }

    private static void Revetment(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, [
            -bleed, height * 0.72,
            width * 0.62, height * 0.3,
            width + bleed, height * 0.34,
            width + bleed, height + bleed,
            -bleed, height + bleed,
        ], palette.Tone3, enterDY: height * 0.1);
        var ribs = Enumerable.Range(0, 9).Select(index =>
        {
            var x = width * (0.04 + index * 0.11);
            var top = height * (0.72 - x / (width * 0.62) * 0.42);
            return new TemperaLine(x, Math.Max(top, height * 0.3) + height * 0.03, x - width * 0.03, height + bleed);
        });
        ctx.Add(S.Lines(ribs, palette.Tone4, 2.4, 0.5), O(delay: 0.16, span: 0.65));
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
    }

    private static void TowerCrop(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, H.Rect(width * 0.58, height * 0.18, width * 0.42 + bleed, height * 0.82 + bleed), palette.Tone4, enterDX: width * 0.08,
            enterDY: height * 0.06, edgeWidth: 3);
        AddFaceRuling(ctx, H.Rect(width * 0.62, height * 0.26, width * 0.12, height * 0.6), 257, palette.Paper, 0.3);
        AddSurveyLines(ctx, 2);
        if (!ctx.ShowDecor) return;
        AddWireTrace(ctx, width * 0.2, height * 0.7, Math.Min(width, height) * 0.1);
        AddCornerTicks(ctx);
    }

    private static void Lintel(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        AddMass(ctx, H.Rect(-bleed, height * 0.4, width + bleed * 2, height * 0.2), palette.Tone3, enterDX: -width * 0.16, edgeWidth: 3);
        double[] bearings = [0.18, 0.66];
        for (var index = 0; index < bearings.Length; index++)
            AddMass(ctx, H.Rect(width * bearings[index], height * 0.6, width * 0.16, height * 0.12), palette.Tone4, delay: 0.12 + index * 0.04,
                enterDY: height * 0.06, edge: false);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
    }

    private static void RubbleFan(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var hubX = -width * 0.12;
        var hubY = height * 1.12;
        AddGround(ctx, palette.Tone1);
        for (var index = 0; index < 5; index++)
        {
            var angle = -1.24 + index * 0.19 + Hash01(ctx.Seed, index, 259) * 0.05;
            var reach = Math.Sqrt(width * width + height * height) * (0.5 + index * 0.09);
            var slab = H.Rect(hubX + reach * 0.34, hubY - height * 0.06, reach * 0.5, height * 0.12 + index * 8);
            AddMass(ctx, C.Rotate(slab, hubX, hubY, angle), index % 2 == 0 ? palette.Tone3 : palette.Tone4, delay: 0.04 + index * 0.05,
                enterDX: -width * 0.06, enterDY: height * 0.06, edgeWidth: 2);
        }
        AddSurveyLines(ctx, 2);
    }

    private static void Gnomon(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddGround(ctx, palette.Tone1);
        ctx.Add(S.PolygonFill([
            width * 0.42, height * 0.86,
            width + bleed, height * 0.6,
            width + bleed, height * 0.78,
            width * 0.46, height * 0.94,
        ], palette.Tone2, 0.6, ctx.Gradient), O(delay: 0.1, span: 0.65, enterDX: width * 0.1));
        AddMass(ctx, [width * 0.3, height + bleed, width * 0.36, height * 0.16, width * 0.42, height * 0.16, width * 0.46, height + bleed],
            palette.Tone4, delay: 0.04, enterDY: height * 0.08, edgeWidth: 2);
        AddSurveyLines(ctx, 3);
        if (!ctx.ShowDecor) return;
        AddCornerTicks(ctx);
        AddWireTrace(ctx, width * 0.8, height * 0.82, Math.Min(width, height) * 0.08);
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["apex-mass"] = ApexMass;
        registry["ziggurat"] = Ziggurat;
        registry["slab-wall"] = SlabWall;
        registry["cantilever"] = Cantilever;
        registry["pylon-pair"] = PylonPair;
        registry["bunker-slit"] = BunkerSlit;
        registry["plinth-stack"] = PlinthStack;
        registry["buttress-run"] = ButtressRun;
        registry["void-core"] = VoidCore;
        registry["shear-block"] = ShearBlock;
        registry["ridge-line"] = RidgeLine;
        registry["chasm"] = Chasm;
        registry["overhang"] = Overhang;
        registry["step-well"] = StepWell;
        registry["pier-row"] = PierRow;
        registry["revetment"] = Revetment;
        registry["tower-crop"] = TowerCrop;
        registry["lintel"] = Lintel;
        registry["rubble-fan"] = RubbleFan;
        registry["gnomon"] = Gnomon;
    }
}
