using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>Résultat de la pipette (couleur d'écran) : coordonnées écran et couleur au format "#RRGGBB".</summary>
public sealed record PixelColorPickResult(int X, int Y, string ColorHex);

/// <summary>Résultat de la capture de région (outil de recherche d'image) : modèle PNG encodé en base64 + sa région.</summary>
public sealed record ImageCaptureResult(string TemplatePngBase64, int Width, int Height, RectRegion Region);

/// <summary>Résultat du bouton « Tester » de l'éditeur Script C#.</summary>
public sealed record ScriptTestResult(bool Success, string? ErrorMessage);

public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Dialogues utilisateur, derrière une interface pour garder les ViewModels indépendants de WPF.</summary>
public interface IDialogService
{
    /// <summary>Boîte « Ouvrir » ; null si annulée.</summary>
    string? PickOpenFile();

    /// <summary>Boîte « Enregistrer sous » ; null si annulée.</summary>
    string? PickSaveFile(string suggestedName);

    /// <summary>Boîte « Ouvrir » générique (programme, fichier son…), pas restreinte aux macros ; null si annulée.</summary>
    string? PickAnyFile(string title, string filter);

    /// <summary>Boîte de sélection de dossier (Paramètres > Dossier des macros) ; null si annulée.</summary>
    string? PickFolder(string title);

    UnsavedChangesChoice AskSaveChanges(string macroName);

    void ShowError(string title, string message);

    /// <summary>
    /// Ouvre l'éditeur du type de commande (nouvelle commande si <paramref name="existing"/> est null) ; null si annulé.
    /// <paramref name="knownLabels"/> alimente le choix « Aller à l'étiquette » (ignoré pour les autres types).
    /// </summary>
    MacroCommand? EditCommand(string kind, MacroCommand? existing, IReadOnlyList<string>? knownLabels = null);

    /// <summary>Pipette plein écran : choisir un pixel et sa couleur ; null si annulé (Échap).</summary>
    PixelColorPickResult? PickPixelColor();

    /// <summary>Glisser-sélectionner une région à l'écran, capturée comme modèle PNG ; null si annulé (Échap) ou sélection trop petite.</summary>
    ImageCaptureResult? CaptureImageRegion();

    /// <summary>Bouton « Tester » de l'éditeur Script C# : exécute réellement le code (mêmes services que la lecture, variables jetables).</summary>
    Task<ScriptTestResult> TestScriptAsync(string code, int timeoutSeconds);

    /// <summary>
    /// Avertissement de confiance à l'ouverture d'une macro contenant un Script C# ou une commande Lancer un
    /// programme/commande shell : vrai si l'utilisateur confirme vouloir l'ouvrir quand même.
    /// </summary>
    bool ConfirmOpenUntrustedMacro(string macroName);
}
