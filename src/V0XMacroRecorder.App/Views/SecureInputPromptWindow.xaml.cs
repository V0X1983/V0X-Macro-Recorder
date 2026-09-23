using System.Windows;

namespace V0XMacroRecorder.App.Views;

/// <summary>
/// Boîte de saisie masquée générique (étape 8) : commande « Saisie protégée » en mode « demander à la lecture »,
/// et mot de passe de protection d'un fichier macro. <see cref="System.Windows.Controls.PasswordBox.Password"/>
/// n'est jamais accessible par liaison XAML (protection WPF contre les fuites) : lu directement en code-behind.
/// </summary>
public partial class SecureInputPromptWindow : Window
{
    public string? Secret { get; private set; }

    public SecureInputPromptWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        Loaded += (_, _) => SecretBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Secret = SecretBox.Password;
        DialogResult = true;
    }
}
