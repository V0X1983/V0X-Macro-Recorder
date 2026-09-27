using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Ligne de la grille : une commande et ses textes d'affichage.</summary>
public sealed partial class CommandRowViewModel(int index, MacroCommand command) : ObservableObject
{
    /// <summary>Position de la commande dans la macro (0 = première).</summary>
    public int Index { get; } = index;

    public MacroCommand Command { get; } = command;

    public string Title { get; } = CommandDescriber.GetTitle(command);

    public string Details { get; } = CommandDescriber.GetDetails(command);

    public string DelayText { get; } = CommandDescriber.GetDelayText(command);

    /// <summary>Vrai pendant la lecture, pour la ligne dont l'exécution vient de commencer (surlignage).</summary>
    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>Profondeur d'imbrication (Si/Boucle) : 0 = racine. Calculée par <c>MacroDocumentViewModel.Refresh</c>.</summary>
    public int IndentLevel { get; set; }

    /// <summary>Vrai si cette ligne est le début d'un bloc (Si/Boucle), pour afficher le bouton plier/déplier.</summary>
    public bool IsBlockStart { get; set; }

    /// <summary>Replié par l'utilisateur (ne concerne que les lignes où <see cref="IsBlockStart"/> est vrai).</summary>
    [ObservableProperty]
    private bool _isCollapsed;

    /// <summary>Faux si un ancêtre replié contient cette ligne (ligne alors masquée dans la grille).</summary>
    [ObservableProperty]
    private bool _isVisible = true;
}
