using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class ScriptCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    private string _code;

    [ObservableProperty]
    private string _timeoutText;

    [ObservableProperty]
    private string? _testResultMessage;

    [ObservableProperty]
    private bool _isTesting;

    public ScriptCommandEditorViewModel(ScriptCommand? existing, IDialogService? dialogs = null)
        : base(existing, "Script C#")
    {
        _dialogs = dialogs;
        var command = existing ?? new ScriptCommand();
        _code = command.Code;
        _timeoutText = command.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        IsTesting = true;
        TestResultMessage = "Exécution du script…";
        TestCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await _dialogs.TestScriptAsync(Code, IntOr(TimeoutText, 10));
            TestResultMessage = result.Success ? "Le script s'est exécuté sans erreur." : $"Erreur : {result.ErrorMessage}";
        }
        finally
        {
            IsTesting = false;
            TestCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanTest => !IsTesting && !string.IsNullOrWhiteSpace(Code);

    public override MacroCommand Build() => new ScriptCommand
    {
        Code = Code,
        TimeoutSeconds = IntOr(TimeoutText, 10),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        !string.IsNullOrWhiteSpace(Code) && TryNonNegative(TimeoutText, out var timeout) && timeout > 0;
}
