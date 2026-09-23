; Script Inno Setup 6 — V0X Macro Recorder. Compilé par installer\build.ps1 (définit AppVersion, PublishDir, OutputDir).
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{38E8D4BB-9FCC-401A-82E1-6AAEE1B25EEE}
AppName=V0X Macro Recorder
AppVersion={#AppVersion}
AppPublisher=V0X
DefaultDirName={autopf}\V0X Macro Recorder
DefaultGroupName=V0X Macro Recorder
UninstallDisplayIcon={app}\V0XMacroRecorder.exe
UninstallDisplayName=V0X Macro Recorder
OutputDir={#OutputDir}
OutputBaseFilename=V0XMacroRecorder-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; L'application elle-même n'a besoin d'aucun droit administrateur (registre HKCU, Planificateur de tâches en
; mode utilisateur) : "lowest" est donc le choix par défaut (installation dans le profil de l'utilisateur,
; {autopf} retombe alors sur %LOCALAPPDATA%\Programs), avec la possibilité de choisir une installation pour
; tous les utilisateurs (élévation UAC) via la page ajoutée par PrivilegesRequiredOverridesAllowed.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
WizardStyle=modern
SetupIconFile=..\src\V0XMacroRecorder.App\Resources\app.ico

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\V0X Macro Recorder"; Filename: "{app}\V0XMacroRecorder.exe"
Name: "{group}\Désinstaller V0X Macro Recorder"; Filename: "{uninstallexe}"
Name: "{autodesktop}\V0X Macro Recorder"; Filename: "{app}\V0XMacroRecorder.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\V0XMacroRecorder.exe"; Description: "Lancer V0X Macro Recorder"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Un identifiant de tâche fixe (RunOnceId) ne convient pas ici : contrairement à V0X Cleaner (une seule tâche
; planifiée nommée), V0X Macro Recorder crée une tâche par macro planifiée ("V0XMacroRecorder Scheduled - <nom
; de fichier>", voir Win32MacroScheduler) — leur nombre et leurs noms exacts ne sont connus qu'à la désinstallation,
; d'où l'énumération par joker via PowerShell plutôt qu'un schtasks /Delete /TN sur un nom unique.
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Get-ScheduledTask -TaskName 'V0XMacroRecorder Scheduled - *' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false"""; Flags: runhidden; RunOnceId: "DelScheduledMacroTasks"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "V0XMacroRecorder"; Flags: uninsdeletevalue dontcreatekey
