using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class PixelCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;

    public static IReadOnlyList<Choice<PixelActionMode>> ModeChoices { get; } =
    [
        new(PixelActionMode.Wait, "Attendre la couleur"),
        new(PixelActionMode.Test, "Tester la couleur"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTimeout), nameof(ShowsVariable))]
    private PixelActionMode _mode;

    [ObservableProperty]
    private string _xText;

    [ObservableProperty]
    private string _yText;

    [ObservableProperty]
    private string _colorHex;

    [ObservableProperty]
    private string _toleranceText;

    [ObservableProperty]
    private string _timeoutText;

    [ObservableProperty]
    private string? _variableName;

    public PixelCommandEditorViewModel(PixelCommand? existing, IDialogService? dialogs)
        : base(existing, "Pixel")
    {
        _dialogs = dialogs;
        var command = existing ?? new PixelCommand();
        _mode = command.Mode;
        _xText = command.X.ToString(CultureInfo.InvariantCulture);
        _yText = command.Y.ToString(CultureInfo.InvariantCulture);
        _colorHex = command.ExpectedColorHex;
        _toleranceText = command.TolerancePercent.ToString(CultureInfo.InvariantCulture);
        _timeoutText = command.TimeoutMs.ToString(CultureInfo.InvariantCulture);
        _variableName = command.VariableName;
    }

    public bool ShowsTimeout => Mode == PixelActionMode.Wait;

    public bool ShowsVariable => Mode == PixelActionMode.Test;

    [RelayCommand]
    private void PickColor()
    {
        var result = _dialogs?.PickPixelColor();
        if (result is null)
        {
            return;
        }

        XText = result.X.ToString(CultureInfo.InvariantCulture);
        YText = result.Y.ToString(CultureInfo.InvariantCulture);
        ColorHex = result.ColorHex;
    }

    public override MacroCommand Build() => new PixelCommand
    {
        Mode = Mode,
        X = IntOr(XText, 0),
        Y = IntOr(YText, 0),
        ExpectedColorHex = ColorHex,
        TolerancePercent = IntOr(ToleranceText, 0),
        TimeoutMs = IntOr(TimeoutText, 10000),
        VariableName = VariableName,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        TryInt(XText, out _) && TryInt(YText, out _) && TryNonNegative(ToleranceText, out _)
        && (!ShowsTimeout || TryNonNegative(TimeoutText, out _))
        && (!ShowsVariable || !string.IsNullOrWhiteSpace(VariableName));
}
