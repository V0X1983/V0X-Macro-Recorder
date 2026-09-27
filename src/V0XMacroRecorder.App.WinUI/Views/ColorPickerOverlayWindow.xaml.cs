using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using Windows.Graphics;
using Windows.System;

namespace V0XMacroRecorder.App.Views;

/// <summary>
/// Fenêtre transparente plein écran virtuel : suit le pixel sous le curseur (pipette) jusqu'au clic ou Échap.
/// Contrairement à WPF (<c>ShowDialog</c>, bloquant), WinUI 3 n'a pas de fenêtre modale : <see cref="ShowAndWaitAsync"/>
/// affiche la fenêtre et attend sa fermeture via un <see cref="TaskCompletionSource{T}"/>, complété par
/// <see cref="RootGrid_PointerPressed"/>/<see cref="RootGrid_KeyDown"/> (Échap) avant l'appel à <see cref="Window.Close"/>.
/// </summary>
public sealed partial class ColorPickerOverlayWindow : Window
{
    private readonly IPixelReader _pixelReader;
    private readonly TaskCompletionSource<PixelColorPickResult?> _completion = new();
    private RectRegion _bounds = null!;

    public ColorPickerOverlayWindow(IPixelReader pixelReader, IScreenCapture screenCapture)
    {
        _pixelReader = pixelReader;
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

        // Capturé avant l'affichage (Activate(), dans ShowAndWaitAsync) : jamais de nous-mêmes dans le cliché. Sert
        // aussi de garde-fou pour la précision de la pipette (voir RootGrid_PointerMoved/Pressed) : IPixelReader lit
        // l'écran réel, qui montre notre propre fenêtre opaque tant qu'elle est affichée — comme ce cliché reproduit
        // fidèlement le bureau au pixel près, la couleur lue reste correcte tant que le bureau ne change pas pendant
        // l'opération (hypothèse raisonnable pour un outil de sélection de couleur ponctuel).
        BackgroundImage.Source = ScreenSnapshot.ToWriteableBitmap(screenCapture.Capture(_bounds));
    }

    /// <summary>Affiche l'overlay et attend le résultat (pixel choisi, ou null si Échap).</summary>
    public Task<PixelColorPickResult?> ShowAndWaitAsync()
    {
        Activate();
        RootGrid.Focus(FocusState.Programmatic);
        return _completion.Task;
    }

    private (int X, int Y) ToScreenPixel(PointerRoutedEventArgs e)
    {
        var scale = Content.XamlRoot.RasterizationScale;
        var point = e.GetCurrentPoint(RootGrid).Position;
        return (_bounds.X + (int)(point.X * scale), _bounds.Y + (int)(point.Y * scale));
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var (x, y) = ToScreenPixel(e);
        if (_pixelReader.GetPixelColor(x, y) is not { } color)
        {
            return;
        }

        PreviewLabel.Text = $"{ColorMatch.ToHex(color)}  ({x}, {y})";
        PreviewSwatch.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, color.R, color.G, color.B));
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var (x, y) = ToScreenPixel(e);
        var hex = _pixelReader.GetPixelColor(x, y) is { } color ? ColorMatch.ToHex(color) : "#000000";
        Complete(new PixelColorPickResult(x, y, hex));
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Complete(null);
        }
    }

    private void Complete(PixelColorPickResult? result)
    {
        if (_completion.TrySetResult(result))
        {
            Close();
        }
    }
}
