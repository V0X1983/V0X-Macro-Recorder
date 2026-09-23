using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class IfCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;
    private string? _imageTemplatePngBase64;

    public static IReadOnlyList<Choice<ConditionKind>> ConditionKindChoices { get; } =
    [
        new(ConditionKind.WindowExists, "Une fenêtre existe"),
        new(ConditionKind.PixelMatches, "Un pixel a une couleur"),
        new(ConditionKind.ImageFound, "Une image est présente"),
        new(ConditionKind.VariableCompare, "Une variable vérifie une condition"),
        new(ConditionKind.FileExists, "Un fichier existe"),
    ];

    public static IReadOnlyList<Choice<ComparisonOperator>> ComparisonOperatorChoices { get; } =
    [
        new(ComparisonOperator.Equals, "="),
        new(ComparisonOperator.NotEquals, "≠"),
        new(ComparisonOperator.GreaterThan, ">"),
        new(ComparisonOperator.LessThan, "<"),
        new(ComparisonOperator.Contains, "contient"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWindowFields), nameof(ShowsPixelFields), nameof(ShowsImageFields), nameof(ShowsVariableFields), nameof(ShowsFileFields))]
    private ConditionKind _conditionKind;

    [ObservableProperty]
    private bool _negate;

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

    [ObservableProperty]
    private string _imageSummary;

    [ObservableProperty]
    private string _imageToleranceText;

    [ObservableProperty]
    private string? _variableName;

    [ObservableProperty]
    private ComparisonOperator _comparisonOperator;

    [ObservableProperty]
    private string _comparisonValue;

    [ObservableProperty]
    private string? _filePath;

    public IfCommandEditorViewModel(IfCommand? existing, IDialogService? dialogs)
        : base(existing, "Condition (Si…)")
    {
        _dialogs = dialogs;
        var command = existing ?? new IfCommand();
        var spec = command.Condition;
        _negate = command.Negate;
        _conditionKind = spec.Kind;
        _windowTitle = spec.WindowTitle;
        _windowClassName = spec.WindowClassName;
        _pixelXText = spec.PixelX.ToString(CultureInfo.InvariantCulture);
        _pixelYText = spec.PixelY.ToString(CultureInfo.InvariantCulture);
        _pixelColorHex = spec.PixelColorHex;
        _pixelToleranceText = spec.PixelTolerancePercent.ToString(CultureInfo.InvariantCulture);
        _imageTemplatePngBase64 = spec.ImageTemplatePngBase64;
        _imageSummary = DescribeImage();
        _imageToleranceText = spec.ImageTolerancePercent.ToString(CultureInfo.InvariantCulture);
        _variableName = spec.VariableName;
        _comparisonOperator = spec.ComparisonOperator;
        _comparisonValue = spec.ComparisonValue;
        _filePath = spec.FilePath;
    }

    public bool ShowsWindowFields => ConditionKind == ConditionKind.WindowExists;

    public bool ShowsPixelFields => ConditionKind == ConditionKind.PixelMatches;

    public bool ShowsImageFields => ConditionKind == ConditionKind.ImageFound;

    public bool ShowsVariableFields => ConditionKind == ConditionKind.VariableCompare;

    public bool ShowsFileFields => ConditionKind == ConditionKind.FileExists;

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

    private string DescribeImage() => string.IsNullOrEmpty(_imageTemplatePngBase64) ? "Aucun modèle capturé" : "Modèle déjà capturé";

    public override MacroCommand Build() => new IfCommand
    {
        Negate = Negate,
        Condition = new ConditionSpec
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
            VariableName = VariableName,
            ComparisonOperator = ComparisonOperator,
            ComparisonValue = ComparisonValue,
            FilePath = FilePath,
        },
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => ConditionKind switch
    {
        ConditionKind.WindowExists => !string.IsNullOrWhiteSpace(WindowTitle) || !string.IsNullOrWhiteSpace(WindowClassName),
        ConditionKind.PixelMatches => TryInt(PixelXText, out _) && TryInt(PixelYText, out _) && TryNonNegative(PixelToleranceText, out _),
        ConditionKind.ImageFound => !string.IsNullOrEmpty(_imageTemplatePngBase64) && TryNonNegative(ImageToleranceText, out _),
        ConditionKind.VariableCompare => !string.IsNullOrWhiteSpace(VariableName),
        ConditionKind.FileExists => !string.IsNullOrWhiteSpace(FilePath),
        _ => false,
    };
}
