using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
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
    private BitmapImage? _templatePreview;

    [ObservableProperty]
    private string _toleranceText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsButton))]
    private bool _clickIfFound;

    [ObservableProperty]
    private MouseButton _clickButton;

    [ObservableProperty]
    private bool _doubleClick;

    [ObservableProperty]
    private string _timeoutText;

    [ObservableProperty]
    private string? _foundXVariable;

    [ObservableProperty]
    private string? _foundYVariable;

    [ObservableProperty]
    private string? _testResultMessage;

    [ObservableProperty]
    private bool _isTesting;

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
        _templatePreview = DecodePreview(_templatePngBase64);
        _toleranceText = command.TolerancePercent.ToString(CultureInfo.InvariantCulture);
        _clickIfFound = command.ClickIfFound;
        _clickButton = command.ClickButton;
        _doubleClick = command.DoubleClick;
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
        TemplatePreview = DecodePreview(_templatePngBase64);
        TestResultMessage = null;
        OnPropertyChanged(nameof(HasTemplate));
        TestCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        IsTesting = true;
        TestResultMessage = "Recherche en cours…";
        TestCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await _dialogs.TestImageSearchAsync(_templatePngBase64, IntOr(ToleranceText, 10), IntOr(TimeoutText, 10000));
            TestResultMessage = result.Found
                ? $"Image trouvée à ({result.X}, {result.Y})."
                : "Image non trouvée.";
        }
        finally
        {
            IsTesting = false;
            TestCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanTest => !IsTesting && HasTemplate;

    private string DescribeTemplate() =>
        _templatePngBase64.Length == 0 ? "Aucun modèle capturé" : $"Modèle capturé : {_templateWidth}×{_templateHeight}";

    /// <summary>Décode le PNG en aperçu bitmap affiché dans l'éditeur ; null si aucun modèle capturé. Gelé (<see cref="Freezable.Freeze"/>) car créé hors du thread UI n'est pas garanti sans ça.</summary>
    private static BitmapImage? DecodePreview(string templatePngBase64)
    {
        if (templatePngBase64.Length == 0)
        {
            return null;
        }

        var bytes = Convert.FromBase64String(templatePngBase64);
        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public override MacroCommand Build() => new ImageSearchCommand
    {
        TemplatePngBase64 = _templatePngBase64,
        TemplateWidth = _templateWidth,
        TemplateHeight = _templateHeight,
        TolerancePercent = IntOr(ToleranceText, 10),
        ClickIfFound = ClickIfFound,
        ClickButton = ClickButton,
        DoubleClick = DoubleClick,
        TimeoutMs = IntOr(TimeoutText, 10000),
        FoundXVariable = FoundXVariable,
        FoundYVariable = FoundYVariable,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        HasTemplate && TryNonNegative(ToleranceText, out _) && TryNonNegative(TimeoutText, out _);
}
