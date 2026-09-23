namespace V0XMacroRecorder.Core.Macros;

/// <summary>Fichier de macro illisible, corrompu ou créé par une version plus récente. Le message est destiné à l'utilisateur.</summary>
public sealed class MacroFormatException : Exception
{
    public MacroFormatException(string message)
        : base(message)
    {
    }

    public MacroFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
