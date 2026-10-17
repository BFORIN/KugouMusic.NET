using AvaloniaSilkEffects.Tempera.Compositions;
using AvaloniaSilkEffects.Tempera.Rendering;

namespace AvaloniaSilkEffects.Tempera;

/// <summary>
/// Folia temperaBlocks.ts: the screentone MG layer of one shot. It owns the enter stagger (paced against the shot's lyric),
/// the steady creep along the flow vector (paced against the shot's whole life) and the slow deterministic float; all
/// geometry comes from the composition registry.
/// </summary>
internal sealed class TemperaBlocks
{
    private sealed class Item
    {
        public required TemperaGraphic Node;
        public double BaseX;
        public double BaseY;
        public double BaseAlpha;
        public double EnterDX;
        public double EnterDY;
        public double DelayFraction;
        public double SpanFraction;
        public bool Drift;
        public double DriftPhase;
        public bool Grow;
    }

    private readonly List<Item> _items = [];
    private readonly double _flowX;
    private readonly double _flowY;
    private readonly double _carry;
    private double _schedulePace = double.NaN;
    private double[] _delays = [];
    private double[] _spans = [];

    public TemperaGroup Container { get; } = new();

    public TemperaBlocks(string kind, TemperaDecorSpec decor, TemperaPalette palette, double width, double height, long seed, bool showDecor,
        double flowAngle)
    {
        _flowX = Math.Cos(flowAngle);
        _flowY = Math.Sin(flowAngle);
        // Per-item stagger distance only; the bulk of the hand-off travel belongs to the shot container.
        _carry = Math.Max(width, height) * 0.09;
        var context = new TemperaCompositionContext
        {
            Kind = kind,
            Palette = palette,
            Decor = decor,
            Width = width,
            Height = height,
            Seed = seed,
            ShowDecor = showDecor,
            FlowAngle = flowAngle,
            Bleed = _carry + Math.Max(width, height) * 0.08,
            Gradient = palette.Gradient is { } colors
                ? new TemperaGradientFill(colors, TemperaRandom.Hash01(seed, 3, 197) * Math.PI * 2)
                : null,
            AddNode = (node, options, parent) =>
            {
                _items.Add(new Item
                {
                    Node = node,
                    BaseX = node.X,
                    BaseY = node.Y,
                    BaseAlpha = options.Alpha,
                    EnterDX = options.EnterDX,
                    EnterDY = options.EnterDY,
                    DelayFraction = options.Delay,
                    SpanFraction = options.Span,
                    Drift = options.Drift,
                    DriftPhase = TemperaRandom.Hash01(seed, _items.Count, 173) * Math.PI * 2,
                    Grow = options.Grow,
                });
                (parent ?? Container).Add(node);
            },
            CreateGroupNode = (rotation, x, y) =>
            {
                var group = new TemperaGroup { Rotation = rotation };
                group.SetPosition(x, y);
                Container.Add(group);
                return group;
            },
        };
        TemperaCompositions.Draw(context);
    }

    private void ResolveSchedule(double paceDuration)
    {
        if (paceDuration == _schedulePace) return;
        _schedulePace = paceDuration;
        var budget = Math.Max(0.5, paceDuration);
        _delays = new double[_items.Count];
        _spans = new double[_items.Count];
        for (var index = 0; index < _items.Count; index++)
        {
            var rawDelay = TemperaEasing.ShotPaced(paceDuration, _items[index].DelayFraction, 0, 1.4);
            var rawSpan = TemperaEasing.ShotPaced(paceDuration, _items[index].SpanFraction, 0.7, 2.6);
            // Short shots compress the whole stagger instead of dropping the late items.
            var compress = Math.Min(1, budget / (rawDelay + rawSpan));
            _delays[index] = rawDelay * compress;
            _spans[index] = rawSpan * compress;
        }
    }

    public void Update(double time, double shotStart, double shotEnd, double lyricEnd)
    {
        var duration = Math.Max(shotEnd - shotStart, 0.2);
        var paceDuration = Math.Max(lyricEnd - shotStart, 0.2);
        var progress = TemperaEasing.Clamp01((time - shotStart) / duration);
        var creep = TemperaEasing.InOut(progress) * _carry * 0.35;
        ResolveSchedule(paceDuration);
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var enter = TemperaEasing.Enter((time - shotStart - _delays[index]) / _spans[index]);
            item.Node.Alpha = item.BaseAlpha * enter;
            item.Node.Visible = enter > 0.001;
            var behind = (1 - enter) * _carry - creep;
            item.Node.SetPosition(item.BaseX + item.EnterDX * (1 - enter) - _flowX * behind,
                item.BaseY + item.EnterDY * (1 - enter) - _flowY * behind);
            if (!item.Drift && !item.Grow) continue;
            var floatScale = item.Drift ? 1 + Math.Sin(time * 0.5 + item.DriftPhase) * 0.02 : 1;
            item.Node.ScaleX = item.Grow ? Math.Max(0.0001, enter) * floatScale : floatScale;
            item.Node.ScaleY = floatScale;
            if (item.Drift) item.Node.Rotation = Math.Sin(time * 0.33 + item.DriftPhase) * 0.012;
        }
    }

    public void Dispose()
    {
        foreach (var item in _items) item.Node.Dispose();
    }
}
