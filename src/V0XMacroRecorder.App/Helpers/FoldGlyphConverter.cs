using System.Globalization;
using System.Windows.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Réplié (vrai) → ▸ (déplier), déplié (faux) → ▾ (replier) : bouton plier/déplier d'un bloc Si/Boucle.</summary>
public sealed class FoldGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "▸" : "▾";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
