using System.Windows.Input;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Entrée du menu Fichier > Fichiers récents.</summary>
public sealed record RecentFileItem(string Title, string Path, ICommand Command);
