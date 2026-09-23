using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class CommentCommandEditorViewModel : CommandEditorViewModel
{
    [ObservableProperty]
    private string _text;

    public CommentCommandEditorViewModel(CommentCommand? existing)
        : base(existing, "Commentaire")
    {
        _text = existing?.Text ?? "";
    }

    public override bool ShowsDelay => false;

    public override MacroCommand Build() => new CommentCommand { Text = Text, DelayMs = Delay };

    protected override bool AreFieldsValid() => true;
}
