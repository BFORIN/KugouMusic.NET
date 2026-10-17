using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaCutout;
using C = AvaloniaSilkEffects.Tempera.Geometry.TemperaCurves;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia compositions/temperaCorridorCompositions.ts: openings cut along the flow vector, so neighbouring shots sliding
/// past each other keep the same corridor moving instead of cutting to a new one.
/// </summary>
internal static class TemperaCorridorCompositions
{
    private static double[] Channel(TemperaCompositionContext ctx, double offset, double width)
    {
        var centre = AcrossPoint(ctx, offset);
        return AxisRect(centre.X, centre.Y, FlowSpan(ctx), width, ChannelAxis(ctx));
    }

    private static void AddRails(TemperaCompositionContext ctx, double offset, double width, double alpha = 0.55)
    {
        var angle = ChannelAxis(ctx);
        var half = FlowSpan(ctx) / 2;
        double[] sides = [-1, 1];
        for (var index = 0; index < sides.Length; index++)
        {
            var start = FlowPoint(ctx, -half, offset + sides[index] * width);
            var end = FlowPoint(ctx, half, offset + sides[index] * width);
            ctx.Add(S.Lines([new TemperaLine(start.X, start.Y, end.X, end.Y)], ctx.Palette.Tone4, 1.4, alpha),
                O(delay: 0.18 + index * 0.03, span: 0.5, enterDX: Math.Cos(angle) * ctx.Width * 0.08, enterDY: Math.Sin(angle) * ctx.Height * 0.08));
        }
    }

    private static void FlowChannel(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var slot = Channel(ctx, ctx.Width * 0.26, unit * 0.3);
        AddCutField(ctx, ctx.Palette.Tone3, [slot]);
        AddHoleLip(ctx, slot, 2.6, 0.85, 0.08);
        AddRails(ctx, ctx.Width * 0.26, unit * 0.19);
    }

    private static void TwinChannel(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        double[][] slots = [Channel(ctx, -ctx.Width * 0.34, unit * 0.16), Channel(ctx, ctx.Width * 0.34, unit * 0.16)];
        AddCutField(ctx, ctx.Palette.Tone4, slots);
        for (var index = 0; index < slots.Length; index++) AddHoleLip(ctx, slots[index], 2.4, 0.8, 0.1 + index * 0.04);
    }

    private static void ReedRun(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var slots = new List<double[]>();
        foreach (var side in new double[] { -1, 1 })
            for (var index = 0; index < 4; index++)
                slots.Add(Channel(ctx, side * ctx.Width * (0.22 + index * 0.08), unit * 0.035));
        AddCutField(ctx, ctx.Palette.Tone2, slots, alpha: 0.9);
        AddRails(ctx, 0, ctx.Width * 0.17, 0.4);
    }

    private static void TaperChannel(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var half = FlowSpan(ctx) / 2;
        var wide = unit * 0.24;
        var narrow = unit * 0.06;
        var offset = ctx.Width * 0.24;
        (double X, double Y)[] corners =
        [
            FlowPoint(ctx, -half, offset - wide),
            FlowPoint(ctx, -half, offset + wide),
            FlowPoint(ctx, half, offset + narrow),
            FlowPoint(ctx, half, offset - narrow),
        ];
        double[] slot = [.. corners.SelectMany(point => new[] { point.X, point.Y })];
        AddCutField(ctx, ctx.Palette.Tone3, [slot]);
        AddHoleLip(ctx, slot, 2.6, 0.85, 0.08);
    }

    private static void ChainPorts(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var offset = width * 0.28;
        var spacing = height * 0.26;
        var ports = new[] { -1.5, -0.5, 0.5, 1.5 }.Select(step => FlowPoint(ctx, step * spacing, offset)).ToArray();
        AddCutField(ctx, palette.Tone4, [.. ports.Select(port => H.Circle(port.X, port.Y, unit * 0.11, 40))]);
        ctx.Add(S.Rings([.. ports.Select(port => new TemperaDisc(port.X, port.Y, unit * 0.11))], palette.Ink, 2.4, 0.85), O(delay: 0.1, span: 0.55));
        var head = FlowPoint(ctx, -spacing * 2.4, offset);
        var tail = FlowPoint(ctx, spacing * 2.4, offset);
        ctx.Add(S.Lines([new TemperaLine(head.X, head.Y, tail.X, tail.Y)], palette.Tone2, 1.6, 0.6), O(delay: 0.18, span: 0.5));
    }

    private static void DashChannel(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var offset = ctx.Width * 0.24;
        var step = ctx.Height * 0.32;
        var angle = ChannelAxis(ctx);
        var slots = new[] { -1.5, -0.5, 0.5, 1.5 }.Select(index =>
        {
            var centre = FlowPoint(ctx, index * step, offset);
            return AxisRect(centre.X, centre.Y, step * 0.72, unit * 0.22, angle);
        }).ToArray();
        AddCutField(ctx, ctx.Palette.Tone3, slots);
        for (var index = 0; index < slots.Length; index++) AddHoleLip(ctx, slots[index], 2.2, 0.8, 0.08 + index * 0.03);
    }

    private static void WindowRun(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var offset = ctx.Width * 0.27;
        var step = ctx.Height * 0.36;
        var angle = ChannelAxis(ctx);
        var slots = new[] { -1.5, -0.5, 0.5, 1.5 }.Select(index =>
        {
            var centre = FlowPoint(ctx, index * step, offset);
            var size = unit * 0.2;
            return C.Rotate(C.RoundedRect(centre.X - size, centre.Y - size * 0.62, size * 2, size * 1.24, size * 0.3), centre.X, centre.Y,
                angle + Math.PI / 2);
        }).ToArray();
        AddCutField(ctx, ctx.Palette.Tone4, slots);
        for (var index = 0; index < slots.Length; index++) AddHoleLip(ctx, slots[index], 2.4, 0.85, 0.08 + index * 0.03);
    }

    private static void BridgeSpan(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var slot = Channel(ctx, 0, unit * 0.62);
        AddCutField(ctx, palette.Tone4, [slot]);
        AddHoleLip(ctx, slot, 2.6, 0.8, 0.08);
        var band = AxisRect(width / 2, height / 2, FlowSpan(ctx), height * 0.46, ChannelAxis(ctx) + Math.PI / 2);
        ctx.Add(S.PolygonFill(band, palette.Tone3, 0.95, ctx.Gradient), O(delay: 0.12, span: 0.6, enterDX: -width * 0.16));
        ctx.Add(S.PolygonOutline(band, palette.Ink, 2, 0.7), O(delay: 0.2, span: 0.5, enterDX: width * 0.1));
    }

    private static void BraidChannel(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var angle = ChannelAxis(ctx);
        var pitch = ctx.Height * 0.42;
        var slots = new List<double[]>();
        foreach (var side in new double[] { -1, 1 })
            for (var index = -1; index <= 1; index++)
            {
                var centre = FlowPoint(ctx, index * pitch + side * pitch * 0.5, side * ctx.Width * 0.32);
                slots.Add(AxisRect(centre.X, centre.Y, pitch * 0.78, unit * 0.16, angle));
            }
        AddCutField(ctx, ctx.Palette.Tone3, slots);
        for (var index = 0; index < slots.Count; index++) AddHoleLip(ctx, slots[index], 2.2, 0.8, 0.08 + index * 0.025);
    }

    private static void PortLadder(TemperaCompositionContext ctx)
    {
        var unit = Math.Min(ctx.Width, ctx.Height);
        var offset = ctx.Width * 0.3;
        var slot = Channel(ctx, offset, unit * 0.14);
        AddCutField(ctx, ctx.Palette.Tone2, [slot]);
        AddHoleLip(ctx, slot, 2.2, 0.8, 0.08);
        var rungs = Enumerable.Range(0, 9).Select(index =>
        {
            var distance = (index - 4) * ctx.Height * 0.13;
            var isLong = index % 3 == 0;
            var inner = FlowPoint(ctx, distance, offset - unit * 0.09);
            var outer = FlowPoint(ctx, distance, offset - unit * (isLong ? 0.22 : 0.15));
            return new TemperaLine(inner.X, inner.Y, outer.X, outer.Y);
        });
        ctx.Add(S.Lines(rungs, ctx.Palette.Tone4, 2, 0.7), O(delay: 0.14, span: 0.6));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["flow-channel"] = FlowChannel;
        registry["twin-channel"] = TwinChannel;
        registry["reed-run"] = ReedRun;
        registry["taper-channel"] = TaperChannel;
        registry["chain-ports"] = ChainPorts;
        registry["dash-channel"] = DashChannel;
        registry["window-run"] = WindowRun;
        registry["bridge-span"] = BridgeSpan;
        registry["braid-channel"] = BraidChannel;
        registry["port-ladder"] = PortLadder;
    }
}
