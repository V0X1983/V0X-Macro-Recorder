using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class ImageSearchCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;

    public static IReadOnlyList<Choice<MouseButton>> ButtonChoices { get; } =
    [
        new(MouseButton.Left, "Gauche"),
        new(MouseButton.Right, "Droit"),
        new(MouseButton.Middle, "Milieu"),
    ];

    private string _templatePngBase64;

    [ObservableProperty]
    private string _templateSummary;

    [ObservableProperty]
    private string _toleranceText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsButton))]
    private bool _clickIfFound;

    [ObservableProperty]
    private MouseButton _clickButton;

    [ObservableProperty]
    private string _timeoutText;

    [ObservableProperty]
    private string? _foundXVariable;

    [ObservableProperty]
    private string? _foundYVariable;

    private int _templateWidth;
    private int _templateHeight;

    public ImageSearchCommandEditorViewModel(ImageSearchCommand? existing, IDialogService? dialogs)
        : base(existing, "Recherche d'image")
    {
        _dialogs = dialogs;
        var command = existing ?? new ImageSearchCommand();
        _templatePngBase64 = command.TemplatePngBase64;
        _templateWidth = command.TemplateWidth;
        _templateHeight = command.TemplateHeight;
        _templateSummary = DescribeTemplate();
        _toleranceText = command.TolerancePercent.ToString(CultureInfo.InvariantCulture);
        _clickIfFound = command.ClickIfFound;
        _clickButton = command.ClickButton;
        _timeoutText = command.TimeoutMs.ToString(CultureInfo.InvariantCulture);
        _foundXVariable = command.FoundXVariable;
        _foundYVariable = command.FoundYVariable;
    }

    public bool ShowsButton => ClickIfFound;

    public bool HasTemplate => _templatePngBase64.Length > 0;

    [RelayCommand]
    private void CaptureRegion()
    {
        var result = _dialogs?.CaptureImageRegion();
        if (result is null)
        {
            return;
        }

        _templatePngBase64 = result.TemplatePngBase64;
        _templateWidth = result.Width;
        _templateHeight = result.Height;
        TemplateSummary = DescribeTemplate();
        OnPropertyChanged(nameof(HasTemplate));
    }

    private string DescribeTemplate() =>
        _templatePngBase64.Length == 0 ? "Aucun modèle capturé" : $"Modèle capturé : {_templateWidth}×{_templateHeight}";

    public override MacroCommand Build() => new ImageSearchCommand
    {
        TemplatePngBase64 = _templatePngBase64,
        TemplateWidth = _templateWidth,
        TemplateHeight = _templateHeight,
        TolerancePercent = IntOr(ToleranceText, 10),
        ClickIfFound = ClickIfFound,
        ClickButton = ClickButton,
        TimeoutMs = IntOr(TimeoutText, 10000),
        FoundXVariable = FoundXVariable,
        FoundYVariable = FoundYVariable,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        HasTemplate && TryNonNegative(ToleranceText, out _) && TryNonNegative(TimeoutText, out _);
}
