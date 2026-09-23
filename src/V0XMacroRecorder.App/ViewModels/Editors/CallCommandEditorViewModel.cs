using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class CallCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDialogService? _dialogs;

    [ObservableProperty]
    private string _macroFilePath;

    public CallCommandEditorViewModel(CallCommand? existing, IDialogService? dialogs)
        : base(existing, "Appeler une autre macro")
    {
        _dialogs = dialogs;
        _macroFilePath = (existing ?? new CallCommand()).MacroFilePath;
    }

    [RelayCommand]
    private void Browse()
    {
        var path = _dialogs?.PickAnyFile("Choisir une macro", "Macros V0X (*.v0xmacro)|*.v0xmacro|Tous les fichiers (*.*)|*.*");
        if (path is not null)
        {
            MacroFilePath = path;
        }
    }

    public override MacroCommand Build() => new CallCommand { MacroFilePath = MacroFilePath, DelayMs = Delay };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(MacroFilePath);
}
