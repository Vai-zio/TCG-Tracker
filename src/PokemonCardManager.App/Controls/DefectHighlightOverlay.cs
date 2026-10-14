using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.Controls;

/// <summary>A defect the grading report draws on a photo. The box is in fractions (0–1) of the photo.</summary>
public interface IDefectMarker
{
    NormalizedBox Box { get; }
    int Number { get; }
    /// <summary>The app's own 0–5 scale; decides the colour.</summary>
    int Severity { get; }
}

/// <summary>
/// Read-only highlights for the grading report. Each defect gets a box coloured by severity and a numbered badge.
/// When one is selected, everything else on the photo is dimmed (a "spotlight"), so the eye goes straight to it.
/// Clicking a box selects that defect. Sits on top of the photo at the photo's size, like <see cref="InspectionOverlay"/>.
/// </summary>
public sealed class DefectHighlightOverlay : FrameworkElement
{
    public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(nameof(Markers), typeof(IEnumerable),
        typeof(DefectHighlightOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnMarkersChanged));

    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected), typeof(object),
        typeof(DefectHighlightOverlay), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public IEnumerable? Markers { get => (IEnumerable?)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    public object? Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    public DefectHighlightOverlay() => Cursor = Cursors.Hand;

    /// <summary>Severity 0–1 green, 2 yellow, 3 orange, 4–5 red. Theme brushes, with fallbacks.</summary>
    public static (string Key, Color Fallback) SeverityColour(int severity) => severity switch
    {
        <= 1 => ("MintBrush", Colors.MediumSeaGreen),
        2 => ("LemonBrush", Colors.Gold),
        3 => ("PeachBrush", Colors.Orange),
        _ => ("DangerBrush", Colors.Red),
    };

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (DefectHighlightOverlay)d;
        if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= overlay.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged now) now.CollectionChanged += overlay.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    // Clicks anywhere on the photo reach this element (a click outside every box clears the selection).
    protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || Markers is null) return;
        double w = ActualWidth, h = ActualHeight;
        var scale = ScreenScale();
        var markers = Markers.OfType<IDefectMarker>().ToList();

        // Spotlight: a dark layer with a hole where the selected defect is.
        // (Only when the selected defect is on this photo; one seen but not marked has no box to show.)
        if (Selected is IDefectMarker chosen && markers.Contains(chosen))
        {
            var dim = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
            dim.Freeze();
            var hole = Inflate(ToRect(chosen.Box, w, h), 6 / scale);
            dc.DrawGeometry(dim, null, new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(0, 0, w, h)), new RectangleGeometry(hole)));
        }

        // Selected last, so it's drawn on top of any box it overlaps.
        foreach (var m in markers.OrderBy(m => ReferenceEquals(m, Selected)))
        {
            var selected = ReferenceEquals(m, Selected);
            var (key, fallback) = SeverityColour(m.Severity);
            var brush = TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
            var rect = ToRect(m.Box, w, h);
            // Pens are sized in screen pixels, so lines look the same however large the photo is.
            // A dark outline under the coloured one keeps the box visible on light and dark card art alike.
            dc.DrawRectangle(null, new Pen(Brushes.Black, (selected ? 6 : 4) / scale), rect);
            dc.DrawRectangle(null, new Pen(brush, (selected ? 4 : 2.5) / scale), rect);
            DrawBadge(dc, m.Number.ToString(CultureInfo.CurrentCulture), brush, rect.TopLeft, scale);
        }
    }

    /// <summary>A round number badge on the box's top-left corner, matching the number in the defect list.</summary>
    private void DrawBadge(DrawingContext dc, string text, Brush fill, Point corner, double scale)
    {
        var radius = 11 / scale;
        var label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12 / scale, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var centre = new Point(Math.Max(radius, corner.X), Math.Max(radius, corner.Y));
        dc.DrawEllipse(fill, new Pen(Brushes.Black, 1.5 / scale), centre, radius, radius);
        dc.DrawText(label, new Point(centre.X - label.Width / 2, centre.Y - label.Height / 2));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        // The smallest box under the pointer wins, so a small defect inside a big one can still be picked.
        Selected = Markers?.OfType<IDefectMarker>()
            .Where(m => Inflate(ToRect(m.Box, ActualWidth, ActualHeight), 4 / ScreenScale()).Contains(p))
            .OrderBy(m => m.Box.Width * m.Box.Height)
            .FirstOrDefault();
        e.Handled = true;
    }

    private static Rect ToRect(NormalizedBox b, double w, double h) => new(b.X * w, b.Y * h, b.Width * w, b.Height * h);

    private static Rect Inflate(Rect r, double by)
    {
        r.Inflate(by, by);
        return r;
    }

    /// <summary>How many screen pixels one unit of this element covers (the photo is scaled to fit).</summary>
    private double ScreenScale()
    {
        if (PresentationSource.FromVisual(this)?.RootVisual is not { } root || !root.IsAncestorOf(this)) return 1;
        var t = TransformToAncestor(root);
        var length = (t.Transform(new Point(1, 0)) - t.Transform(new Point(0, 0))).Length;
        return length > 0 ? length : 1;
    }
}
