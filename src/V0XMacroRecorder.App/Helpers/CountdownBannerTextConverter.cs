using System.Globalization;
using System.Windows.Data;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Texte du bandeau d'enregistrement selon le décompte restant (0 = enregistrement réellement en cours).</summary>
public sealed class CountdownBannerTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int and > 0 ? $"Démarrage dans {value}…" : "Enregistrement en cours — Ctrl+Alt+R ou le bouton ARRÊTER pour arrêter.";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
