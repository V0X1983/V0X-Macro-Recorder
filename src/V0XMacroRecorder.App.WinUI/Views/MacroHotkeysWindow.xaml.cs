using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App.Views;

public sealed partial class MacroHotkeysWindow : Window
{
    private readonly MacroHotkeysViewModel _viewModel;

    public MacroHotkeysWindow(MacroHotkeysViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Title = "Raccourcis de macros";
        RootPanel.DataContext = viewModel;
        RootPanel.KeyDown += OnRootKeyDown;
        WindowSizing.Set(this, 560, 520);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save())
        {
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Champ raccourci : la touche pressée devient le raccourci de la ligne (même patron que <c>CommandEditorWindow</c>).</summary>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (FocusManager.GetFocusedElement(Content.XamlRoot) is not TextBox { Tag: "MacroHotkeyCapture", DataContext: MacroHotkeyRowViewModel row })
        {
            return;
        }

        var virtualKey = (int)e.Key;
        if (virtualKey is >= 1 and <= 254)
        {
            row.VirtualKey = virtualKey;
        }

        e.Handled = true;
    }
}
