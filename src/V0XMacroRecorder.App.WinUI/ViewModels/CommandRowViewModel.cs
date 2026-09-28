using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Ligne de la grille : une commande et ses textes d'affichage.</summary>
public sealed partial class CommandRowViewModel(int index, MacroCommand command, IReadOnlyDictionary<string, string>? kindColors = null) : ObservableObject
{
    /// <summary>Position de la commande dans la macro (0 = première).</summary>
    public int Index { get; } = index;

    public MacroCommand Command { get; } = command;

    public string Title { get; } = CommandDescriber.GetTitle(command);

    public string Details { get; } = CommandDescriber.GetDetails(command);

    public string DelayText { get; } = CommandDescriber.GetDelayText(command);

    /// <summary>Barre verticale à gauche de la ligne (couleur par type de commande, personnalisable dans Paramètres).</summary>
    [ObservableProperty]
    private SolidColorBrush _kindColorBrush = ParseColor(CommandKindPalette.Resolve(command.Kind, kindColors));

    /// <summary>Rafraîchit la couleur sans reconstruire la ligne (appelé quand l'utilisateur change une couleur dans Paramètres).</summary>
    public void UpdateKindColor(IReadOnlyDictionary<string, string>? kindColors) =>
        KindColorBrush = ParseColor(CommandKindPalette.Resolve(Command.Kind, kindColors));

    private static SolidColorBrush ParseColor(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }

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
