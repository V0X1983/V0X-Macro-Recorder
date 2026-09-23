using V0XMacroRecorder.Services;

namespace V0XMacroRecorder.Tests;

/// <summary>
/// DPAPI (contrairement à SendInput) ne dépend d'aucune fenêtre/pilote d'entrée : ce test exercice le vrai
/// <see cref="Win32DataProtector"/>, pas un faux.
/// </summary>
public sealed class Win32DataProtectorTests
{
    [Fact]
    public void Round_trips_for_the_current_user()
    {
        var protector = new Win32DataProtector();

        var protectedText = protector.Protect("hunter2");

        Assert.NotEqual("hunter2", protectedText);
        Assert.Equal("hunter2", protector.Unprotect(protectedText));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var protector = new Win32DataProtector();
        var protectedText = protector.Protect("hunter2");
        var bytes = Convert.FromBase64String(protectedText);
        bytes[^1] ^= 0xFF; // Corrompt le dernier octet.
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(tampered));
    }
}
