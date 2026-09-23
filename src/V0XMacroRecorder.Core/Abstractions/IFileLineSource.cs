namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Lit un fichier texte ligne par ligne (boucle Pour chaque ligne). Séparé de <c>File.ReadLines</c> direct pour rester testable sans disque réel.</summary>
public interface IFileLineSource
{
    IEnumerable<string> ReadLines(string path);
}
