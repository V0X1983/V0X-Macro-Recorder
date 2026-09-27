using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Visible seulement si la chaîne liée n'est ni nulle ni vide (ex : dernier avertissement de lecture).</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
