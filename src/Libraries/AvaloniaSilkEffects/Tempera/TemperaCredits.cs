using AvaloniaSilkEffects.Tempera.Geometry;
using AvaloniaSilkEffects.Tempera.Rendering;

namespace AvaloniaSilkEffects.Tempera;

public sealed record TemperaSongMetadata(string? Title = null, string? Artist = null, string? Album = null)
{
    public bool HasCredits => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Artist) || !string.IsNullOrWhiteSpace(Album);
}

/// <summary>Folia resolveTemperaCreditsFrame: the lyric fades out once the final paragraph ends, then the poster rises.</summary>
public readonly record struct TemperaCreditsFrame(bool Active, double LyricAlpha, double PosterAlpha, double PosterOffsetY, double PosterScale)
{
    public static TemperaCreditsFrame At(double time, double finalEndTime)
    {
        var lyricAlpha = 1 - TemperaEasing.InOut((time - finalEndTime - 0.1) / 0.9);
        var posterProgress = TemperaEasing.InOut((time - finalEndTime - 0.9) / 1.1);
        return new TemperaCreditsFrame(time > finalEndTime + 0.35, lyricAlpha, posterProgress, (1 - posterProgress) * 0.06,
            0.96 + posterProgress * 0.04);
    }
}

/// <summary>
/// Folia buildTemperaCreditsPoster: the closing card from the shots' own vocabulary — a tone1 ground, three partial discs
/// whose centres sit off frame climbing the tone ladder, hard ink seams, one hatch pass, the shared crossing lines and corner
/// motif — and the title under the difference filter, fixed while the shapes keep creeping under it.
/// </summary>
internal sealed class TemperaCredits : IDisposable
{
    private const int DiscCount = 3;

    private sealed class Item
    {
        public required TemperaNode Node;
        public double BaseX;
        public double BaseY;
        public double BaseAlpha = 1;
        public double EnterDX;
        public double EnterDY;
        public double Delay;
        public double DriftX;
        public double DriftY;
        public double Grow;
    }

    private readonly List<Item> _items = [];
    private readonly List<TemperaGraphic> _graphics = [];

    /// <summary>The credits layer: positioned at the frame centre, alpha / offset / scale from the credits frame.</summary>
    public TemperaGroup Layer { get; } = new() { Visible = false };

    private TemperaGroup Poster { get; } = new();

    public TemperaCredits(TemperaTheme theme, TemperaTuning tuning, TemperaPalette palette, TemperaSongMetadata metadata, double width,
        double height, double lyricsFontScale)
    {
        Layer.Add(Poster);
        var seed = TemperaRandom.Hash($"{metadata.Title ?? ""}|{metadata.Artist ?? ""}");
        var diagonal = Math.Sqrt(width * width + height * height);
        var bleed = diagonal * 0.25;
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var flip = TemperaRandom.Hash01(seed, 1, 229) > 0.5 ? 1 : -1;
        var gradient = palette.Gradient is { } colors ? new TemperaGradientFill(colors, TemperaRandom.Hash01(seed, 3, 197) * Math.PI * 2) : null;

        var ground = TemperaShapes.PolygonFill(TemperaHatch.Rect(-halfWidth - bleed, -halfHeight - bleed, width + bleed * 2, height + bleed * 2),
            palette.Tone1, 1, gradient);
        _graphics.Add(ground);
        Poster.Add(ground);

        TemperaRgb[] tones = [palette.Tone2, palette.Tone3, palette.Tone4];
        var baseAngle = TemperaRandom.Hash01(seed, 4, 241) * Math.PI * 2;
        var hatchIndex = (int)Math.Floor(TemperaRandom.Hash01(seed, 5, 251) * DiscCount) % DiscCount;
        for (var index = 0; index < DiscCount; index++)
        {
            var angle = baseAngle + index * Math.PI * 2 / DiscCount + (TemperaRandom.Hash01(seed, index, 257) - 0.5) * 0.7;
            var distance = diagonal * (0.58 + TemperaRandom.Hash01(seed, index, 263) * 0.14);
            var radius = distance + diagonal * (0.06 + TemperaRandom.Hash01(seed, index, 269) * 0.1);
            var polygon = TemperaHatch.Circle(0, 0, radius, 72);
            var cx = Math.Cos(angle) * distance;
            var cy = Math.Sin(angle) * distance;
            var delay = 0.08 + index * 0.16;
            void Place(TemperaGraphic node, double itemDelay)
            {
                node.SetPosition(cx, cy);
                Add(node, new Item
                {
                    Node = node,
                    Delay = itemDelay,
                    EnterDX = Math.Cos(angle) * diagonal * 0.16,
                    EnterDY = Math.Sin(angle) * diagonal * 0.16,
                    DriftX = -Math.Cos(angle) * diagonal * 0.024,
                    DriftY = -Math.Sin(angle) * diagonal * 0.024,
                });
            }
            Place(TemperaShapes.PolygonFill(polygon, tones[index], 1, gradient), delay);
            if (index == hatchIndex)
                Place(TemperaShapes.HatchFill(polygon, TemperaHatch.Spec(seed, 271), palette.Paper, 0.28), delay + 0.06);
            Place(TemperaShapes.PolygonOutline(polygon, palette.Ink, 2.4, 0.8), delay + 0.04);
        }

        var lines = TemperaHatch.CrossingLines(seed, 31, width, height, 2)
            .Select(line => new TemperaLine(line.X1 - halfWidth, line.Y1 - halfHeight, line.X2 - halfWidth, line.Y2 - halfHeight));
        var guide = TemperaShapes.Lines(lines, palette.Tone4, 1.3, 0.6);
        Add(guide, new Item { Node = guide, Delay = 0.5, EnterDX = width * 0.2 });

        if (tuning.ShowDecor)
        {
            var cornerX = halfWidth * 0.66 * flip;
            var cornerY = halfHeight * 0.62;
            var marks = TemperaShapes.CrossMarks(TemperaHatch.CrossRow(seed, 71, cornerX - width * 0.1, cornerY, 5, width * 0.05, 8),
                palette.Tone4, 2, 0.8);
            Add(marks, new Item { Node = marks, Delay = 0.62, EnterDX = -width * 0.06 });
            var box = TemperaHatch.Rect(-cornerX - 19, -cornerY - 19, 38, 38);
            var hatch = TemperaShapes.HatchFill(box, TemperaHatch.Spec(seed, 67), palette.Tone4, 0.7);
            Add(hatch, new Item { Node = hatch, Delay = 0.68, Grow = 0.06 });
            var outline = TemperaShapes.PolygonOutline(box, palette.Tone4, 1.2, 0.6);
            Add(outline, new Item { Node = outline, Delay = 0.72 });
        }

        var family = theme.FontFamily;
        var weight = theme.FontWeight;
        var wrapWidth = Math.Min(width * 0.72, diagonal * 0.52);
        var subtitle = string.Join(" - ", new[] { metadata.Artist, metadata.Album }.Where(part => !string.IsNullOrEmpty(part)));
        TemperaGroup titleLayer = TemperaSceneView.TextFilterLayer(palette, tuning.TextInversion) ?? new TemperaGroup();
        var titleSize = Math.Max(26, Math.Min(width, height) * 0.085 * lyricsFontScale);
        TemperaText Line(string text, double size, double offsetY, double alpha)
        {
            var node = new TemperaText
            {
                Text = text,
                FontFamily = family,
                FontWeight = weight,
                FontSize = size,
                Color = palette.Ink,
                WrapWidth = wrapWidth,
                Alpha = alpha,
            };
            node.SetPosition(0, offsetY);
            titleLayer.Add(node);
            return node;
        }
        var titleText = string.IsNullOrWhiteSpace(metadata.Title) ? "♪" : metadata.Title.Trim();
        var title = Line(titleText, titleSize, subtitle.Length > 0 ? -titleSize * 0.35 : 0, 1);
        if (subtitle.Length > 0)
        {
            var titleHeight = Text.TemperaGlyphs.Box(titleText, family, weight, titleSize, wrapWidth).Y;
            Line(subtitle, Math.Max(14, titleSize * 0.34), titleHeight / 2 + titleSize * 0.22, 0.75);
        }
        _ = title;
        Add(titleLayer, new Item { Node = titleLayer, EnterDY = diagonal * 0.03, Delay = 0.78 });
        Update(0);
    }

    private void Add(TemperaNode node, Item item)
    {
        item.BaseX = node.X;
        item.BaseY = node.Y;
        _items.Add(item);
        Poster.Add(node);
        if (node is TemperaGraphic graphic) _graphics.Add(graphic);
    }

    /// <summary><paramref name="elapsed"/> is seconds since the card started; it keeps creeping, asymptotically, forever.</summary>
    public void Update(double elapsed)
    {
        var creep = 1 - Math.Exp(-Math.Max(0, elapsed) / 7);
        foreach (var item in _items)
        {
            var enter = TemperaEasing.Enter((elapsed - item.Delay) / 1.1);
            item.Node.Alpha = item.BaseAlpha * enter;
            item.Node.Visible = enter > 0.001;
            item.Node.SetPosition(item.BaseX + item.EnterDX * (1 - enter) + item.DriftX * creep,
                item.BaseY + item.EnterDY * (1 - enter) + item.DriftY * creep);
            if (item.Grow != 0) item.Node.SetScale(1 + item.Grow * creep);
        }
    }

    public void ApplyFrame(TemperaCreditsFrame frame, double time, double finalEndTime, double width, double height)
    {
        Layer.Visible = frame.Active;
        Layer.Alpha = frame.PosterAlpha;
        if (Layer.Visible) Update(time - finalEndTime);
        Layer.SetPosition(width / 2, height / 2 + frame.PosterOffsetY * height);
        Layer.SetScale(frame.PosterScale);
    }

    public void Dispose()
    {
        foreach (var graphic in _graphics) graphic.Dispose();
    }
}

/// <summary>Folia temperaOverlay.ts: the two corner registration marks and the paragraph wipe block, above everything.</summary>
internal sealed class TemperaOverlay : IDisposable
{
    private TemperaGraphic? _wipe;
    private string _wipeKey = "";
    private TemperaGraphic? _marks;

    public TemperaGroup Container { get; } = new();

    public void Draw(double width, double height, TemperaTuning tuning, TemperaTheme theme)
    {
        Dispose();
        Container.Children.Clear();
        _wipe = new TemperaGraphic { Visible = false };
        _wipeKey = "";
        Container.Add(_wipe);
        if (!tuning.ShowCornerMarks) return;
        var paddingX = Math.Max(28, width * 0.045);
        var paddingY = Math.Max(28, height * 0.045);
        _marks = new TemperaGraphic();
        foreach (var points in new[]
                 {
                     new[] { paddingX, paddingY + 14, paddingX, paddingY, paddingX + 14, paddingY },
                     new[] { width - paddingX - 14, height - paddingY, width - paddingX, height - paddingY, width - paddingX, height - paddingY - 14 },
                 })
        {
            var stroke = TemperaShapes.Polyline(points, theme.Primary, 1.5, 0.5);
            _marks.Parts.AddRange(stroke.Parts);
        }
        Container.Add(_marks);
    }

    /// <summary>Slides a screen-sized chevron block along <paramref name="angle"/>; at travel 1 it covers the frame exactly.</summary>
    public void DrawWipe(double travel, double angle, double width, double height, TemperaRgb color)
    {
        if (_wipe is null) return;
        if (travel <= 0.001 || travel >= 1.999)
        {
            _wipe.Visible = false;
            return;
        }
        var span = Math.Sqrt(width * width + height * height);
        var notch = span * 0.08;
        var length = span + notch * 2;
        var key = $"{width}x{height}|{color}";
        if (key != _wipeKey)
        {
            _wipeKey = key;
            var start = -span / 2 - notch;
            var end = start + length;
            var half = span / 2;
            var shape = TemperaShapes.PolygonFill([start, -half, end, -half, end + notch, 0, end, half, start, half, start + notch, 0], color);
            _wipe.Dispose();
            _wipe.Parts.Clear();
            _wipe.Parts.AddRange(shape.Parts);
        }
        var offset = (travel - 1) * length;
        _wipe.SetPivot(0, 0);
        _wipe.SetPosition(width / 2 + Math.Cos(angle) * offset, height / 2 + Math.Sin(angle) * offset);
        _wipe.Rotation = angle;
        _wipe.SetScale(1);
        _wipe.Visible = true;
    }

    public void HideWipe()
    {
        if (_wipe is not null) _wipe.Visible = false;
    }

    public void Dispose()
    {
        _wipe?.Dispose();
        _marks?.Dispose();
    }
}
