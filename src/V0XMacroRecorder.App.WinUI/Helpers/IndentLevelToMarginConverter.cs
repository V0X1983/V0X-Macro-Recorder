using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Profondeur d'imbrication (Si/Boucle, voir <see cref="ViewModels.CommandRowViewModel.IndentLevel"/>) → marge gauche.</summary>
public sealed class IndentLevelToMarginConverter : IValueConverter
{
    private const double PixelsPerLevel = 16;

    public object Convert(object value, Type targetType, object parameter, string language) =>
        new Thickness((value is int level ? level : 0) * PixelsPerLevel, 0, 0, 0);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
