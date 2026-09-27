using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using CommunityToolkit.WinUI.UI.Controls;
using V0XMacroRecorder.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace V0XMacroRecorder.App;

/// <summary>
/// Port de <c>MainWindow.xaml.cs</c> (WPF) : menu, barre d'outils, grille de commandes. Voir le commentaire de
/// <c>MainWindow.xaml</c> pour pourquoi ce contenu vit ici plutôt que dans la coquille de fenêtre.
/// </summary>
public sealed partial class MainPage : Page
{
    private const string DragFormat = "V0XMacroRecorder.Rows";

    public MainWindowViewModel ViewModel { get; private set; } = null!;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not MainWindowViewModel viewModel)
        {
            return;
        }

        ViewModel = viewModel;
        DataContext = viewModel; // Alimente les {Binding} classiques (DataTemplates de la grille, converters).

        viewModel.Document.SelectionRequested += OnSelectionRequested;
        viewModel.Document.PropertyChanged += OnDocumentPropertyChanged;
        viewModel.Document.RecentFiles.CollectionChanged += (_, _) => RefreshRecentFilesMenu();

        BuildInsertMenu();
        RefreshRecentFilesMenu();
        UpdatePlaybackStatusText();
    }

    // ------------------------------------------------------------ Menus construits dynamiquement

    /// <summary>« Insérer » : un MenuFlyoutItem par entrée de <see cref="MainWindowViewModel.CommandPalette"/> (statique, jamais reconstruit).</summary>
    private void BuildInsertMenu()
    {
        foreach (var item in ViewModel.CommandPalette)
        {
            var menuItem = new MenuFlyoutItem
            {
                Text = item.Title,
                Command = item.Command,
                CommandParameter = item.Key,
                IsEnabled = item.IsAvailable,
            };
            InsertMenuBarItem.Items.Add(menuItem);
        }
    }

    /// <summary>« Fichiers récents » : reconstruit à chaque changement (ouverture/enregistrement), comme le sous-menu de la zone de notification.</summary>
    private void RefreshRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();
        foreach (var recent in ViewModel.Document.RecentFiles)
        {
            var menuItem = new MenuFlyoutItem { Text = recent.Title };
            menuItem.Click += (_, _) => recent.Command.Execute(recent.Path);
            RecentFilesMenu.Items.Add(menuItem);
        }
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string theme })
        {
            ViewModel.SetThemeCommand.Execute(theme);
        }
    }

    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CommandGrid.SelectedItems.Clear();
        foreach (var row in ViewModel.Document.Rows)
        {
            CommandGrid.SelectedItems.Add(row);
        }
    }

    private void EmergencyStopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Document.IsPlaying)
        {
            ViewModel.Document.TogglePlaybackCommand.Execute(null);
        }
    }

    private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "À propos",
            Content = $"{ViewModel.AppName} {ViewModel.VersionLabel}\n\nEnregistreur et lecteur de macros pour Windows 11.\n© V0X",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private void AddElseMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel.Document.AddElseToSelectedIf();

    // ------------------------------------------------------------ Raccourcis clavier (menu)

    private void OnKeyboardAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        var shift = sender.Modifiers.HasFlag(VirtualKeyModifiers.Shift);
        switch (sender.Key)
        {
            case VirtualKey.N:
                ViewModel.Document.NewCommand.Execute(null);
                break;
            case VirtualKey.O:
                ViewModel.Document.OpenCommand.Execute(null);
                break;
            case VirtualKey.S when shift:
                ViewModel.Document.SaveAsCommand.Execute(null);
                break;
            case VirtualKey.S:
                ViewModel.Document.SaveCommand.Execute(null);
                break;
            case VirtualKey.Z:
                ViewModel.Document.UndoCommand.Execute(null);
                break;
            case VirtualKey.Y:
                ViewModel.Document.RedoCommand.Execute(null);
                break;
            case VirtualKey.R:
                ViewModel.Document.ToggleRecordingCommand.Execute(null);
                break;
        }
    }

    // ------------------------------------------------------------ Bandeau de lecture (pas de DataTrigger en WinUI 3)

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(MacroDocumentViewModel.IsPaused) or nameof(MacroDocumentViewModel.IsPlaying))
        {
            UpdatePlaybackStatusText();
        }
    }

    private void UpdatePlaybackStatusText() => PlaybackStatusText.Text = ViewModel.Document.IsPaused
        ? "En pause — cliquez sur REPRENDRE pour continuer."
        : "Lecture en cours — Ctrl+Alt+S pour l'arrêt d'urgence.";

    // ------------------------------------------------------------ Sélection

    private List<int> SelectedIndices() =>
        CommandGrid.SelectedItems.Cast<CommandRowViewModel>().Select(row => row.Index).Order().ToList();

    private void CommandGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.Document.SetSelection(SelectedIndices());

    private void OnSelectionRequested(object? sender, IReadOnlyList<int> indices)
    {
        var rows = ViewModel.Document.Rows;
        CommandGrid.SelectedItems.Clear();
        foreach (var index in indices.Where(i => i >= 0 && i < rows.Count))
        {
            CommandGrid.SelectedItems.Add(rows[index]);
        }

        if (CommandGrid.SelectedItem is { } first)
        {
            // ViewModel.Document.Rows vient peut-être d'être remplacée en bloc (Refresh()) : le DataGrid tiers
            // (CommunityToolkit) peut alors planter dans ScrollIntoView avec "Invalid row index" s'il n'a pas
            // fini de régénérer ses conteneurs de ligne pour la nouvelle collection — piège WinUI 3 reproductible
            // même en différant l'appel d'un cycle via DispatcherQueue.TryEnqueue. **Une exception levée depuis un
            // callback TryEnqueue ne passe jamais par Application.UnhandledException et tue le processus
            // directement, sans être journalisée** — donc capturée ici explicitement : ScrollIntoView n'est qu'un
            // confort visuel (la sélection elle-même, posée juste au-dessus, reste correcte), son échec ne doit
            // jamais faire planter l'app.
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    CommandGrid.ScrollIntoView(first, null);
                }
                catch (InvalidOperationException ex)
                {
                    Serilog.Log.Warning(ex, "ScrollIntoView a échoué après une sélection (ligne pas encore prête dans le DataGrid).");
                }
            });
        }
    }

    private void CommandGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is not null)
        {
            ViewModel.Document.EditSelectedCommand.Execute(null);
        }
    }

    /// <summary>Bouton plier/déplier d'une ligne de début de bloc (Si/Boucle) : le DataContext du bouton est la ligne elle-même.</summary>
    private void FoldButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommandRowViewModel row })
        {
            ViewModel.Document.ToggleFold(row.Index);
        }
    }

    private void CommandGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        switch (e.Key)
        {
            case VirtualKey.C when ctrl:
                ViewModel.Document.CopyCommand.Execute(null);
                e.Handled = true;
                break;
            case VirtualKey.X when ctrl:
                ViewModel.Document.CutCommand.Execute(null);
                e.Handled = true;
                break;
            case VirtualKey.V when ctrl:
                ViewModel.Document.PasteCommand.Execute(null);
                e.Handled = true;
                break;
            case VirtualKey.Delete:
                ViewModel.Document.DeleteCommand.Execute(null);
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                ViewModel.Document.EditSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ------------------------------------------------------ Apparence de ligne (surlignage lecture, pli)
    // WinUI 3 n'a pas de Style.Triggers (contrairement à WPF) : DataGridRow.Visibility/Background/Foreground sont
    // donc posés ici, à chaque génération de ligne, et tenus à jour via un abonnement PropertyChanged détaché dans
    // UnloadingRow pour ne pas fuir si CommunityToolkit.WinUI.UI.Controls.DataGrid recycle ses conteneurs de ligne.

    private void CommandGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not CommandRowViewModel row)
        {
            return;
        }

        void Handler(object? s, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is null or nameof(CommandRowViewModel.IsCurrent) or nameof(CommandRowViewModel.IsVisible))
            {
                ApplyRowAppearance(e.Row, row);
            }
        }

        row.PropertyChanged += Handler;
        e.Row.Tag = (row, (PropertyChangedEventHandler)Handler);
        ApplyRowAppearance(e.Row, row);

        // Glisser-déposer pour réordonner : voir CommandGrid_DragOver/Drop ci-dessous.
        e.Row.CanDrag = true;
        e.Row.DragStarting += Row_DragStarting;
    }

    private void CommandGrid_UnloadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Tag is ValueTuple<CommandRowViewModel, PropertyChangedEventHandler> tag)
        {
            tag.Item1.PropertyChanged -= tag.Item2;
        }

        e.Row.DragStarting -= Row_DragStarting;
    }

    private static void ApplyRowAppearance(DataGridRow gridRow, CommandRowViewModel row)
    {
        gridRow.Visibility = row.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        if (row.IsCurrent)
        {
            gridRow.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            gridRow.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x0B, 0x14, 0x10));
        }
        else
        {
            gridRow.ClearValue(Control.BackgroundProperty);
            gridRow.ClearValue(Control.ForegroundProperty);
        }
    }

    // ------------------------------------------------------ Glisser-déposer (réordonner les commandes)
    // Non vérifié dans cet environnement (pas d'affichage réel disponible, voir PROMPT.md) : à confirmer sur une
    // vraie machine Windows.

    private void Row_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not DataGridRow { DataContext: CommandRowViewModel row })
        {
            args.Cancel = true;
            return;
        }

        var indices = SelectedIndices();
        if (indices.Count == 0 || !indices.Contains(row.Index))
        {
            indices = [row.Index];
        }

        args.Data.SetData(DragFormat, indices.ToArray());
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void CommandGrid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(DragFormat) ? DataPackageOperation.Move : DataPackageOperation.None;
    }

    private async void CommandGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(DragFormat) || await e.DataView.GetDataAsync(DragFormat) is not int[] indices)
        {
            return;
        }

        var rows = ViewModel.Document.Rows;
        var insertBefore = rows.Count;
        if (FindRow(VisualTreeHelper.FindElementsInHostCoordinates(e.GetPosition(CommandGrid), CommandGrid).FirstOrDefault()) is { DataContext: CommandRowViewModel target } row)
        {
            insertBefore = target.Index + (e.GetPosition(row).Y > row.ActualHeight / 2 ? 1 : 0);
        }

        ViewModel.Document.MoveCommands(indices, insertBefore);
    }

    private static DataGridRow? FindRow(DependencyObject? element)
    {
        while (element is not null and not DataGridRow)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as DataGridRow;
    }
}
