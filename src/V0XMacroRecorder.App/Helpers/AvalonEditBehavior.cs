using System.Windows;
using ICSharpCode.AvalonEdit;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>
/// AvalonEdit expose son texte via <see cref="TextEditor.Document"/>, pas une propriété de dépendance liable en
/// écriture : cette propriété attachée fait le pont avec une liaison MVVM classique (deux sens) sur le Text du ViewModel.
/// </summary>
public static class AvalonEditBehavior
{
    public static readonly DependencyProperty BoundTextProperty = DependencyProperty.RegisterAttached(
        "BoundText", typeof(string), typeof(AvalonEditBehavior), new PropertyMetadata(default(string), OnBoundTextChanged));

    public static string GetBoundText(DependencyObject element) => (string)element.GetValue(BoundTextProperty);

    public static void SetBoundText(DependencyObject element, string value) => element.SetValue(BoundTextProperty, value);

    private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        var newText = (string?)e.NewValue ?? "";
        if (editor.Document.Text != newText)
        {
            editor.Document.Text = newText;
        }

        if (e.OldValue is null)
        {
            // 1re liaison seulement : jamais rebrancher l'événement à chaque mise à jour venant du ViewModel.
            editor.TextChanged += (sender, _) => SetBoundText((TextEditor)sender!, ((TextEditor)sender!).Text);
        }
    }
}
