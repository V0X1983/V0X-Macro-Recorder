using System.Globalization;
using System.Windows.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Inverse un booléen (ex : un champ désactivé tant qu'une case à cocher n'est PAS cochée).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
