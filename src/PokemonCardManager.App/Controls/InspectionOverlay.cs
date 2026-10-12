using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.Controls;

public enum OverlayMode { Look, MarkDefect, Centering }

/// <summary>A box the overlay draws. Coordinates are fractions (0–1) of the photo.</summary>
public interface IOverlayBox
{
    NormalizedBox Box { get; }
    string Label { get; }
    bool IsSelected { get; }
}

/// <summary>
/// Sits exactly on top of the photo, at the photo's size, and draws defect boxes and centering guides.
/// Because it lives inside the same zoom/rotate transform as the photo, WPF hands it mouse positions
/// already in photo coordinates: dividing by ActualWidth/Height gives fractions, whatever the zoom or rotation.
/// </summary>
public sealed class InspectionOverlay : FrameworkElement
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(OverlayMode),
        typeof(InspectionOverlay), new FrameworkPropertyMetadata(OverlayMode.Look, FrameworkPropertyMetadataOptions.AffectsRender, OnModeChanged));

    public static readonly DependencyProperty BoxesProperty = DependencyProperty.Register(nameof(Boxes), typeof(IEnumerable),
        typeof(InspectionOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnBoxesChanged));

    public static readonly DependencyProperty GuidesProperty = DependencyProperty.Register(nameof(Guides), typeof(CenteringGuides),
        typeof(InspectionOverlay), new FrameworkPropertyMetadata(CenteringGuides.Default,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty BoxDrawnCommandProperty = DependencyProperty.Register(nameof(BoxDrawnCommand),
        typeof(ICommand), typeof(InspectionOverlay));

    public OverlayMode Mode { get => (OverlayMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public IEnumerable? Boxes { get => (IEnumerable?)GetValue(BoxesProperty); set => SetValue(BoxesProperty, value); }
    public CenteringGuides Guides { get => (CenteringGuides)GetValue(GuidesProperty); set => SetValue(GuidesProperty, value); }
    /// <summary>Executed with a <see cref="NormalizedBox"/> when the user finishes drawing a box.</summary>
    public ICommand? BoxDrawnCommand { get => (ICommand?)GetValue(BoxDrawnCommandProperty); set => SetValue(BoxDrawnCommandProperty, value); }

    private Point? _dragStart;
    private Point _dragNow;
    private int? _draggedGuide; // index 0–7 into the guide list, see GuideValues

    public InspectionOverlay()
    {
        // Transparent but hit-testable only when a tool is active, so in Look mode drags reach the viewer (panning).
        IsHitTestVisible = false;
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (InspectionOverlay)d;
        overlay.IsHitTestVisible = overlay.Mode != OverlayMode.Look;
        overlay.Cursor = overlay.Mode == OverlayMode.MarkDefect ? Cursors.Cross : null;
    }

    private static void OnBoxesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (InspectionOverlay)d;
        if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= overlay.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged now) now.CollectionChanged += overlay.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    /// <summary>Redraw after a box's selection or label changed.</summary>
    public void Refresh() => InvalidateVisual();

    // A transparent background makes the whole area receive mouse clicks.
    protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var scale = ScreenScale();
        double w = ActualWidth, h = ActualHeight;

        // Pens are sized in screen pixels, so lines stay thin and crisp at any zoom.
        var boxBrush = Brush("PinkBrush", Colors.HotPink);
        var selectedBrush = Brush("AccentBrush", Colors.MediumPurple);
        var fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        fill.Freeze();

        if (Boxes is not null)
            foreach (var item in Boxes.OfType<IOverlayBox>())
            {
                var b = item.Box;
                var rect = new Rect(b.X * w, b.Y * h, b.Width * w, b.Height * h);
                var pen = new Pen(item.IsSelected ? selectedBrush : boxBrush, (item.IsSelected ? 3 : 2) / scale);
                dc.DrawRectangle(fill, pen, rect);
                var text = new FormattedText(item.Label, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 12 / scale, pen.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point(rect.X, Math.Max(0, rect.Y - text.Height)));
            }

        if (Mode == OverlayMode.MarkDefect && _dragStart is { } start)
            dc.DrawRectangle(fill, new Pen(selectedBrush, 2 / scale) { DashStyle = DashStyles.Dash }, new Rect(start, _dragNow));

        if (Mode == OverlayMode.Centering)
        {
            var outer = new Pen(Brush("SkyBrush", Colors.SkyBlue), 2 / scale);
            var inner = new Pen(Brush("PinkBrush", Colors.HotPink), 2 / scale);
            var values = GuideValues(Guides);
            for (var i = 0; i < 8; i++)
            {
                var pen = i is 0 or 3 or 4 or 7 ? outer : inner;
                if (i < 4) dc.DrawLine(pen, new Point(values[i] * w, 0), new Point(values[i] * w, h));
                else dc.DrawLine(pen, new Point(0, values[i] * h), new Point(w, values[i] * h));
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = Clamp(e.GetPosition(this));
        if (Mode == OverlayMode.MarkDefect)
        {
            _dragStart = _dragNow = p;
            CaptureMouse();
            e.Handled = true;
        }
        else if (Mode == OverlayMode.Centering && NearestGuide(p) is { } index)
        {
            _draggedGuide = index;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = Clamp(e.GetPosition(this));
        if (_dragStart is not null)
        {
            _dragNow = p;
            InvalidateVisual();
        }
        else if (_draggedGuide is { } index)
        {
            Guides = MoveGuide(Guides, index, index < 4 ? p.X / ActualWidth : p.Y / ActualHeight);
        }
        else if (Mode == OverlayMode.Centering)
        {
            Cursor = NearestGuide(p) is { } i ? (i < 4 ? Cursors.SizeWE : Cursors.SizeNS) : null;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragStart is { } start)
        {
            var rect = new Rect(start, _dragNow);
            _dragStart = null;
            // Ignore accidental clicks: a box must be at least 0.5% of the photo each way.
            if (rect.Width / ActualWidth >= 0.005 && rect.Height / ActualHeight >= 0.005)
            {
                var box = new NormalizedBox(rect.X / ActualWidth, rect.Y / ActualHeight,
                    Math.Min(rect.Width / ActualWidth, 1 - rect.X / ActualWidth),
                    Math.Min(rect.Height / ActualHeight, 1 - rect.Y / ActualHeight));
                if (BoxDrawnCommand?.CanExecute(box) == true) BoxDrawnCommand.Execute(box);
            }
            InvalidateVisual();
        }
        _draggedGuide = null;
        ReleaseMouseCapture();
    }

    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, ActualWidth), Math.Clamp(p.Y, 0, ActualHeight));

    /// <summary>Guide within 8 screen pixels of the point, if any.</summary>
    private int? NearestGuide(Point p)
    {
        var tolerance = 8 / ScreenScale();
        var values = GuideValues(Guides);
        int? best = null;
        var bestDistance = tolerance;
        for (var i = 0; i < 8; i++)
        {
            var distance = i < 4 ? Math.Abs(values[i] * ActualWidth - p.X) : Math.Abs(values[i] * ActualHeight - p.Y);
            if (distance <= bestDistance) { best = i; bestDistance = distance; }
        }
        return best;
    }

    /// <summary>How many screen pixels one unit of this element covers at the current zoom.</summary>
    private double ScreenScale()
    {
        if (PresentationSource.FromVisual(this)?.RootVisual is not { } root || !root.IsAncestorOf(this)) return 1;
        var t = TransformToAncestor(root);
        var length = (t.Transform(new Point(1, 0)) - t.Transform(new Point(0, 0))).Length;
        return length > 0 ? length : 1;
    }

    private static double[] GuideValues(CenteringGuides g) =>
        [g.OuterLeft, g.InnerLeft, g.InnerRight, g.OuterRight, g.OuterTop, g.InnerTop, g.InnerBottom, g.OuterBottom];

    /// <summary>Moves one guide, kept between its neighbours so the order outer → inner → inner → outer holds.</summary>
    private static CenteringGuides MoveGuide(CenteringGuides g, int index, double value)
    {
        var v = GuideValues(g);
        var axisStart = index < 4 ? 0 : 4;
        var low = index == axisStart ? 0 : v[index - 1];
        var high = index == axisStart + 3 ? 1 : v[index + 1];
        // Inner guides may not touch each other (the artwork must have a width).
        if (index - axisStart == 1) high -= 0.01;
        if (index - axisStart == 2) low += 0.01;
        v[index] = Math.Clamp(value, low, high);
        return new CenteringGuides(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);
    }

    private Brush Brush(string key, Color fallback) => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
