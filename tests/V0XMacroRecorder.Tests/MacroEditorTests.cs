using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests;

public sealed class MacroEditorTests
{
    private static CommentCommand C(string text) => new() { Text = text };

    private static string Texts(MacroEditor editor) =>
        string.Join(",", editor.Commands.Cast<CommentCommand>().Select(c => c.Text));

    private static MacroEditor EditorWith(params string[] texts)
    {
        var editor = new MacroEditor();
        editor.Load(new Macro { Commands = texts.Select(t => (MacroCommand)C(t)).ToList() });
        return editor;
    }

    [Fact]
    public void Insert_places_commands_at_the_index_and_clamps_it()
    {
        var editor = EditorWith("a", "b");

        Assert.Equal(1, editor.Insert(1, [C("x"), C("y")]));
        Assert.Equal("a,x,y,b", Texts(editor));

        Assert.Equal(4, editor.Insert(99, [C("z")]));
        Assert.Equal("a,x,y,b,z", Texts(editor));
    }

    [Fact]
    public void Delete_removes_the_given_indices_and_ignores_invalid_ones()
    {
        var editor = EditorWith("a", "b", "c", "d");

        editor.Delete([3, 1, 1, 42, -1]);

        Assert.Equal("a,c", Texts(editor));
    }

    [Fact]
    public void Delete_without_valid_index_does_not_touch_history()
    {
        var editor = EditorWith("a");

        editor.Delete([5]);

        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Replace_swaps_the_command()
    {
        var editor = EditorWith("a", "b");

        editor.Replace(1, C("B"));

        Assert.Equal("a,B", Texts(editor));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Replace(2, C("?")));
    }

    [Theory]
    [InlineData(new[] { 0 }, 3, "b,c,a,d", new[] { 2 })]
    [InlineData(new[] { 3 }, 0, "d,a,b,c", new[] { 0 })]
    [InlineData(new[] { 0, 2 }, 4, "b,d,a,c", new[] { 2, 3 })]
    [InlineData(new[] { 1, 2 }, 0, "b,c,a,d", new[] { 0, 1 })]
    [InlineData(new[] { 1 }, 4, "a,c,d,b", new[] { 3 })]
    public void Move_reorders_and_returns_new_indices(int[] indices, int insertBefore, string expected, int[] newIndices)
    {
        var editor = EditorWith("a", "b", "c", "d");

        var result = editor.Move(indices, insertBefore);

        Assert.Equal(expected, Texts(editor));
        Assert.Equal(newIndices, result);
    }

    [Theory]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 1 }, 2)]
    [InlineData(new[] { 1, 2 }, 1)]
    [InlineData(new[] { 1, 2 }, 3)]
    public void Move_to_the_same_place_is_a_no_op(int[] indices, int insertBefore)
    {
        var editor = EditorWith("a", "b", "c", "d");

        var result = editor.Move(indices, insertBefore);

        Assert.Empty(result);
        Assert.Equal("a,b,c,d", Texts(editor));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void Undo_and_redo_restore_previous_states()
    {
        var editor = EditorWith("a");
        editor.Insert(1, [C("b")]);
        editor.Insert(2, [C("c")]);

        Assert.True(editor.Undo());
        Assert.Equal("a,b", Texts(editor));
        Assert.True(editor.Undo());
        Assert.Equal("a", Texts(editor));
        Assert.False(editor.Undo());

        Assert.True(editor.Redo());
        Assert.Equal("a,b", Texts(editor));
        Assert.True(editor.Redo());
        Assert.Equal("a,b,c", Texts(editor));
        Assert.False(editor.Redo());
    }

    [Fact]
    public void Undo_is_not_affected_by_later_edits_of_the_same_command_object()
    {
        var editor = EditorWith("a");
        var shared = (CommentCommand)editor.Commands[0];
        editor.Insert(1, [C("b")]);

        shared.Text = "modifié hors éditeur";
        editor.Undo();

        Assert.Equal("a", ((CommentCommand)editor.Commands[0]).Text);
    }

    [Fact]
    public void A_new_edit_after_undo_discards_the_redo_branch()
    {
        var editor = EditorWith("a");
        editor.Insert(1, [C("b")]);
        editor.Undo();

        editor.Insert(1, [C("z")]);

        Assert.False(editor.CanRedo);
        Assert.Equal("a,z", Texts(editor));
    }

    [Fact]
    public void Changed_is_raised_for_edits_undo_redo_and_load()
    {
        var editor = new MacroEditor();
        var count = 0;
        editor.Changed += (_, _) => count++;

        editor.Load(new Macro());
        editor.Insert(0, [C("a")]);
        editor.Undo();
        editor.Redo();

        Assert.Equal(4, count);
    }

    [Fact]
    public void IsDirty_tracks_edits_and_returns_to_clean_after_undoing_them()
    {
        var editor = EditorWith("a");
        Assert.False(editor.IsDirty);

        editor.Insert(1, [C("b")]);
        Assert.True(editor.IsDirty);

        editor.Undo();
        Assert.False(editor.IsDirty);

        editor.Redo();
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void MarkClean_resets_the_dirty_flag_until_the_next_edit()
    {
        var editor = EditorWith("a");
        editor.Insert(1, [C("b")]);

        editor.MarkClean();
        Assert.False(editor.IsDirty);

        editor.Insert(2, [C("c")]);
        Assert.True(editor.IsDirty);

        editor.Undo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Saved_state_becomes_unreachable_when_a_new_edit_replaces_the_redo_branch()
    {
        var editor = EditorWith("a");
        editor.Insert(1, [C("b")]);
        editor.Insert(2, [C("c")]);
        editor.MarkClean();            // état enregistré : a,b,c
        editor.Undo();
        editor.Undo();                 // retour à : a

        editor.Insert(1, [C("x")]);    // nouvelle branche : a,x  (a,b,c est perdu)
        editor.Insert(2, [C("y")]);    // a,x,y : même profondeur d'historique que l'état enregistré

        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void Load_resets_history_and_dirty_state()
    {
        var editor = EditorWith("a");
        editor.Insert(1, [C("b")]);

        editor.Load(new Macro());

        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.False(editor.IsDirty);
        Assert.Empty(editor.Commands);
    }

    // ------------------------------------------------------------------------------------------- Lot d'enregistrement

    [Fact]
    public void Recording_batch_appends_live_without_reconstructing_and_is_undoable_as_one_step()
    {
        var editor = EditorWith("a");
        var appended = new List<string>();
        editor.CommandAppended += (_, c) => appended.Add(((CommentCommand)c).Text);
        var changedCount = 0;
        editor.Changed += (_, _) => changedCount++;

        editor.BeginRecordingBatch();
        editor.AppendRecorded(C("b"));
        editor.AppendRecorded(C("c"));
        editor.EndRecordingBatch();

        Assert.Equal(["b", "c"], appended);
        Assert.Equal(1, changedCount); // une seule reconstruction, à la fin du lot.
        Assert.Equal("a,b,c", Texts(editor));

        Assert.True(editor.Undo());
        Assert.Equal("a", Texts(editor)); // tout le lot annulé en un seul Ctrl+Z.
    }

    [Fact]
    public void Recording_batch_is_dirty_only_once_a_command_was_actually_appended()
    {
        var editor = EditorWith("a");

        editor.BeginRecordingBatch();
        editor.EndRecordingBatch(); // décompte annulé avant la première commande : rien d'ajouté.

        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void AppendRecorded_outside_a_batch_throws()
    {
        var editor = EditorWith("a");

        Assert.Throws<InvalidOperationException>(() => editor.AppendRecorded(C("b")));
    }

    [Fact]
    public void EndRecordingBatch_without_begin_is_a_no_op()
    {
        var editor = EditorWith("a");

        editor.EndRecordingBatch();

        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
    }
}
