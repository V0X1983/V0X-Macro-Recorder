using System.Windows.Input;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Un type de commande proposé dans la barre d'icônes de gauche et dans le menu Insérer.</summary>
/// <param name="Key">Identifiant stable du type de commande.</param>
/// <param name="Title">Libellé affiché (infobulle et menu).</param>
/// <param name="Glyph">Caractère de la police Segoe Fluent Icons / Segoe MDL2 Assets.</param>
/// <param name="StartsGroup">Vrai si un séparateur précède cette icône dans la barre.</param>
/// <param name="IsAvailable">Faux tant que le type de commande n'est pas implémenté (bouton grisé).</param>
/// <param name="Command">Commande d'insertion, appelée avec <paramref name="Key"/> en paramètre.</param>
public sealed record CommandPaletteItem(
    string Key,
    string Title,
    string Glyph,
    bool StartsGroup = false,
    bool IsAvailable = false,
    ICommand? Command = null);
