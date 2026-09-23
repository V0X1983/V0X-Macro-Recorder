using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests;

public sealed class BlockMapTests
{
    [Fact]
    public void Matches_a_simple_if_endif_pair()
    {
        var commands = new List<MacroCommand> { new IfCommand(), new CommentCommand(), new EndIfCommand() };

        var map = BlockMap.Build(commands);

        Assert.Equal(2, map.MatchingEnd[0]);
        Assert.Equal(0, map.MatchingStart[2]);
        Assert.False(map.ElseIndex.ContainsKey(0));
    }

    [Fact]
    public void Matches_if_else_endif_and_links_else_to_endif()
    {
        var commands = new List<MacroCommand> { new IfCommand(), new ElseCommand(), new EndIfCommand() };

        var map = BlockMap.Build(commands);

        Assert.Equal(1, map.ElseIndex[0]);
        Assert.Equal(2, map.ElseToEnd[1]);
        Assert.Equal(2, map.MatchingEnd[0]);
    }

    [Fact]
    public void Matches_a_simple_loop_endloop_pair()
    {
        var commands = new List<MacroCommand> { new LoopCommand(), new CommentCommand(), new EndLoopCommand() };

        var map = BlockMap.Build(commands);

        Assert.Equal(2, map.MatchingEnd[0]);
        Assert.Equal(0, map.MatchingStart[2]);
    }

    [Fact]
    public void Matches_nested_blocks_independently()
    {
        // if(0) loop(1) endloop(2) if(3) endif(4) endif(5)
        var commands = new List<MacroCommand>
        {
            new IfCommand(),
            new LoopCommand(),
            new EndLoopCommand(),
            new IfCommand(),
            new EndIfCommand(),
            new EndIfCommand(),
        };

        var map = BlockMap.Build(commands);

        Assert.Equal(2, map.MatchingEnd[1]); // boucle interne
        Assert.Equal(4, map.MatchingEnd[3]); // if interne
        Assert.Equal(5, map.MatchingEnd[0]); // if externe
    }

    [Fact]
    public void Records_labels_case_insensitively()
    {
        var commands = new List<MacroCommand> { new LabelCommand { Name = "Debut" }, new CommentCommand() };

        var map = BlockMap.Build(commands);

        Assert.Equal(0, map.Labels["debut"]);
        Assert.Equal(0, map.Labels["DEBUT"]);
    }

    [Fact]
    public void Duplicate_label_throws()
    {
        var commands = new List<MacroCommand> { new LabelCommand { Name = "x" }, new LabelCommand { Name = "X" } };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Unclosed_if_throws()
    {
        var commands = new List<MacroCommand> { new IfCommand(), new CommentCommand() };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Unclosed_loop_throws()
    {
        var commands = new List<MacroCommand> { new LoopCommand() };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Endif_without_matching_if_throws()
    {
        var commands = new List<MacroCommand> { new EndIfCommand() };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Endloop_without_matching_loop_throws()
    {
        var commands = new List<MacroCommand> { new EndLoopCommand() };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Else_without_matching_if_throws()
    {
        var commands = new List<MacroCommand> { new ElseCommand() };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Interleaved_blocks_throw()
    {
        // if(0) loop(1) endif(2) endloop(3) -- endif ferme un loop encore ouvert : incohérent.
        var commands = new List<MacroCommand>
        {
            new IfCommand(),
            new LoopCommand(),
            new EndIfCommand(),
            new EndLoopCommand(),
        };

        Assert.Throws<MacroFormatException>(() => BlockMap.Build(commands));
    }

    [Fact]
    public void Empty_command_list_builds_an_empty_map()
    {
        var map = BlockMap.Build([]);

        Assert.Empty(map.MatchingEnd);
        Assert.Empty(map.Labels);
    }
}
