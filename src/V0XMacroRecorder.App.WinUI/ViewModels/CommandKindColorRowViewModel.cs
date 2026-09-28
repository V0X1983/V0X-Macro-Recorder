using CommunityToolkit.Mvvm.ComponentModel;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>
/// Une ligne de la section « Couleurs des commandes » de Paramètres : un type de commande et sa couleur (barre
/// verticale dans la grille, voir <c>CommandRowViewModel.KindColorBrush</c>). <paramref name="onCommit"/> est
/// appelé à chaque changement de couleur (même patron que les autres réglages de <c>MainWindowViewModel</c>,
/// ex. <c>OnMinimizeWindowOnPlayChanged</c> : enregistrement immédiat, pas de bouton Appliquer séparé).
/// </summary>
public sealed partial class CommandKindColorRowViewModel(string key, string title, Windows.UI.Color initialColor, Action<string, Windows.UI.Color> onCommit) : ObservableObject
{
    public string Key { get; } = key;

    public string Title { get; } = title;

    [ObservableProperty]
    private Windows.UI.Color _color = initialColor;

    partial void OnColorChanged(Windows.UI.Color value) => onCommit(Key, value);
}
