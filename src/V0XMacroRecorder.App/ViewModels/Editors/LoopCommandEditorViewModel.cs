using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class LoopCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;

    public static IReadOnlyList<Choice<LoopMode>> ModeChoices { get; } =
    [
        new(LoopMode.RepeatCount, "Répéter N fois"),
        new(LoopMode.While, "Tant que"),
        new(LoopMode.ForEachLine, "Pour chaque ligne d'un fichier"),
    ];

    public static IReadOnlyList<Choice<ConditionKind>> ConditionKindChoices { get; } = IfCommandEditorViewModel.ConditionKindChoices;

    public static IReadOnlyList<Choice<ComparisonOperator>> ComparisonOperatorChoices { get; } = IfCommandEditorViewModel.ComparisonOperatorChoices;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRepeatCount), nameof(ShowsWhile), nameof(ShowsForEachLine))]
    private LoopMode _mode;

    [ObservableProperty]
    private string _repeatCountText;

    // --- Tant que : mêmes champs de condition que Si (voir IfCommandEditorViewModel). ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWindowFields), nameof(ShowsPixelFields), nameof(ShowsImageFields), nameof(ShowsVariableCompareFields), nameof(ShowsFileFields))]
    private ConditionKind _conditionKind;

    [ObservableProperty]
    private string? _windowTitle;

    [ObservableProperty]
    private string? _windowClassName;

    [ObservableProperty]
    private string _pixelXText;

    [ObservableProperty]
    private string _pixelYText;

    [ObservableProperty]
    private string _pixelColorHex;

    [ObservableProperty]
    private string _pixelToleranceText;

    private string? _imageTemplatePngBase64;

    [ObservableProperty]
    private string _imageSummary;

    [ObservableProperty]
    private string _imageToleranceText;

    [ObservableProperty]
    private string? _conditionVariableName;

    [ObservableProperty]
    private ComparisonOperator _comparisonOperator;

    [ObservableProperty]
    private string _comparisonValue;

    [ObservableProperty]
    private string? _conditionFilePath;

    // --- Pour chaque ligne ---
    [ObservableProperty]
    private string? _linesFilePath;

    [ObservableProperty]
    private string? _lineVariableName;

    public LoopCommandEditorViewModel(LoopCommand? existing, IDialogService? dialogs)
        : base(existing, "Boucle")
    {
        _dialogs = dialogs;
        var command = existing ?? new LoopCommand();
        _mode = command.Mode;
        _repeatCountText = command.RepeatCount.ToString(CultureInfo.InvariantCulture);

        var spec = command.WhileCondition ?? new ConditionSpec();
        _conditionKind = spec.Kind;
        _windowTitle = spec.WindowTitle;
        _windowClassName = spec.WindowClassName;
        _pixelXText = spec.PixelX.ToString(CultureInfo.InvariantCulture);
        _pixelYText = spec.PixelY.ToString(CultureInfo.InvariantCulture);
        _pixelColorHex = spec.PixelColorHex;
        _pixelToleranceText = spec.PixelTolerancePercent.ToString(CultureInfo.InvariantCulture);
        _imageTemplatePngBase64 = spec.ImageTemplatePngBase64;
        _imageSummary = string.IsNullOrEmpty(_imageTemplatePngBase64) ? "Aucun modèle capturé" : "Modèle déjà capturé";
        _imageToleranceText = spec.ImageTolerancePercent.ToString(CultureInfo.InvariantCulture);
        _conditionVariableName = spec.VariableName;
        _comparisonOperator = spec.ComparisonOperator;
        _comparisonValue = spec.ComparisonValue;
        _conditionFilePath = spec.FilePath;

        _linesFilePath = command.FilePath;
        _lineVariableName = command.LineVariableName;
    }

    public bool ShowsRepeatCount => Mode == LoopMode.RepeatCount;

    public bool ShowsWhile => Mode == LoopMode.While;

    public bool ShowsForEachLine => Mode == LoopMode.ForEachLine;

    public bool ShowsWindowFields => ShowsWhile && ConditionKind == ConditionKind.WindowExists;

    public bool ShowsPixelFields => ShowsWhile && ConditionKind == ConditionKind.PixelMatches;

    public bool ShowsImageFields => ShowsWhile && ConditionKind == ConditionKind.ImageFound;

    public bool ShowsVariableCompareFields => ShowsWhile && ConditionKind == ConditionKind.VariableCompare;

    public bool ShowsFileFields => ShowsWhile && ConditionKind == ConditionKind.FileExists;

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
        ImageSummary = $"Modèle capturé : {result.Width}×{result.Height}";
    }

    public override MacroCommand Build() => new LoopCommand
    {
        Mode = Mode,
        RepeatCount = IntOr(RepeatCountText, 1),
        WhileCondition = new ConditionSpec
        {
            Kind = ConditionKind,
            WindowTitle = WindowTitle,
            WindowClassName = WindowClassName,
            PixelX = IntOr(PixelXText, 0),
            PixelY = IntOr(PixelYText, 0),
            PixelColorHex = PixelColorHex,
            PixelTolerancePercent = IntOr(PixelToleranceText, 0),
            ImageTemplatePngBase64 = _imageTemplatePngBase64,
            ImageTolerancePercent = IntOr(ImageToleranceText, 10),
            VariableName = ConditionVariableName,
            ComparisonOperator = ComparisonOperator,
            ComparisonValue = ComparisonValue,
            FilePath = ConditionFilePath,
        },
        FilePath = LinesFilePath,
        LineVariableName = LineVariableName,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => Mode switch
    {
        LoopMode.RepeatCount => TryInt(RepeatCountText, out var n) && n >= 1,
        LoopMode.ForEachLine => !string.IsNullOrWhiteSpace(LinesFilePath) && !string.IsNullOrWhiteSpace(LineVariableName),
        _ => ConditionKind switch
        {
            ConditionKind.WindowExists => !string.IsNullOrWhiteSpace(WindowTitle) || !string.IsNullOrWhiteSpace(WindowClassName),
            ConditionKind.PixelMatches => TryInt(PixelXText, out _) && TryInt(PixelYText, out _),
            ConditionKind.ImageFound => !string.IsNullOrEmpty(_imageTemplatePngBase64),
            ConditionKind.VariableCompare => !string.IsNullOrWhiteSpace(ConditionVariableName),
            ConditionKind.FileExists => !string.IsNullOrWhiteSpace(ConditionFilePath),
            _ => false,
        },
    };
}
