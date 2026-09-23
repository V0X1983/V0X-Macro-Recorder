using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App;

public partial class MainWindow : Window
{
    private const string DragFormat = "V0XMacroRecorder.Rows";

    private readonly MainWindowViewModel _viewModel;
    private Point _dragStart;
    private bool _dragCandidate;
    private bool _exiting;
    private bool _closeConfirmed;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Document.SelectionRequested += OnSelectionRequested;
        viewModel.Document.MinimizeRequested += (_, _) => WindowState = WindowState.Minimized;
        viewModel.Document.RestoreRequested += (_, _) => WindowState = WindowState.Normal;
    }

    /// <summary>
    /// Quitte réellement l'application (Fichier > Quitter, menu de la zone de notification), même si « Réduire
    /// dans la zone de notification en fermant » est actif — ce réglage ne s'applique qu'au bouton X. Ne force pas
    /// la sortie si l'utilisateur annule finalement l'invite « enregistrer les modifications ? » posée par
    /// <see cref="OnClosing"/> : dans ce cas <see cref="_closeConfirmed"/> reste faux et l'application continue.
    /// </summary>
    public void ExitForReal()
    {
        _exiting = true;
        _closeConfirmed = false;
        Close();
        if (_closeConfirmed)
        {
            Application.Current.Shutdown(); // ShutdownMode=OnExplicitShutdown (pour la réduction dans la zone de notification) exige un appel explicite.
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!_exiting && _viewModel.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
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

        if (!_viewModel.Document.ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        _closeConfirmed = true;
    }

    private void RecordOptionsButton_Click(object sender, RoutedEventArgs e) =>
        RecordOptionsPopup.IsOpen = !RecordOptionsPopup.IsOpen;

    private void PlayOptionsButton_Click(object sender, RoutedEventArgs e) =>
        PlayOptionsPopup.IsOpen = !PlayOptionsPopup.IsOpen;

    private void EmergencyStopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Document.IsPlaying)
        {
            _viewModel.Document.TogglePlaybackCommand.Execute(null);
        }
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            this,
            $"{_viewModel.AppName} {_viewModel.VersionLabel}\n\nEnregistreur et lecteur de macros pour Windows 11.\n© V0X",
            "À propos",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    // ------------------------------------------------------------ Sélection

    private List<int> SelectedIndices() =>
        CommandGrid.SelectedItems.Cast<CommandRowViewModel>().Select(row => row.Index).Order().ToList();

    private void CommandGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel.Document.SetSelection(SelectedIndices());

    private void OnSelectionRequested(object? sender, IReadOnlyList<int> indices)
    {
        var rows = _viewModel.Document.Rows;
        CommandGrid.SelectedItems.Clear();
        foreach (var index in indices.Where(i => i >= 0 && i < rows.Count))
        {
            CommandGrid.SelectedItems.Add(rows[index]);
        }

        if (CommandGrid.SelectedItem is { } first)
        {
            CommandGrid.ScrollIntoView(first);
            CommandGrid.Focus();
        }
    }

    private void CommandGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is not null)
        {
            _viewModel.Document.EditSelectedCommand.Execute(null);
        }
    }

    /// <summary>Bouton plier/déplier d'une ligne de début de bloc (Si/Boucle) : le DataContext du bouton est la ligne elle-même.</summary>
    private void FoldButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommandRowViewModel row })
        {
            _viewModel.Document.ToggleFold(row.Index);
        }

        e.Handled = true; // Ne pas laisser le clic sélectionner/désélectionner la ligne en dessous.
    }

    private void AddElseMenuItem_Click(object sender, RoutedEventArgs e) => _viewModel.Document.AddElseToSelectedIf();

    // ------------------------------------------------------ Glisser-déposer

    private void CommandGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragCandidate = FindRow(e.OriginalSource as DependencyObject) is not null;
    }

    private void CommandGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragCandidate = false;
        var indices = SelectedIndices();
        if (indices.Count > 0)
        {
            DragDrop.DoDragDrop(CommandGrid, new DataObject(DragFormat, indices.ToArray()), DragDropEffects.Move);
        }
    }

    private void CommandGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void CommandGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is not int[] indices)
        {
            return;
        }

        var rows = _viewModel.Document.Rows;
        var insertBefore = rows.Count;
        if (FindRow(CommandGrid.InputHitTest(e.GetPosition(CommandGrid)) as DependencyObject) is { Item: CommandRowViewModel target } row)
        {
            insertBefore = target.Index + (e.GetPosition(row).Y > row.ActualHeight / 2 ? 1 : 0);
        }

        _viewModel.Document.MoveCommands(indices, insertBefore);
        e.Handled = true;
    }

    private static DataGridRow? FindRow(DependencyObject? element)
    {
        while (element is not null and not DataGridRow)
        {
            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return element as DataGridRow;
    }
}
