using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class WaitCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;
    private string? _imageTemplatePngBase64;
    private int _imageTemplateWidth;
    private int _imageTemplateHeight;

    public static IReadOnlyList<Choice<WaitMode>> ModeChoices { get; } =
    [
        new(WaitMode.FixedDelay, "Délai fixe ou aléatoire"),
        new(WaitMode.WindowAppears, "Qu'une fenêtre apparaisse"),
        new(WaitMode.WindowDisappears, "Qu'une fenêtre disparaisse"),
        new(WaitMode.PixelMatch, "Qu'un pixel prenne une couleur"),
        new(WaitMode.ImageFound, "Qu'une image apparaisse"),
        new(WaitMode.KeyPress, "Qu'une touche soit pressée"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDuration), nameof(ShowsWindowFields), nameof(ShowsKeyField), nameof(ShowsPixelFields), nameof(ShowsImageFields))]
    private WaitMode _mode;

    [ObservableProperty]
    private string _durationText;

    [ObservableProperty]
    private string _randomExtraText;

    [ObservableProperty]
    private string _timeoutText;

    [ObservableProperty]
    private string? _windowTitle;

    [ObservableProperty]
    private string? _windowClassName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private int _virtualKey;

    [ObservableProperty]
    private string _pixelXText;

    [ObservableProperty]
    private string _pixelYText;

    [ObservableProperty]
    private string _pixelColorHex;

    [ObservableProperty]
    private string _pixelToleranceText;

    [ObservableProperty]
    private string _imageTemplateSummary;

    [ObservableProperty]
    private string _imageToleranceText;

    public WaitCommandEditorViewModel(WaitCommand? existing, IDialogService? dialogs = null)
        : base(existing, "Attente")
    {
        _dialogs = dialogs;
        var command = existing ?? new WaitCommand();
        _mode = command.Mode;
        _durationText = command.DurationMs.ToString(CultureInfo.InvariantCulture);
        _randomExtraText = command.RandomExtraMs.ToString(CultureInfo.InvariantCulture);
        _timeoutText = command.TimeoutMs.ToString(CultureInfo.InvariantCulture);
        _windowTitle = command.WindowTitle;
        _windowClassName = command.WindowClassName;
        _virtualKey = command.VirtualKey;
        _pixelXText = command.PixelX.ToString(CultureInfo.InvariantCulture);
        _pixelYText = command.PixelY.ToString(CultureInfo.InvariantCulture);
        _pixelColorHex = command.PixelColorHex;
        _pixelToleranceText = command.PixelTolerancePercent.ToString(CultureInfo.InvariantCulture);
        _imageTemplatePngBase64 = command.ImageTemplatePngBase64;
        _imageTemplateSummary = DescribeImageTemplate();
        _imageToleranceText = command.ImageTolerancePercent.ToString(CultureInfo.InvariantCulture);
    }

    public override bool ShowsDelay => false;

    public bool ShowsDuration => Mode == WaitMode.FixedDelay;

    public bool ShowsWindowFields => Mode is WaitMode.WindowAppears or WaitMode.WindowDisappears;

    public bool ShowsKeyField => Mode == WaitMode.KeyPress;

    public bool ShowsPixelFields => Mode == WaitMode.PixelMatch;

    public bool ShowsImageFields => Mode == WaitMode.ImageFound;

    public bool ShowsTimeout => Mode != WaitMode.FixedDelay;

    /// <summary>0 (n'importe quelle touche) n'est pas dans <see cref="VirtualKeyNames"/> : libellé dédié.</summary>
    public string KeyLabel => VirtualKey == 0 ? "N'importe quelle touche" : VirtualKeyNames.GetName(VirtualKey);

    [RelayCommand]
    private void PickColor()
    {
        var result = _dialogs?.PickPixelColor();
        if (result is null)
        {
            return;
        }

        PixelXText = result.X.ToString(CultureInfo.InvariantCulture);
        PixelYText = result.Y.ToString(CultureInfo.InvariantCulture);
        PixelColorHex = result.ColorHex;
    }

    [RelayCommand]
    private void CaptureImage()
    {
        var result = _dialogs?.CaptureImageRegion();
        if (result is null)
        {
            return;
        }

        _imageTemplatePngBase64 = result.TemplatePngBase64;
        _imageTemplateWidth = result.Width;
        _imageTemplateHeight = result.Height;
        ImageTemplateSummary = DescribeImageTemplate();
    }

    private string DescribeImageTemplate() =>
        string.IsNullOrEmpty(_imageTemplatePngBase64) ? "Aucun modèle capturé" : $"Modèle capturé : {_imageTemplateWidth}×{_imageTemplateHeight}";

    public override MacroCommand Build() => new WaitCommand
    {
        Mode = Mode,
        DurationMs = IntOr(DurationText, 1000),
        RandomExtraMs = IntOr(RandomExtraText, 0),
        TimeoutMs = IntOr(TimeoutText, 10000),
        WindowTitle = WindowTitle,
        WindowClassName = WindowClassName,
        VirtualKey = VirtualKey,
        PixelX = IntOr(PixelXText, 0),
        PixelY = IntOr(PixelYText, 0),
        PixelColorHex = PixelColorHex,
        PixelTolerancePercent = IntOr(PixelToleranceText, 0),
        ImageTemplatePngBase64 = _imageTemplatePngBase64,
        ImageTolerancePercent = IntOr(ImageToleranceText, 10),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        (!ShowsDuration || (TryNonNegative(DurationText, out _) && TryNonNegative(RandomExtraText, out _)))
        && (!ShowsTimeout || TryNonNegative(TimeoutText, out _))
        && (!ShowsWindowFields || !string.IsNullOrWhiteSpace(WindowTitle) || !string.IsNullOrWhiteSpace(WindowClassName))
        && (!ShowsPixelFields || (TryInt(PixelXText, out _) && TryInt(PixelYText, out _) && TryNonNegative(PixelToleranceText, out _)))
        && (!ShowsImageFields || (!string.IsNullOrEmpty(_imageTemplatePngBase64) && TryNonNegative(ImageToleranceText, out _)));
}
