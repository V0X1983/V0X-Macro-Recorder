using System.IO;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.Views;

/// <summary>Guide de démarrage + génération d'une macro d'exemple (jamais exécutée automatiquement : seulement enregistrée sur demande explicite).</summary>
public sealed partial class HelpWindow : Window
{
    private readonly IDialogService? _dialogs;

    public HelpWindow(IDialogService? dialogs = null)
    {
        InitializeComponent();
        _dialogs = dialogs;
        Title = "Guide de démarrage";
        WindowSizing.Set(this, 560, 620);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void CreateExampleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_dialogs is null)
        {
            return;
        }

        var path = await _dialogs.PickSaveFileAsync("Exemple V0X Macro Recorder");
        if (path is null)
        {
            return;
        }

        string message;
        try
        {
            File.WriteAllText(path, MacroSerializer.Serialize(BuildExampleMacro()), new UTF8Encoding(false));
            message = $"Macro d'exemple créée : « {Path.GetFileName(path)} ».\n\nOuvrez-la avec Fichier > Ouvrir pour l'explorer.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            message = $"Impossible de créer le fichier : {ex.Message}";
        }

        await new ContentDialog
        {
            Title = "Guide de démarrage",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        }.ShowAsync();
    }

    /// <summary>Compteur affiché 3 fois dans une boîte de message : illustre Variable/Boucle/Message/jetons sans toucher au système.</summary>
    private static Macro BuildExampleMacro() => new()
    {
        Name = "Exemple",
        Commands =
        [
            new CommentCommand { Text = "Macro d'exemple — V0X Macro Recorder" },
            new VariableCommand { Mode = VariableMode.Set, Name = "compteur", Value = "0" },
            new LoopCommand { Mode = LoopMode.RepeatCount, RepeatCount = 3 },
            new VariableCommand { Mode = VariableMode.Increment, Name = "compteur", Value = "1", DelayMs = 300 },
            new MessageCommand { Title = "Exemple", Text = "Tour numéro {var:compteur}", MessageKind = MessageBoxKind.Info },
            new EndLoopCommand(),
            new CommentCommand { Text = "Fin de l'exemple : essayez de modifier le nombre de répétitions ci-dessus." },
        ],
    };
}
