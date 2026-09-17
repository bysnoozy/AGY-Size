using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AgySize.App.ViewModels;

namespace AgySize.App.Views.Controls;

/// <summary>
/// Treemap façon TreeSize : chaque enfant du nœud affiché est dessiné sous forme de rectangle
/// proportionnel à sa taille (algorithme "squarified treemap" de Bruls, Huizing &amp; van Wijk), avec
/// navigation par clic pour descendre dans un sous-dossier.
/// </summary>
public sealed class TreemapControl : Control
{
    public static readonly StyledProperty<FileSystemNodeViewModel?> NodeProperty =
        AvaloniaProperty.Register<TreemapControl, FileSystemNodeViewModel?>(nameof(Node));

    public FileSystemNodeViewModel? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public event EventHandler<FileSystemNodeViewModel>? NodeClicked;

    private static readonly IBrush[] Palette =
    {
        new SolidColorBrush(Color.Parse("#4F7CAC")),
        new SolidColorBrush(Color.Parse("#33475B")),
        new SolidColorBrush(Color.Parse("#6C91C2")),
        new SolidColorBrush(Color.Parse("#5B7A99")),
        new SolidColorBrush(Color.Parse("#8AA9D6")),
        new SolidColorBrush(Color.Parse("#7F98B3")),
    };

    private static readonly IPen BorderPen = new Pen(Brushes.White, 1);

    private readonly List<Rect> _lastRectangles = new();
    private readonly List<FileSystemNodeViewModel> _lastItems = new();

    static TreemapControl()
    {
        AffectsRender<TreemapControl>(NodeProperty);
        AffectsRender<TreemapControl>(BoundsProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);

        _lastRectangles.Clear();
        _lastItems.Clear();

        if (Node is null || bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var items = Node.Children
            .Where(c => c.SizeInBytes > 0)
            .OrderByDescending(c => c.SizeInBytes)
            .Take(60)
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        var sizes = items.Select(i => (double)i.SizeInBytes).ToList();
        var rectangles = Squarify(sizes, bounds);

        for (var i = 0; i < items.Count; i++)
        {
            var rect = rectangles[i];
            if (rect.Width <= 0.5 || rect.Height <= 0.5)
            {
                continue;
            }

            var brush = Palette[i % Palette.Length];
            context.DrawRectangle(brush, BorderPen, rect);

            _lastRectangles.Add(rect);
            _lastItems.Add(items[i]);

            if (rect.Width > 42 && rect.Height > 18)
            {
                var label = $"{items[i].Name} ({items[i].SizeDisplay})";
                var text = new FormattedText(
                    label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.White)
                {
                    MaxTextWidth = Math.Max(1, rect.Width - 8),
                    MaxTextHeight = Math.Max(1, rect.Height - 4),
                };

                context.DrawText(text, rect.TopLeft + new Vector(4, 2));
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);
        for (var i = 0; i < _lastRectangles.Count; i++)
        {
            if (_lastRectangles[i].Contains(point))
            {
                NodeClicked?.Invoke(this, _lastItems[i]);
                break;
            }
        }
    }

    private static List<Rect> Squarify(List<double> sizes, Rect bounds)
    {
        var result = new List<Rect>(new Rect[sizes.Count]);
        var total = sizes.Sum();
        if (total <= 0)
        {
            return result;
        }

        var scale = (bounds.Width * bounds.Height) / total;
        var areas = sizes.Select(s => s * scale).ToList();
        var indices = Enumerable.Range(0, areas.Count).ToList();

        LayoutRows(areas, indices, bounds, result);
        return result;
    }

    private static void LayoutRows(List<double> areas, List<int> indices, Rect bounds, List<Rect> result)
    {
        var remainingIndices = new List<int>(indices);
        var remainingBounds = bounds;

        while (remainingIndices.Count > 0 && remainingBounds.Width > 0 && remainingBounds.Height > 0)
        {
            var rowIndices = new List<int> { remainingIndices[0] };
            var shortSide = Math.Min(remainingBounds.Width, remainingBounds.Height);
            var bestRatio = WorstAspectRatio(new List<double> { areas[remainingIndices[0]] }, shortSide);

            var cursor = 1;
            while (cursor < remainingIndices.Count)
            {
                var candidateIndices = new List<int>(rowIndices) { remainingIndices[cursor] };
                var candidateRatio = WorstAspectRatio(candidateIndices.Select(i => areas[i]).ToList(), shortSide);

                if (candidateRatio > bestRatio)
                {
                    break;
                }

                rowIndices.Add(remainingIndices[cursor]);
                bestRatio = candidateRatio;
                cursor++;
            }

            var rowTotalArea = rowIndices.Sum(i => areas[i]);
            var isHorizontal = remainingBounds.Width >= remainingBounds.Height;
            var rowThickness = isHorizontal
                ? rowTotalArea / remainingBounds.Height
                : rowTotalArea / remainingBounds.Width;

            var offset = 0.0;
            foreach (var index in rowIndices)
            {
                var itemArea = areas[index];
                var itemLength = rowThickness <= 0 ? 0 : itemArea / rowThickness;

                result[index] = isHorizontal
                    ? new Rect(remainingBounds.X + offset, remainingBounds.Y, itemLength, rowThickness)
                    : new Rect(remainingBounds.X, remainingBounds.Y + offset, rowThickness, itemLength);

                offset += itemLength;
            }

            remainingBounds = isHorizontal
                ? new Rect(remainingBounds.X, remainingBounds.Y + rowThickness, remainingBounds.Width, Math.Max(0, remainingBounds.Height - rowThickness))
                : new Rect(remainingBounds.X + rowThickness, remainingBounds.Y, Math.Max(0, remainingBounds.Width - rowThickness), remainingBounds.Height);

            remainingIndices.RemoveRange(0, rowIndices.Count);
        }
    }

    private static double WorstAspectRatio(List<double> rowAreas, double shortSide)
    {
        var sum = rowAreas.Sum();
        if (sum <= 0 || shortSide <= 0)
        {
            return double.MaxValue;
        }

        var max = rowAreas.Max();
        var min = rowAreas.Min();
        var sideSquared = shortSide * shortSide;

        return Math.Max(sideSquared * max / (sum * sum), sum * sum / (sideSquared * min));
    }
}
