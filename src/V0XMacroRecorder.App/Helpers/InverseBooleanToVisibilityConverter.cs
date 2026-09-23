using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Vrai → Collapsed, faux → Visible (ex : message d'erreur affiché quand un formulaire est invalide).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
