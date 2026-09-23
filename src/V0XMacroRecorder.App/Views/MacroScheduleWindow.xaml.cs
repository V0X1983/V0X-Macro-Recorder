using System.Windows;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App.Views;

public partial class MacroScheduleWindow : Window
{
    public MacroScheduleWindow(MacroScheduleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
