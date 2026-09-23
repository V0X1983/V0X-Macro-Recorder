namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Édition d'une macro en mémoire : insertion, suppression, remplacement, déplacement, annuler/rétablir et suivi
/// des modifications non enregistrées. Indépendant de l'interface : toute la logique est testable.
/// </summary>
public sealed class MacroEditor
{
    private readonly UndoRedoHistory<List<MacroCommand>> _history = new();
    private long _cleanPosition;
    private bool _recordingBatchActive;
    private bool _recordingBatchHasSnapshot;

    public MacroEditor()
    {
        Macro = new Macro();
    }

    public Macro Macro { get; private set; }

    public IReadOnlyList<MacroCommand> Commands => Macro.Commands;

    public bool IsDirty => _history.Position != _cleanPosition;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>Levé après chaque modification du contenu (y compris annuler/rétablir et chargement).</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Levé pour chaque commande ajoutée entre <see cref="BeginRecordingBatch"/> et <see cref="EndRecordingBatch"/> :
    /// permet à la vue d'ajouter la ligne en direct sans reconstruire toute la grille à chaque commande (ce que
    /// ferait <see cref="Changed"/>, coûteux pour un enregistrement de centaines de commandes).
    /// </summary>
    public event EventHandler<MacroCommand>? CommandAppended;

    /// <summary>Remplace la macro courante (nouveau document ou fichier ouvert) : historique vidé, état « enregistré ».</summary>
    public void Load(Macro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        Macro = macro;
        _history.Clear();
        _cleanPosition = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>À appeler après un enregistrement réussi sur disque.</summary>
    public void MarkClean() => _cleanPosition = _history.Position;

    /// <summary>Insère les commandes à <paramref name="index"/> (borné à la liste) ; renvoie l'index réel d'insertion.</summary>
    public int Insert(int index, IEnumerable<MacroCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var items = commands.ToList();
        index = Math.Clamp(index, 0, Macro.Commands.Count);
        if (items.Count == 0)
        {
            return index;
        }

        BeginChange();
        Macro.Commands.InsertRange(index, items);
        EndChange();
        return index;
    }

    /// <summary>Supprime les commandes aux indices donnés (indices invalides ignorés).</summary>
    public void Delete(IEnumerable<int> indices)
    {
        var toRemove = ValidIndices(indices);
        if (toRemove.Count == 0)
        {
            return;
        }

        BeginChange();
        for (var i = toRemove.Count - 1; i >= 0; i--)
        {
            Macro.Commands.RemoveAt(toRemove[i]);
        }

        EndChange();
    }

    public void Replace(int index, MacroCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (index < 0 || index >= Macro.Commands.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        BeginChange();
        Macro.Commands[index] = command;
        EndChange();
    }

    /// <summary>
    /// Déplace les commandes aux indices donnés pour qu'elles se retrouvent (dans leur ordre relatif) juste avant
    /// la commande qui occupait <paramref name="insertBefore"/> (Count = à la fin). Renvoie leurs nouveaux indices,
    /// ou une liste vide si l'ordre n'a pas changé.
    /// </summary>
    public IReadOnlyList<int> Move(IEnumerable<int> indices, int insertBefore)
    {
        var moving = ValidIndices(indices);
        if (moving.Count == 0)
        {
            return [];
        }

        insertBefore = Math.Clamp(insertBefore, 0, Macro.Commands.Count);
        var movingSet = moving.ToHashSet();
        var moved = moving.Select(i => Macro.Commands[i]).ToList();
        var remaining = Macro.Commands.Where((_, i) => !movingSet.Contains(i)).ToList();
        var target = insertBefore - moving.Count(i => i < insertBefore);

        var result = new List<MacroCommand>(remaining);
        result.InsertRange(target, moved);
        if (result.SequenceEqual(Macro.Commands))
        {
            return [];
        }

        BeginChange();
        Macro.Commands.Clear();
        Macro.Commands.AddRange(result);
        EndChange();
        return Enumerable.Range(target, moved.Count).ToList();
    }

    /// <summary>
    /// Démarre un lot d'ajouts en direct (enregistrement) : une seule entrée d'annuler/rétablir couvrira tout le
    /// lot, quel que soit le nombre de commandes ajoutées via <see cref="AppendRecorded"/>. L'instantané d'annulation
    /// n'est pris qu'à la 1re commande réellement ajoutée : un lot resté vide (décompte annulé avant le premier
    /// événement capturé) ne crée aucune entrée d'historique et ne rend pas le document modifié.
    /// </summary>
    public void BeginRecordingBatch()
    {
        _recordingBatchActive = true;
        _recordingBatchHasSnapshot = false;
    }

    /// <summary>Ajoute une commande capturée en direct, sans reconstruire la grille (voir <see cref="CommandAppended"/>). À appeler entre <see cref="BeginRecordingBatch"/> et <see cref="EndRecordingBatch"/>.</summary>
    public void AppendRecorded(MacroCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_recordingBatchActive)
        {
            throw new InvalidOperationException($"{nameof(AppendRecorded)} doit être appelé entre {nameof(BeginRecordingBatch)} et {nameof(EndRecordingBatch)}.");
        }

        if (!_recordingBatchHasSnapshot)
        {
            BeginChange();
            _recordingBatchHasSnapshot = true;
        }

        Macro.Commands.Add(command);
        CommandAppended?.Invoke(this, command);
    }

    /// <summary>Termine le lot : historise l'état final et notifie <see cref="Changed"/> une seule fois (aucun effet si le lot est resté vide).</summary>
    public void EndRecordingBatch()
    {
        if (_recordingBatchHasSnapshot)
        {
            EndChange();
        }

        _recordingBatchActive = false;
        _recordingBatchHasSnapshot = false;
    }

    public bool Undo()
    {
        if (!_history.CanUndo)
        {
            return false;
        }

        Restore(_history.Undo(Snapshot()));
        return true;
    }

    public bool Redo()
    {
        if (!_history.CanRedo)
        {
            return false;
        }

        Restore(_history.Redo(Snapshot()));
        return true;
    }

    private void BeginChange()
    {
        // Si l'état enregistré se trouvait dans la branche « rétablir » qu'on va perdre, il n'est plus atteignable.
        if (_history.CanRedo && _cleanPosition > _history.Position)
        {
            _cleanPosition = -1;
        }

        _history.Record(Snapshot());
    }

    private void EndChange() => Changed?.Invoke(this, EventArgs.Empty);

    private void Restore(List<MacroCommand> snapshot)
    {
        Macro.Commands.Clear();
        Macro.Commands.AddRange(snapshot);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<MacroCommand> Snapshot() => Macro.Commands.Select(c => c.Clone()).ToList();

    private List<int> ValidIndices(IEnumerable<int> indices) =>
        indices.Where(i => i >= 0 && i < Macro.Commands.Count).Distinct().Order().ToList();
}
