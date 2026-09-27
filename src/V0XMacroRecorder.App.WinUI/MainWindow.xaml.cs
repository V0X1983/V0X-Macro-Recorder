using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App;

/// <summary>
/// Coquille de la fenêtre principale : ne porte que ce qui est propre à la fenêtre elle-même (fermeture réelle vs
/// réduction dans la zone de notification, minimiser/restaurer). Le contenu (menu, barre d'outils, grille de
/// commandes…) vit dans <see cref="MainPage"/> — voir le commentaire du gabarit dans <c>MainWindow.xaml</c>.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    /// <summary>Vrai pendant <see cref="ExitForReal"/> : lève l'exception « toujours réduire dans la zone de notification » le temps de cette fermeture précise.</summary>
    private bool _exiting;
    /// <summary>
    /// Vrai une fois <see cref="OnAppWindowClosing"/> passé par <see cref="HandleCloseRequestAsync"/> jusqu'au bout :
    /// permet au second <see cref="Close"/> (déclenché depuis cette même méthode) de fermer réellement la fenêtre au
    /// lieu de rappeler <see cref="HandleCloseRequestAsync"/> indéfiniment. Nécessaire car <c>AppWindow.Closing</c>
    /// (contrairement à <c>Window.Closing</c> de WPF) ne peut pas être annulé de façon asynchrone : on annule
    /// toujours la fermeture immédiatement, on pose les questions (enregistrer ?) en tâche de fond, puis on referme
    /// « pour de vrai » seulement si l'utilisateur ne l'a pas annulé.
    /// </summary>
    private bool _forceClose;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Closing += OnAppWindowClosing;

        viewModel.ExitRequested += (_, _) => ExitForReal();
        viewModel.Document.MinimizeRequested += (_, _) => (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
        viewModel.Document.RestoreRequested += (_, _) => (AppWindow.Presenter as OverlappedPresenter)?.Restore();

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage), viewModel);
    }

    /// <summary>
    /// Quitte réellement l'application (Fichier > Quitter, menu de la zone de notification), même si « Réduire dans
    /// la zone de notification en fermant » est actif — ce réglage ne s'applique qu'au bouton X.
    /// </summary>
    public void ExitForReal()
    {
        _exiting = true;
        _ = HandleCloseRequestAsync();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose)
        {
            return; // Fermeture déjà confirmée par HandleCloseRequestAsync ci-dessous : laisser faire.
        }

        // AppWindow.Closing n'offre aucun mécanisme de report (pas de GetDeferral, contrairement à WPF) : on annule
        // toujours ici, puis on referme "pour de vrai" nous-mêmes une fois les questions async répondues.
        args.Cancel = true;
        _ = HandleCloseRequestAsync();
    }

    private async Task HandleCloseRequestAsync()
    {
        if (!_exiting && _viewModel.MinimizeToTrayOnClose)
        {
            AppWindow.Hide();
            return;
        }

        if (_viewModel.Document.IsPlaying)
        {
            _viewModel.Document.TogglePlaybackCommand.Execute(null); // Arrête proprement (relâche les touches/boutons tenus).
        }

        if (_viewModel.Document.IsRecording)
        {
            _viewModel.Document.ToggleRecordingCommand.Execute(null);
        }

        if (!await _viewModel.Document.ConfirmDiscardChangesAsync())
        {
            _exiting = false; // L'utilisateur a annulé Fichier > Quitter : ne pas rester bloqué en "mode sortie".
            return;
        }

        _forceClose = true;
        Close();
    }
}
