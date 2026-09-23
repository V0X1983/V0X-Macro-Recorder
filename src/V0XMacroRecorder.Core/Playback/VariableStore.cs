using System.Globalization;

namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Variables de macro (commande Variable, boucles, jetons <c>{var:nom}</c>). Noms insensibles à la casse ;
/// une variable non définie vaut "" (jamais d'exception à la lecture).
/// </summary>
public sealed class VariableStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string Get(string name) => _values.TryGetValue(name, out var value) ? value : "";

    public void Set(string name, string value) => _values[name] = value;

    public bool TryGetNumber(string name, out double value) =>
        double.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public void Clear() => _values.Clear();
}
