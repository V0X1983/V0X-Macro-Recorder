namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Pile annuler/rétablir par instantanés. <see cref="Position"/> avance à chaque modification et recule à chaque
/// annulation : deux états identiques ont la même position, ce qui permet de savoir si un document est revenu à
/// son état enregistré.
/// </summary>
public sealed class UndoRedoHistory<T>
{
    private readonly List<T> _undo = [];
    private readonly List<T> _redo = [];
    private readonly int _limit;

    public UndoRedoHistory(int limit = 200)
    {
        _limit = Math.Max(1, limit);
    }

    public long Position { get; private set; }

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Mémorise l'état AVANT une modification ; invalide les états « rétablir ».</summary>
    public void Record(T stateBeforeChange)
    {
        _undo.Add(stateBeforeChange);
        if (_undo.Count > _limit)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        Position++;
    }

    /// <summary>Renvoie l'état précédent ; <paramref name="current"/> devient rétablissable.</summary>
    public T Undo(T current)
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("Rien à annuler.");
        }

        var previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current);
        Position--;
        return previous;
    }

    /// <summary>Renvoie l'état annulé ; <paramref name="current"/> redevient annulable.</summary>
    public T Redo(T current)
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("Rien à rétablir.");
        }

        var next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current);
        Position++;
        return next;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Position = 0;
    }
}
