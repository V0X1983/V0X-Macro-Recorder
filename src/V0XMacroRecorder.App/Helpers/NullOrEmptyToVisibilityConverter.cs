using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Visible seulement si la chaîne liée n'est ni nulle ni vide (ex : dernier avertissement de lecture).</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
