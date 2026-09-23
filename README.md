# V0X Macro Recorder

Enregistreur et lecteur de macros (souris, clavier, fenêtres, images) pour Windows 11, en **C# / .NET 8 / WPF**. Même stack, même style et même chaîne de distribution que V0X Cleaner.

> En cours de développement : voir [PROMPT.md](PROMPT.md) pour la feuille de route étape par étape et l'état d'avancement.

## Développement

```powershell
dotnet build V0XMacroRecorder.sln
dotnet test V0XMacroRecorder.sln --no-build
dotnet run --project src/V0XMacroRecorder.App
```

Structure : `src/V0XMacroRecorder.Core` (modèles, moteur), `src/V0XMacroRecorder.Services` (Win32, stockage), `src/V0XMacroRecorder.App` (WPF), `tests/V0XMacroRecorder.Tests` (xUnit), `installer/` (Inno Setup, étape 9).

Données utilisateur : `%AppData%\V0XMacroRecorder` (config.json, logs).
