namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Joue un fichier son (commande Sons/messages).</summary>
public interface ISoundPlayer
{
    Task PlayAsync(string filePath, bool waitForCompletion, CancellationToken ct);
}
