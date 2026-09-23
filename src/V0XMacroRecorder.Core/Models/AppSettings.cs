using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.Core.Models;

/// <summary>Paramètres persistants de l'application, sérialisés en JSON dans %AppData%\V0XMacroRecorder\config.json.</summary>
public sealed class AppSettings
{
    public const string DarkTheme = "Dark";
    public const string LightTheme = "Light";

    /// <summary>Suit le thème clair/sombre choisi dans les paramètres Windows (étape 7).</summary>
    public const string SystemTheme = "System";

    public const int MaxRecentFiles = 10;

    /// <summary>Dépôt GitHub Releases vérifié pour les mises à jour ; les valeurs vides retombent sur le dépôt officiel.</summary>
    public const string DefaultUpdateOwner = "V0X1983";
    public const string DefaultUpdateRepo = "V0X-Macro-Recorder";

    private int _emergencyStopHotKeyVirtualKey = 0x53; // 'S'

    public string Theme { get; set; } = DarkTheme;

    public string UpdateCheckOwner { get; set; } = DefaultUpdateOwner;
    public string UpdateCheckRepo { get; set; } = DefaultUpdateRepo;

    /// <summary>Macros ouvertes ou enregistrées récemment, la plus récente en premier.</summary>
    public List<string> RecentFiles { get; set; } = [];

    /// <summary>Réglages du bouton ENREGISTRER (menu déroulant), persistés d'une session à l'autre.</summary>
    public RecordingOptions Recording { get; set; } = new();

    /// <summary>Réglages du bouton LECTURE (menu déroulant), persistés d'une session à l'autre.</summary>
    public PlaybackOptions Playback { get; set; } = new();

    /// <summary>Raccourcis globaux « lancer une macro depuis n'importe où » (étape 6).</summary>
    public List<MacroHotkeyBinding> MacroHotkeys { get; set; } = [];

    /// <summary>Lance V0X Macro Recorder à l'ouverture de session (clé HKCU Run, sans droits administrateur).</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Combiné à <see cref="StartWithWindows"/> : la fenêtre principale ne s'affiche pas au démarrage (reste dans la zone de notification).</summary>
    public bool StartMinimizedToTray { get; set; }

    /// <summary>Fermer la fenêtre (bouton X) la réduit dans la zone de notification au lieu de quitter l'application.</summary>
    public bool MinimizeToTrayOnClose { get; set; }

    /// <summary>Raccourci global d'arrêt d'urgence pendant la lecture (étape 7 : devient configurable, fixe à Ctrl+Alt+S depuis l'étape 3).</summary>
    public KeyModifiers EmergencyStopHotKeyModifiers { get; set; } = KeyModifiers.Ctrl | KeyModifiers.Alt;

    public int EmergencyStopHotKeyVirtualKey
    {
        get => _emergencyStopHotKeyVirtualKey;
        set => _emergencyStopHotKeyVirtualKey = Math.Clamp(value, 1, 254);
    }

    /// <summary>Dossier initial proposé par les boîtes Ouvrir/Enregistrer sous ; null = comportement par défaut de Windows (dernier dossier utilisé).</summary>
    public string? DefaultMacrosFolder { get; set; }

    /// <summary>Réduit la fenêtre principale dès le démarrage d'une lecture (bandeau/barre d'état restent visibles dans la barre des tâches).</summary>
    public bool MinimizeWindowOnPlay { get; set; }

    /// <summary>Joue un signal sonore système (pas de fichier configurable, simplification assumée) à la fin d'une lecture.</summary>
    public bool PlayEndOfMacroSound { get; set; }

    /// <summary>Vrai après la fermeture du premier écran d'accueil (étape 7) : ne plus le reproposer automatiquement.</summary>
    public bool HasSeenWelcome { get; set; }

    public void AddRecentFile(string path)
    {
        RemoveRecentFile(path);
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        }
    }

    public void RemoveRecentFile(string path) =>
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
}
