using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class TextCommandEditorViewModel : CommandEditorViewModel
{
    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private string _characterDelayText;

    [ObservableProperty]
    private bool _expandTokens;

    public TextCommandEditorViewModel(TextCommand? existing)
        : base(existing, "Texte")
    {
        var command = existing ?? new TextCommand();
        _text = command.Text;
        _characterDelayText = command.CharacterDelayMs.ToString(CultureInfo.InvariantCulture);
        _expandTokens = command.ExpandTokens;
    }

    public override MacroCommand Build() => new TextCommand
    {
        Text = Text,
        CharacterDelayMs = IntOr(CharacterDelayText, 0),
        ExpandTokens = ExpandTokens,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => Text.Length > 0 && TryNonNegative(CharacterDelayText, out _);
}
