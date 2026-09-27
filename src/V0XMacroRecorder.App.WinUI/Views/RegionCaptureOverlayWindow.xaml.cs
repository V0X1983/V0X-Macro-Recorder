using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using Windows.Graphics;
using Windows.System;

namespace V0XMacroRecorder.App.Views;

/// <summary>Fenêtre transparente plein écran virtuel : glisser-sélectionner un rectangle, capturé au relâchement.</summary>
public sealed partial class RegionCaptureOverlayWindow : Window
{
    private readonly IScreenCapture _screenCapture;
    private readonly IImageCodec _codec;
    private readonly TaskCompletionSource<ImageCaptureResult?> _completion = new();
    private RectRegion _bounds = null!;
    private Windows.Foundation.Point? _dragStartLocal;

    public RegionCaptureOverlayWindow(IScreenCapture screenCapture, IImageCodec codec)
    {
        _screenCapture = screenCapture;
        _codec = codec;
        InitializeComponent();

        _bounds = screenCapture.VirtualScreenBounds;

        var appWindow = AppWindow;
        appWindow.IsShownInSwitchers = false;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }

        appWindow.MoveAndResize(new RectInt32(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height));

        // Capturé avant que cette fenêtre ne soit affichée (Activate(), dans ShowAndWaitAsync) : jamais de nous-mêmes dans le cliché.
        BackgroundImage.Source = ScreenSnapshot.ToWriteableBitmap(screenCapture.Capture(_bounds));
    }

    /// <summary>Affiche l'overlay et attend le résultat (région capturée, ou null si Échap/sélection trop petite).</summary>
    public Task<ImageCaptureResult?> ShowAndWaitAsync()
    {
        Activate();
        RootGrid.Focus(FocusState.Programmatic);
        return _completion.Task;
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragStartLocal = e.GetCurrentPoint(RootGrid).Position;
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelectionVisual(_dragStartLocal.Value, _dragStartLocal.Value);
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragStartLocal is null)
        {
            return;
        }

        UpdateSelectionVisual(_dragStartLocal.Value, e.GetCurrentPoint(RootGrid).Position);
    }

    private async void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragStartLocal is not { } start)
        {
            return;
        }

        var end = e.GetCurrentPoint(RootGrid).Position;
        var region = ToRegion(start, end);
        _dragStartLocal = null;
        if (region.Width < 2 || region.Height < 2)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return; // Sélection trop petite : ignorée, l'utilisateur peut recommencer.
        }

        // Masquer la fenêtre entière (pas seulement son contenu) avant de capturer : le BitBlt de Win32ScreenCapture
        // lit l'écran réel (GetDC(NULL)), qui montre ce que DWM a effectivement composé au-dessus — notre fenêtre
        // plein écran y compris (elle est opaque, voir ScreenSnapshot/RootGrid Background). Collapser seulement les
        // enfants XAML ne suffit pas : la surface de la Window elle-même resterait composée par-dessus le bureau,
        // contrairement à la version WPF (fenêtre réellement transparente via AllowsTransparency, jamais opaque).
        AppWindow.Hide();
        await Task.Delay(150);

        var bitmap = _screenCapture.Capture(region);
        var pngBase64 = Convert.ToBase64String(_codec.EncodePng(bitmap));
        Complete(new ImageCaptureResult(pngBase64, region.Width, region.Height, region));
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Complete(null);
        }
    }

    private void UpdateSelectionVisual(Windows.Foundation.Point start, Windows.Foundation.Point current)
    {
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        SelectionBorder.Margin = new Thickness(x, y, 0, 0);
        SelectionBorder.Width = Math.Abs(current.X - start.X);
        SelectionBorder.Height = Math.Abs(current.Y - start.Y);
    }

    private RectRegion ToRegion(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var scale = Content.XamlRoot.RasterizationScale;
        var x0 = _bounds.X + (int)(Math.Min(start.X, end.X) * scale);
        var y0 = _bounds.Y + (int)(Math.Min(start.Y, end.Y) * scale);
        var x1 = _bounds.X + (int)(Math.Max(start.X, end.X) * scale);
        var y1 = _bounds.Y + (int)(Math.Max(start.Y, end.Y) * scale);
        return new RectRegion { X = x0, Y = y0, Width = x1 - x0, Height = y1 - y0 };
    }

    private void Complete(ImageCaptureResult? result)
    {
        if (_completion.TrySetResult(result))
        {
            Close();
        }
    }
}
