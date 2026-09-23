# V0X Macro Recorder

Enregistreur et lecteur de macros (souris, clavier, fenêtres, images) pour Windows 11, en **C# / .NET 8 / WPF**. Même stack, même style et même chaîne de distribution que V0X Cleaner.

## Fonctionnalités

- Enregistrement souris/clavier réel (hooks bas niveau), avec options de délais, mode de coordonnées (écran, fenêtre active, relatif au curseur) et champs mot de passe ignorés.
- Lecture fidèle (vitesse 0,1–10×, répétitions, boucle infinie, pas à pas) avec arrêt d'urgence garanti (relâche toute touche/bouton resté enfoncé).
- Commandes avancées : Attente (fenêtre/pixel/image/touche), Programme/URL, Fenêtre, Presse-papiers, Texte avec jetons (`{date}`, `{clipboard}`, `{var:nom}`), Pixel, Recherche d'image, Son, Message, contrôle de flux complet (Si/Sinon, Boucle, Variables, Étiquette/Aller à, Appeler une macro, Pause).
- Script C# (Roslyn) pour les cas non couverts, avec éditeur à coloration syntaxique et bouton « Tester ».
- Raccourcis globaux par macro, icône de zone de notification, planificateur (Planificateur de tâches Windows), ligne de commande `--play "chemin.v0xmacro" [--silent] [--repeat N]`.
- Paramètres complets (thème clair/sombre/système, raccourcis, dossier des macros, démarrage avec Windows), barre d'état, guide de démarrage intégré.
- Sécurité : détection de session verrouillée/UAC avant de lancer une lecture, commande « Saisie protégée » (secret jamais en clair), fichiers macro chiffrés et protégés en intégrité (AES-GCM).
- Mise à jour automatique via GitHub Releases (vérification manuelle depuis Paramètres, téléchargement HTTPS avec vérification SHA-256).

## Installation

- **Installeur** : `V0XMacroRecorder-Setup-<version>.exe` (généré par `installer\build.ps1`). Aucun droit administrateur requis par défaut (installation dans le profil utilisateur) ; une option permet d'installer pour tous les utilisateurs.
- **Portable** : archive `V0XMacroRecorder-<version>-portable.zip`, à décompresser où vous voulez — aucune installation, aucune écriture hors de `%AppData%\V0XMacroRecorder`.

## Raccourcis par défaut

| Action | Raccourci |
| --- | --- |
| Démarrer/arrêter l'enregistrement | `Ctrl+Alt+R` |
| Arrêt d'urgence (pendant la lecture) | `Ctrl+Alt+S` |

Les deux sont reconfigurables dans **Paramètres**. Un raccourci par macro peut aussi être défini dans **Outils > Raccourcis de macros…**

## ⚠️ Avertissement de sécurité

V0X Macro Recorder simule des entrées clavier/souris réelles (Win32 `SendInput`) : toute macro que vous lancez agit exactement comme si vous tapiez/cliquiez vous-même. En conséquence :

- N'ouvrez et ne lancez que des macros dont vous connaissez l'origine — un fichier `.v0xmacro` peut contenir un Script C# ou une commande « Programme » exécutant du code arbitraire (l'application avertit avant l'ouverture d'un tel fichier, mais l'exécution reste possible si vous acceptez).
- Certains jeux et logiciels protégés par un anticheat détectent ou bloquent l'injection d'entrées synthétique ; leur utilisation avec ce logiciel reste sous votre seule responsabilité. V0X Macro Recorder n'inclut et n'ajoutera aucune fonction de contournement d'anticheat ou de détection.
- L'injection d'entrées ne fonctionne pas sur l'écran de verrouillage ni sur le bureau sécurisé UAC (le logiciel le détecte et avertit avant de lancer une lecture) — ce n'est pas un moyen de contourner ces protections Windows.

## Développement

```powershell
dotnet build V0XMacroRecorder.sln
dotnet test V0XMacroRecorder.sln --no-build
dotnet run --project src/V0XMacroRecorder.App
```

Packaging (publish self-contained, archive portable, installeur Inno Setup si disponible) :

```powershell
powershell -ExecutionPolicy Bypass -File installer\build.ps1
```

Structure : `src/V0XMacroRecorder.Core` (modèles, moteur), `src/V0XMacroRecorder.Services` (Win32, stockage, mise à jour), `src/V0XMacroRecorder.App` (WPF), `tests/V0XMacroRecorder.Tests` (xUnit), `installer/` (Inno Setup).

Données utilisateur : `%AppData%\V0XMacroRecorder` (config.json, logs).

> En cours de développement : voir [PROMPT.md](PROMPT.md) pour la feuille de route étape par étape et l'état d'avancement.
