using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Tests.Fakes;

namespace V0XMacroRecorder.Tests;

public sealed class TokenExpanderTests
{
    [Fact]
    public void Expands_date_using_the_injected_clock()
    {
        var vars = new VariableStore();
        var clock = new DateTime(2026, 3, 5, 14, 30, 0);

        var result = TokenExpander.Expand("Le {date}", vars, clipboard: null, () => clock);

        Assert.Equal("Le 2026-03-05 14:30:00", result);
    }

    [Fact]
    public void Expands_clipboard_from_the_service()
    {
        var vars = new VariableStore();
        var clipboard = new FakeClipboardService { Text = "copié" };

        var result = TokenExpander.Expand("Coller : {clipboard}", vars, clipboard);

        Assert.Equal("Coller : copié", result);
    }

    [Fact]
    public void Null_clipboard_expands_to_empty_string()
    {
        var vars = new VariableStore();

        var result = TokenExpander.Expand("[{clipboard}]", vars, clipboard: null);

        Assert.Equal("[]", result);
    }

    [Fact]
    public void Expands_a_named_variable()
    {
        var vars = new VariableStore();
        vars.Set("nom", "V0X");

        var result = TokenExpander.Expand("Bonjour {var:nom} !", vars, clipboard: null);

        Assert.Equal("Bonjour V0X !", result);
    }

    [Fact]
    public void Undefined_variable_expands_to_empty_string()
    {
        var vars = new VariableStore();

        var result = TokenExpander.Expand("[{var:inconnue}]", vars, clipboard: null);

        Assert.Equal("[]", result);
    }

    [Fact]
    public void Expands_multiple_tokens_in_one_string()
    {
        var vars = new VariableStore();
        vars.Set("x", "1");
        vars.Set("y", "2");

        var result = TokenExpander.Expand("{var:x}-{var:y}", vars, clipboard: null);

        Assert.Equal("1-2", result);
    }

    [Fact]
    public void Unknown_token_is_left_untouched()
    {
        var vars = new VariableStore();

        var result = TokenExpander.Expand("{inconnu}", vars, clipboard: null);

        Assert.Equal("{inconnu}", result);
    }

    [Fact]
    public void Text_without_tokens_is_unchanged()
    {
        var vars = new VariableStore();

        var result = TokenExpander.Expand("texte simple", vars, clipboard: null);

        Assert.Equal("texte simple", result);
    }
}
