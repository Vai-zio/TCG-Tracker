using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PokemonCardManager.App.Controls;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

/// <summary>
/// Mouse handling for the photo viewer. This is pure view behaviour (scrolling and zooming the picture),
/// so it lives here rather than in the ViewModel; the ViewModel only holds the zoom level and rotation.
/// </summary>
public partial class InspectionView : UserControl
{
    private Point? _panStart;
    private Point _panOrigin;

    public InspectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // The photo may finish loading before this view exists, so fit once more when it appears.
        Loaded += (_, _) => OnFitRequested(this, EventArgs.Empty);
    }

    private InspectionViewModel? ViewModel => DataContext as InspectionViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is InspectionViewModel old)
        {
            old.FitRequested -= OnFitRequested;
            old.BoxesChanged -= OnBoxesChanged;
        }
        if (e.NewValue is InspectionViewModel now)
        {
            now.FitRequested += OnFitRequested;
            now.BoxesChanged += OnBoxesChanged;
        }
    }

    private void OnBoxesChanged(object? sender, EventArgs e) => Overlay.Refresh();

    /// <summary>Zoom so the whole photo fits the viewer, taking rotation into account.</summary>
    private void OnFitRequested(object? sender, EventArgs e)
    {
        // Wait for layout, so the viewer's size is known.
        Dispatcher.BeginInvoke(() =>
        {
            if (ViewModel?.SelectedPhoto is not { } photo || photo.Info.Width == 0 || photo.Info.Height == 0) return;
            var sideways = ViewModel.Rotation is 90 or 270;
            double w = sideways ? photo.Info.Height : photo.Info.Width;
            double h = sideways ? photo.Info.Width : photo.Info.Height;
            var available = new Size(Math.Max(1, Viewer.ViewportWidth - 24), Math.Max(1, Viewer.ViewportHeight - 24));
            ViewModel.Zoom = Math.Min(available.Width / w, available.Height / h);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Mouse wheel zooms around the pointer, so the spot under it stays put.</summary>
    private void OnViewerMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        var pointer = e.GetPosition(Viewer);
        var relativeX = Viewer.ExtentWidth > 0 ? (Viewer.HorizontalOffset + pointer.X) / Viewer.ExtentWidth : 0.5;
        var relativeY = Viewer.ExtentHeight > 0 ? (Viewer.VerticalOffset + pointer.Y) / Viewer.ExtentHeight : 0.5;

        vm.Zoom *= e.Delta > 0 ? 1.15 : 1 / 1.15;
        Viewer.UpdateLayout();
        Viewer.ScrollToHorizontalOffset(relativeX * Viewer.ExtentWidth - pointer.X);
        Viewer.ScrollToVerticalOffset(relativeY * Viewer.ExtentHeight - pointer.Y);
        e.Handled = true;
    }

    // Left-drag pans in Look mode; right-drag pans in every mode (so you can move while marking).
    private void OnViewerLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.Mode == OverlayMode.Look) StartPan(e);
    }

    private void OnViewerRightDown(object sender, MouseButtonEventArgs e) => StartPan(e);

    private void StartPan(MouseButtonEventArgs e)
    {
        _panStart = e.GetPosition(Viewer);
        _panOrigin = new Point(Viewer.HorizontalOffset, Viewer.VerticalOffset);
        Viewer.CaptureMouse();
        Viewer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnViewerMouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is not { } start) return;
        var now = e.GetPosition(Viewer);
        Viewer.ScrollToHorizontalOffset(_panOrigin.X - (now.X - start.X));
        Viewer.ScrollToVerticalOffset(_panOrigin.Y - (now.Y - start.Y));
    }

    private void OnViewerButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is null) return;
        _panStart = null;
        Viewer.ReleaseMouseCapture();
        Viewer.Cursor = null;
        e.Handled = true;
    }
}
