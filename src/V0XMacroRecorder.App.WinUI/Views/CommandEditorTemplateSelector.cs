using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using V0XMacroRecorder.App.ViewModels.Editors;

namespace V0XMacroRecorder.App.Views;

/// <summary>
/// Sélectionne le <see cref="DataTemplate"/> du type de commande édité. Remplace le mécanisme WPF de "DataTemplate
/// implicite" (<c>DataTemplate DataType="{x:Type ed:X}"</c> appliqué automatiquement par tout <see cref="ContentControl"/>) :
/// WinUI 3 n'a pas cet équivalent pour <see cref="ContentControl.Content"/> — chaque <see cref="DataTemplate"/> doit
/// être nommé (<c>x:Key</c>) et sélectionné explicitement ici.
/// </summary>
public sealed class CommandEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MouseTemplate { get; set; }
    public DataTemplate? KeyboardTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? ClipboardTemplate { get; set; }
    public DataTemplate? WaitTemplate { get; set; }
    public DataTemplate? LaunchTemplate { get; set; }
    public DataTemplate? OpenUrlTemplate { get; set; }
    public DataTemplate? WindowTemplate { get; set; }
    public DataTemplate? PixelTemplate { get; set; }
    public DataTemplate? SoundTemplate { get; set; }
    public DataTemplate? MessageTemplate { get; set; }
    public DataTemplate? ImageSearchTemplate { get; set; }
    public DataTemplate? IfTemplate { get; set; }
    public DataTemplate? LoopTemplate { get; set; }
    public DataTemplate? VariableTemplate { get; set; }
    public DataTemplate? LabelTemplate { get; set; }
    public DataTemplate? GotoTemplate { get; set; }
    public DataTemplate? StopTemplate { get; set; }
    public DataTemplate? PauseTemplate { get; set; }
    public DataTemplate? CallTemplate { get; set; }
    public DataTemplate? CommentTemplate { get; set; }
    public DataTemplate? ScriptTemplate { get; set; }
    public DataTemplate? SecureInputTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        MouseCommandEditorViewModel => MouseTemplate,
        KeyboardCommandEditorViewModel => KeyboardTemplate,
        TextCommandEditorViewModel => TextTemplate,
        ClipboardCommandEditorViewModel => ClipboardTemplate,
        WaitCommandEditorViewModel => WaitTemplate,
        LaunchCommandEditorViewModel => LaunchTemplate,
        OpenUrlCommandEditorViewModel => OpenUrlTemplate,
        WindowCommandEditorViewModel => WindowTemplate,
        PixelCommandEditorViewModel => PixelTemplate,
        SoundCommandEditorViewModel => SoundTemplate,
        MessageCommandEditorViewModel => MessageTemplate,
        ImageSearchCommandEditorViewModel => ImageSearchTemplate,
        IfCommandEditorViewModel => IfTemplate,
        LoopCommandEditorViewModel => LoopTemplate,
        VariableCommandEditorViewModel => VariableTemplate,
        LabelCommandEditorViewModel => LabelTemplate,
        GotoCommandEditorViewModel => GotoTemplate,
        StopCommandEditorViewModel => StopTemplate,
        PauseCommandEditorViewModel => PauseTemplate,
        CallCommandEditorViewModel => CallTemplate,
        CommentCommandEditorViewModel => CommentTemplate,
        ScriptCommandEditorViewModel => ScriptTemplate,
        SecureInputCommandEditorViewModel => SecureInputTemplate,
        _ => null,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
