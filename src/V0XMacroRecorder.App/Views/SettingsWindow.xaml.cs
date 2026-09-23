using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using V0XMacroRecorder.App.ViewModels;

namespace V0XMacroRecorder.App.Views;

public partial class SettingsWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public SettingsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.LatestReleaseUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_viewModel.LatestReleaseUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // L'utilisateur n'a pas de navigateur associé aux adresses https ; rien à faire de plus ici.
        }
    }

    /// <summary>Champs de capture de raccourci : même patron que <see cref="CommandEditorWindow"/>/<see cref="MacroHotkeysWindow"/>.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.OriginalSource is not TextBox { Tag: string tag } textBox || tag is not ("RecordHotkeyCapture" or "EmergencyStopHotkeyCapture"))
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
            if ((string)textBox.Tag == "RecordHotkeyCapture")
            {
                _viewModel.RecordingOptions.HotKeyVirtualKey = virtualKey;
            }
            else
            {
                _viewModel.EmergencyStopVirtualKey = virtualKey;
            }
        }

        e.Handled = true;
    }
}
