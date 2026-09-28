using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Pastille de couleur (section « Couleurs des commandes » de Paramètres) : <see cref="Windows.UI.Color"/> → <see cref="SolidColorBrush"/>.</summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        new SolidColorBrush(value is Windows.UI.Color color ? color : Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
