using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using V0XMacroRecorder.App.ViewModels.Editors;

namespace V0XMacroRecorder.App.Views;

public partial class CommandEditorWindow : Window
{
    public CommandEditorWindow(CommandEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    /// <summary>Coloration syntaxique : la propriété n'est pas assignable en XAML (type <c>IHighlightingDefinition</c>, pas une chaîne).</summary>
    private void ScriptEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextEditor editor)
        {
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }
    }

    /// <summary>PasswordBox.Password n'est jamais liable en XAML (protection WPF) : lu ici et copié dans le ViewModel.</summary>
    private void SecureInputSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { Tag: "SecureInputSecret" } box && DataContext is SecureInputCommandEditorViewModel secure)
        {
            secure.NewSecret = box.Password;
        }
    }

    /// <summary>Champ « Touche » : la touche pressée devient la touche de la commande (Tab, Échap et Entrée comprises).</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.OriginalSource is not TextBox { Tag: "KeyCapture" or "WaitKeyCapture" } textBox)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey is < 1 or > 254)
        {
            e.Handled = true;
            return;
        }

        switch (DataContext)
        {
            case KeyboardCommandEditorViewModel keyboard:
                keyboard.VirtualKey = virtualKey;
                break;

            // Échap dans le champ Attente/Pause = « n'importe quelle touche » (VirtualKey 0), plutôt qu'une valeur figée.
            case WaitCommandEditorViewModel wait when (string?)textBox.Tag == "WaitKeyCapture":
                wait.VirtualKey = key == Key.Escape ? 0 : virtualKey;
                break;

            case PauseCommandEditorViewModel pause:
                pause.VirtualKey = key == Key.Escape ? 0 : virtualKey;
                break;
        }

        e.Handled = true;
    }
}
