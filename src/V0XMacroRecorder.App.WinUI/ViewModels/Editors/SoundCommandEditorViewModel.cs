using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class SoundCommandEditorViewModel : CommandEditorViewModel
{
    [ObservableProperty]
    private string _filePath;

    [ObservableProperty]
    private bool _waitForCompletion;

    public SoundCommandEditorViewModel(SoundCommand? existing)
        : base(existing, "Jouer un son")
    {
        var command = existing ?? new SoundCommand();
        _filePath = command.FilePath;
        _waitForCompletion = command.WaitForCompletion;
    }

    public override MacroCommand Build() => new SoundCommand
    {
        FilePath = FilePath,
        WaitForCompletion = WaitForCompletion,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(FilePath);
}
