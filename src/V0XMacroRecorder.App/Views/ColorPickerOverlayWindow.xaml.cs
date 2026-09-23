using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.App.Views;

/// <summary>Fenêtre transparente plein écran virtuel : suit le pixel sous le curseur (pipette) jusqu'au clic ou Échap.</summary>
public partial class ColorPickerOverlayWindow : Window
{
    private readonly IPixelReader _pixelReader;

    public ColorPickerOverlayWindow(IPixelReader pixelReader)
    {
        _pixelReader = pixelReader;
        InitializeComponent();
    }

    public int PickedX { get; private set; }

    public int PickedY { get; private set; }

    public string PickedColorHex { get; private set; } = "#000000";

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Activate();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var (x, y) = ScreenPoint(e);
        var color = _pixelReader.GetPixelColor(x, y);
        if (color is not { } c)
        {
            return;
        }

        var hex = ColorMatch.ToHex(c);
        PreviewLabel.Text = $"{hex}  ({x}, {y})";
        PreviewSwatch.Fill = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var (x, y) = ScreenPoint(e);
        PickedX = x;
        PickedY = y;
        PickedColorHex = _pixelReader.GetPixelColor(x, y) is { } c ? ColorMatch.ToHex(c) : "#000000";
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private (int X, int Y) ScreenPoint(MouseEventArgs e)
    {
        var point = PointToScreen(e.GetPosition(this));
        return ((int)point.X, (int)point.Y);
    }
}
