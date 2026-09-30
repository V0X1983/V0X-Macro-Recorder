using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App.Views;

public sealed partial class SettingsWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public SettingsWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "Paramètres";

        if (Content is FrameworkElement root)
        {
            // DataContext n'alimente ici que les quelques `{Binding}` classiques (Visibility + converter) : le
            // x:Bind au ViewModel n'a pas besoin du DataContext, mais le combo x:Bind+Converter={StaticResource}
            // au niveau racine d'une Window (pas une FrameworkElement en WinUI 3) échoue à la compilation
            // (SetConverterLookupRoot attend une FrameworkElement) — ces 4 bindings restent donc en classique.
            root.DataContext = viewModel;
            root.KeyDown += OnRootKeyDown;
            // Le panneau de navigation (180px) mange de la largeur de contenu par rapport à l'ancienne disposition
            // à une seule colonne : élargi pour que la ligne de cases Ctrl/Alt/Maj/Win (Raccourcis) ne déborde plus
            // (« Win » coupé au bord droit, signalé par l'utilisateur).
            WindowSizing.Set(this, 840, 640);
        }
    }

    private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.LatestReleaseUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(ViewModel.LatestReleaseUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // L'utilisateur n'a pas de navigateur associé aux adresses https ; rien à faire de plus ici.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Bascule le panneau visible selon la catégorie choisie dans le NavigationView (un seul StackPanel
    /// visible à la fois, tous empilés dans la même cellule de Grid).</summary>
    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        foreach (var panel in new[] { ApparencePanel, RaccourcisPanel, LecturePanel, DossierPanel, MiseAJourPanel, CouleursPanel, DemarragePanel })
        {
            panel.Visibility = Visibility.Collapsed;
        }

        var selected = tag switch
        {
            "Apparence" => ApparencePanel,
            "Raccourcis" => RaccourcisPanel,
            "Lecture" => LecturePanel,
            "Dossier" => DossierPanel,
            "MiseAJour" => MiseAJourPanel,
            "Couleurs" => CouleursPanel,
            "Demarrage" => DemarragePanel,
            _ => ApparencePanel,
        };
        selected.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Champs de capture de raccourci : même patron que <c>CommandEditorWindow</c>/<c>MacroHotkeysWindow</c>.
    /// Contrairement à WPF, <see cref="Windows.System.VirtualKey"/> (WinUI 3) correspond déjà directement au code
    /// virtuel Win32 (VK_*) attendu par <see cref="ViewModels.RecordingOptionsViewModel"/> — pas besoin de l'équivalent
    /// de <c>KeyInterop.VirtualKeyFromKey</c> (WPF).
    /// </summary>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (FocusManager.GetFocusedElement(Content.XamlRoot) is not TextBox { Tag: string tag } || tag is not ("RecordHotkeyCapture" or "EmergencyStopHotkeyCapture"))
        {
            return;
        }

        var virtualKey = (int)e.Key;
        if (virtualKey is >= 1 and <= 254)
        {
            if (tag == "RecordHotkeyCapture")
            {
                ViewModel.RecordingOptions.HotKeyVirtualKey = virtualKey;
            }
            else
            {
                ViewModel.EmergencyStopVirtualKey = virtualKey;
            }
        }

        e.Handled = true;
    }
}
