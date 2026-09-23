namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Attente d'une frappe clavier (commande Attente en mode KeyPress, commande Pause).</summary>
public interface IKeyWaiter
{
    /// <summary>
    /// Attend qu'une touche soit pressée (<paramref name="virtualKey"/> précis, ou n'importe laquelle si 0).
    /// Renvoie faux si <paramref name="timeoutMs"/> s'écoule avant (0 ou moins = attente infinie).
    /// </summary>
    Task<bool> WaitForKeyAsync(int virtualKey, int timeoutMs, CancellationToken ct);
}
