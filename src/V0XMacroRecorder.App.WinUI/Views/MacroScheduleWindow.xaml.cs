using Microsoft.UI.Xaml;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App.Views;

public sealed partial class MacroScheduleWindow : Window
{
    public MacroScheduleWindow(MacroScheduleViewModel viewModel)
    {
        InitializeComponent();
        Title = "Planifier une macro";
        RootPanel.DataContext = viewModel;
        WindowSizing.Set(this, 560, 620);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
