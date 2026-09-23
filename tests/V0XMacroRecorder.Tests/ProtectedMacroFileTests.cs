using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests;

public sealed class ProtectedMacroFileTests
{
    [Fact]
    public void Round_trips_with_the_correct_password()
    {
        const string json = "{\"schemaVersion\":1,\"commands\":[]}";

        var encrypted = ProtectedMacroFile.Encrypt(json, "correct horse battery staple");

        Assert.True(ProtectedMacroFile.IsProtected(encrypted));
        Assert.Equal(json, ProtectedMacroFile.Decrypt(encrypted, "correct horse battery staple"));
    }

    [Fact]
    public void Wrong_password_is_rejected_with_a_friendly_error()
    {
        var encrypted = ProtectedMacroFile.Encrypt("{}", "right-password");

        var ex = Assert.Throws<MacroFormatException>(() => ProtectedMacroFile.Decrypt(encrypted, "wrong-password"));
        Assert.DoesNotContain("Exception", ex.Message);
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected_even_with_the_correct_password()
    {
        var encrypted = ProtectedMacroFile.Encrypt("{\"name\":\"original\"}", "password123");

        // Modifie un caractère au milieu du texte chiffré (après le préfixe) : l'étiquette d'authentification AES-GCM doit détecter l'altération.
        var chars = encrypted.ToCharArray();
        var tamperIndex = encrypted.Length - 5;
        chars[tamperIndex] = chars[tamperIndex] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        Assert.Throws<MacroFormatException>(() => ProtectedMacroFile.Decrypt(tampered, "password123"));
    }

    [Fact]
    public void Two_encryptions_of_the_same_content_produce_different_ciphertext()
    {
        var first = ProtectedMacroFile.Encrypt("{}", "same-password");
        var second = ProtectedMacroFile.Encrypt("{}", "same-password");

        Assert.NotEqual(first, second); // sel/nonce aléatoires à chaque appel.
    }

    [Fact]
    public void Unprotected_content_is_reported_as_such()
    {
        Assert.False(ProtectedMacroFile.IsProtected("{\"schemaVersion\":1}"));
    }

    [Fact]
    public void Decrypting_unprotected_content_throws_a_clear_error()
    {
        Assert.Throws<MacroFormatException>(() => ProtectedMacroFile.Decrypt("{\"schemaVersion\":1}", "anything"));
    }
}
