using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Boîte de saisie masquée générique : commande « Saisie protégée » en mode « demander à la lecture », et mot de
/// passe de protection d'un fichier macro. Remplace <c>SecureInputPromptWindow</c> (WPF, fenêtre + <c>ShowDialog</c>)
/// par un <see cref="ContentDialog"/> WinUI 3 — plus adapté qu'une fenêtre séparée pour un prompt modal simple, et
/// intrinsèquement lié à la racine visuelle (<see cref="XamlRoot"/>) d'une fenêtre existante plutôt qu'une fenêtre
/// indépendante. Toujours affiché sur le thread UI : <see cref="Core.Playback.MacroPlayer"/> (comme
/// <c>Win32MessageBoxService</c>) tourne en tâche de fond et peut appeler ceci depuis un autre thread.
/// </summary>
public sealed class SecureInputPrompter(IAppWindowProvider appWindow) : ISecureInputPrompter
{
    public Task<string?> PromptForSecretAsync(string title, string message)
    {
        if (appWindow.DispatcherQueue.HasThreadAccess)
        {
            return ShowAsync(title, message);
        }

        var tcs = new TaskCompletionSource<string?>();
        appWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                tcs.SetResult(await ShowAsync(title, message));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private async Task<string?> ShowAsync(string title, string message)
    {
        var secretBox = new PasswordBox { FontFamily = new FontFamily("Consolas") };
        var panel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) },
                secretBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = appWindow.MainWindow.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? secretBox.Password : null;
    }
}
