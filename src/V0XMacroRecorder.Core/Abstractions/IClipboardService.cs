namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Accès au presse-papiers (commande Presse-papiers, jeton <c>{clipboard}</c>).</summary>
public interface IClipboardService
{
    string GetText();

    void SetText(string text);
}
