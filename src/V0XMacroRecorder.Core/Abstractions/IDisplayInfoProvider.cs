namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Dimensions du bureau virtuel (tous les écrans réunis), étape 8 : comparer la valeur enregistrée avec une macro
/// (<see cref="Macros.Macro.RecordedScreenWidth"/>/<see cref="Macros.Macro.RecordedScreenHeight"/>) à la valeur
/// actuelle avant de lire permet d'avertir d'un changement de résolution/DPI ou d'un écran débranché depuis
/// l'enregistrement, plutôt que de cliquer silencieusement au mauvais endroit.
/// </summary>
public interface IDisplayInfoProvider
{
    (int Width, int Height) GetVirtualScreenSize();
}
