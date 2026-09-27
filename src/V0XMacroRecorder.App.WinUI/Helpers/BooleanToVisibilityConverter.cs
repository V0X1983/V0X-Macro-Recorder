using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Vrai → Visible, faux → Collapsed. Équivalent du convertisseur intégré WPF <c>BooleanToVisibilityConverter</c> (WinUI 3 n'en fournit pas).</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
