using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App.Views;

public partial class MacroHotkeysWindow : Window
{
    private readonly MacroHotkeysViewModel _viewModel;

    public MacroHotkeysWindow(MacroHotkeysViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save())
        {
            DialogResult = true;
        }
    }

    /// <summary>Champ raccourci : la touche pressée devient le raccourci de la ligne (même patron que <see cref="CommandEditorWindow"/>).</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.OriginalSource is not TextBox { Tag: "MacroHotkeyCapture", DataContext: MacroHotkeyRowViewModel row })
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey is >= 1 and <= 254)
        {
            row.VirtualKey = virtualKey;
        }

        e.Handled = true;
    }
}
