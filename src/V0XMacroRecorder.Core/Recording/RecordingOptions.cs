using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Recording;

public enum MouseSamplingMode
{
    /// <summary>Ne garde que les clics/molette et la position finale de chaque déplacement continu (par défaut).</summary>
    ClicksAndEndpoints,

    /// <summary>Garde chaque échantillon de déplacement (trajectoire fidèle, macro plus volumineuse).</summary>
    FullPath,
}

/// <summary>Réglages de l'enregistrement, choisis dans le menu du bouton ENREGISTRER et persistés dans AppSettings.</summary>
public sealed class RecordingOptions
{
    private int? _delayCapMs;
    private int _startCountdownSeconds;
    private int _hotKeyVirtualKey = 0x52; // 'R'

    public bool CaptureMouse { get; set; } = true;

    public bool CaptureKeyboard { get; set; } = true;

    /// <summary>Si faux, toutes les commandes enregistrées ont un délai de 0 (lecture au plus vite).</summary>
    public bool RecordDelays { get; set; } = true;

    /// <summary>Délai maximal enregistré entre deux commandes (ms) ; null = aucun plafond.</summary>
    public int? DelayCapMs
    {
        get => _delayCapMs;
        set => _delayCapMs = value is null ? null : Math.Max(0, value.Value);
    }

    public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Screen;

    public MouseSamplingMode MouseSampling { get; set; } = MouseSamplingMode.ClicksAndEndpoints;

    /// <summary>Fusionne les frappes de caractères imprimables consécutives en une seule commande Texte.</summary>
    public bool RecordTypedTextAsString { get; set; } = true;

    /// <summary>Secondes avant le vrai début de l'enregistrement (0 = immédiat), affichées en décompte.</summary>
    public int StartCountdownSeconds
    {
        get => _startCountdownSeconds;
        set => _startCountdownSeconds = Math.Clamp(value, 0, 10);
    }

    /// <summary>N'enregistre aucune frappe tant que le champ actif est détecté comme un champ de mot de passe.</summary>
    public bool DontRecordPasswordFields { get; set; } = true;

    public KeyModifiers HotKeyModifiers { get; set; } = KeyModifiers.Ctrl | KeyModifiers.Alt;

    /// <summary>Raccourci global démarrer/arrêter l'enregistrement (VK_*), par défaut R (Ctrl+Alt+R).</summary>
    public int HotKeyVirtualKey
    {
        get => _hotKeyVirtualKey;
        set => _hotKeyVirtualKey = Math.Clamp(value, 1, 254);
    }
}
