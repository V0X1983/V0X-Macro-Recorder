using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

/// <summary>Transformation réversible triviale (jamais de vraie cryptographie) : suffisant pour vérifier le câblage de <see cref="IDataProtector"/>.</summary>
public sealed class FakeDataProtector : IDataProtector
{
    private const string Prefix = "protected:";

    public bool ThrowOnUnprotect { get; set; }

    public string Protect(string plainText) => Prefix + plainText;

    public string Unprotect(string protectedText)
    {
        if (ThrowOnUnprotect || !protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Simulated unprotect failure.");
        }

        return protectedText[Prefix.Length..];
    }
}
