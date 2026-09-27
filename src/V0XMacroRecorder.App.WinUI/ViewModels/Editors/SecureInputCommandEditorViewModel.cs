using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

/// <summary>
/// Éditeur de la commande Saisie protégée (étape 8). Un secret déjà enregistré n'est jamais redéchiffré/réaffiché :
/// le champ mot de passe reste vide, et Build() conserve l'ancien secret chiffré tant que l'utilisateur n'en tape
/// pas un nouveau (même logique qu'un champ « changer le mot de passe » classique).
/// </summary>
public sealed partial class SecureInputCommandEditorViewModel : CommandEditorViewModel
{
    private readonly IDataProtector? _dataProtector;
    private readonly string? _existingProtectedValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStoredSecret))]
    private bool _promptAtPlayback;

    [ObservableProperty]
    private string _promptLabel;

    [ObservableProperty]
    private string _characterDelayText;

    /// <summary>Fixé par le code-behind (jamais par liaison XAML : PasswordBox.Password n'est pas bindable).</summary>
    [ObservableProperty]
    private string _newSecret = "";

    public SecureInputCommandEditorViewModel(SecureInputCommand? existing, IDataProtector? dataProtector)
        : base(existing, "Saisie protégée")
    {
        _dataProtector = dataProtector;
        var command = existing ?? new SecureInputCommand();
        _promptAtPlayback = command.PromptAtPlayback;
        _promptLabel = command.PromptLabel;
        _characterDelayText = command.CharacterDelayMs.ToString(CultureInfo.InvariantCulture);
        _existingProtectedValue = command.ProtectedValueBase64;
    }

    /// <summary>Vrai si un secret chiffré existe déjà pour cette commande (mode « enregistrer ») : le champ affiche alors « laisser vide pour conserver ».</summary>
    public bool HasStoredSecret => !PromptAtPlayback && !string.IsNullOrEmpty(_existingProtectedValue);

    public override MacroCommand Build()
    {
        string? protectedValue = null;
        if (!PromptAtPlayback)
        {
            protectedValue = !string.IsNullOrEmpty(NewSecret) && _dataProtector is not null
                ? _dataProtector.Protect(NewSecret)
                : _existingProtectedValue; // Champ laissé vide : conserve l'ancien secret chiffré tel quel.
        }

        return new SecureInputCommand
        {
            PromptAtPlayback = PromptAtPlayback,
            ProtectedValueBase64 = protectedValue,
            PromptLabel = PromptLabel,
            CharacterDelayMs = IntOr(CharacterDelayText, 0),
            DelayMs = Delay,
        };
    }

    protected override bool AreFieldsValid() =>
        TryNonNegative(CharacterDelayText, out _)
        && (PromptAtPlayback ? !string.IsNullOrWhiteSpace(PromptLabel) : (HasStoredSecret || !string.IsNullOrEmpty(NewSecret)));
}
