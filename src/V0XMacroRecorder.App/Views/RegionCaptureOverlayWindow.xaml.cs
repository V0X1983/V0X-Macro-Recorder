using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.Views;

/// <summary>Fenêtre transparente plein écran virtuel : glisser-sélectionner un rectangle, capturé au relâchement.</summary>
public partial class RegionCaptureOverlayWindow : Window
{
    private readonly IScreenCapture _screenCapture;
    private readonly IImageCodec _codec;
    private Point? _dragStartScreen;

    public RegionCaptureOverlayWindow(IScreenCapture screenCapture, IImageCodec codec)
    {
        _screenCapture = screenCapture;
        _codec = codec;
        InitializeComponent();
    }

    public RectRegion? CapturedRegion { get; private set; }

    public string CapturedPngBase64 { get; private set; } = "";

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Activate();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelectionVisual(e.GetPosition(this), e.GetPosition(this));
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartScreen is null)
        {
            return;
        }

        var startLocal = PointFromScreen(_dragStartScreen.Value);
        UpdateSelectionVisual(startLocal, e.GetPosition(this));
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStartScreen is not { } startScreen)
        {
            return;
        }

        var endScreen = PointToScreen(e.GetPosition(this));
        var region = ToRegion(startScreen, endScreen);
        if (region.Width < 2 || region.Height < 2)
        {
            _dragStartScreen = null;
            SelectionBorder.Visibility = Visibility.Collapsed;
            return; // Sélection trop petite : ignorée, l'utilisateur peut recommencer.
        }

        // Masquer l'overlay (teinte de fond + rectangle de sélection) avant de capturer : sinon le modèle
        // capturé contient la teinte de cette fenêtre elle-même (vérifié réellement : jusqu'à ~20% d'écart
        // par canal sur les pixels du rectangle sélectionné) au lieu du vrai contenu de l'écran, et ne
        // correspondrait alors plus jamais à l'écran réel pendant la lecture — la recherche d'image
        // échouerait systématiquement. On utilise Visibility = Hidden (et non Hide()) : Hide() met fin à la
        // session modale ouverte par ShowDialog(), ce qui rend impossible tout DialogResult ultérieur.
        // Une pause laisse au compositeur DWM le temps de redessiner le bureau réel avant le BitBlt.
        Visibility = Visibility.Hidden;
        System.Threading.Thread.Sleep(150);

        var bitmap = _screenCapture.Capture(region);
        CapturedRegion = region;
        CapturedPngBase64 = Convert.ToBase64String(_codec.EncodePng(bitmap));
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void UpdateSelectionVisual(Point startLocal, Point currentLocal)
    {
        var x = Math.Min(startLocal.X, currentLocal.X);
        var y = Math.Min(startLocal.Y, currentLocal.Y);
        var width = Math.Abs(currentLocal.X - startLocal.X);
        var height = Math.Abs(currentLocal.Y - startLocal.Y);

        SelectionBorder.Margin = new Thickness(x, y, 0, 0);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private static RectRegion ToRegion(Point start, Point end) => new()
    {
        X = (int)Math.Min(start.X, end.X),
        Y = (int)Math.Min(start.Y, end.Y),
        Width = (int)Math.Abs(end.X - start.X),
        Height = (int)Math.Abs(end.Y - start.Y),
    };
}
