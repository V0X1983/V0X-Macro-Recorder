using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.ViewModels.Editors;
using Windows.System;

namespace V0XMacroRecorder.App.Views;

public sealed partial class CommandEditorWindow : Window
{
    private readonly CommandEditorViewModel _viewModel;
    private readonly TaskCompletionSource<bool> _completion = new();

    public CommandEditorWindow(CommandEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Title = viewModel.Title;

        RootGrid.DataContext = viewModel;
        RootGrid.KeyDown += OnRootKeyDown;
        WindowSizing.Set(this, 480, 640);
    }

    /// <summary>Affiche la fenêtre et attend sa fermeture (OK ou Annuler).</summary>
    public Task<bool> ShowAndWaitAsync()
    {
        Activate();
        return _completion.Task;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Complete(true);

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Complete(false);

    private void Complete(bool result)
    {
        if (_completion.TrySetResult(result))
        {
            Close();
        }
    }

    /// <summary>PasswordBox.Password n'est jamais liable en XAML (protection native) : lu ici et copié dans le ViewModel.</summary>
    private void SecureInputSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { Tag: "SecureInputSecret" } box && _viewModel is SecureInputCommandEditorViewModel secure)
        {
            secure.NewSecret = box.Password;
        }
    }

    /// <summary>Champ « Touche » : la touche pressée devient la touche de la commande (Tab, Échap et Entrée comprises).</summary>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (FocusManager.GetFocusedElement(Content.XamlRoot) is not TextBox { Tag: string tag } textBox || tag is not ("KeyCapture" or "WaitKeyCapture"))
        {
            return;
        }

        var virtualKey = (int)e.Key;
        if (virtualKey is < 1 or > 254)
        {
            e.Handled = true;
            return;
        }

        switch (_viewModel)
        {
            case KeyboardCommandEditorViewModel keyboard:
                keyboard.VirtualKey = virtualKey;
                break;

            // Échap dans le champ Attente/Pause = « n'importe quelle touche » (VirtualKey 0), plutôt qu'une valeur figée.
            case WaitCommandEditorViewModel wait when tag == "WaitKeyCapture":
                wait.VirtualKey = e.Key == VirtualKey.Escape ? 0 : virtualKey;
                break;

            case PauseCommandEditorViewModel pause:
                pause.VirtualKey = e.Key == VirtualKey.Escape ? 0 : virtualKey;
                break;
        }

        e.Handled = true;
    }
}
