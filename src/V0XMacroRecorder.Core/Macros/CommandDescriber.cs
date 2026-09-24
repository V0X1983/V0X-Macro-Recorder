using System.Globalization;

namespace V0XMacroRecorder.Core.Macros;

/// <summary>Textes français affichés dans la grille de commandes (colonnes Commande, Détails et Délai).</summary>
public static class CommandDescriber
{
    private const int MaxTextLength = 60;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string GetTitle(MacroCommand command) => command switch
    {
        MouseCommand => "Souris",
        KeyboardCommand => "Clavier",
        TextCommand => "Texte",
        WaitCommand => "Attente",
        CommentCommand => "Commentaire",
        ClipboardCommand => "Presse-papiers",
        LaunchCommand => "Programme",
        OpenUrlCommand => "Adresse web",
        WindowCommand => "Fenêtre",
        PixelCommand => "Pixel",
        SoundCommand => "Son",
        MessageCommand => "Message",
        ImageSearchCommand => "Image",
        IfCommand => "Si",
        ElseCommand => "Sinon",
        EndIfCommand => "Fin si",
        LoopCommand => "Boucle",
        EndLoopCommand => "Fin boucle",
        VariableCommand => "Variable",
        LabelCommand => "Étiquette",
        GotoCommand => "Aller à",
        StopCommand => "Arrêter",
        CallCommand => "Appeler une macro",
        PauseCommand => "Pause",
        ScriptCommand => "Script C#",
        SecureInputCommand => "Saisie protégée",
        _ => command.Kind,
    };

    public static string GetDetails(MacroCommand command) => command switch
    {
        MouseCommand m => DescribeMouse(m),
        KeyboardCommand k => DescribeKeyboard(k),
        TextCommand t => DescribeText(t),
        WaitCommand w => DescribeWait(w),
        CommentCommand c => FirstLine(c.Text),
        ClipboardCommand p => DescribeClipboard(p),
        LaunchCommand l => DescribeLaunch(l),
        OpenUrlCommand u => $"Ouvrir « {u.Path} »",
        WindowCommand win => DescribeWindow(win),
        PixelCommand px => DescribePixel(px),
        SoundCommand snd => $"Jouer « {snd.FilePath} »",
        MessageCommand msg => $"« {msg.Title} » : {FirstLine(msg.Text)}",
        ImageSearchCommand img => DescribeImageSearch(img),
        IfCommand ifCmd => DescribeIf(ifCmd),
        LoopCommand loop => DescribeLoop(loop),
        VariableCommand variable => DescribeVariable(variable),
        LabelCommand label => $"« {label.Name} »",
        GotoCommand gotoCmd => $"Aller à « {gotoCmd.TargetLabel} »",
        CallCommand call => $"Appeler « {call.MacroFilePath} »",
        PauseCommand pause => pause.VirtualKey == 0 ? "Attendre n'importe quelle touche" : $"Attendre la touche {VirtualKeyNames.GetName(pause.VirtualKey)}",
        ScriptCommand script => DescribeScript(script),
        SecureInputCommand secure => DescribeSecureInput(secure),
        _ => "",
    };

    /// <summary>Contenu de la colonne Délai : vide pour les commandes qui portent leur propre durée ou n'ont pas d'exécution.</summary>
    public static string GetDelayText(MacroCommand command) =>
        command is WaitCommand or CommentCommand or IfCommand or ElseCommand or EndIfCommand or LoopCommand or EndLoopCommand or LabelCommand
        || command.DelayMs == 0
            ? ""
            : FormatDuration(command.DelayMs);

    public static string FormatDuration(int milliseconds) =>
        milliseconds < 1000
            ? $"{milliseconds} ms"
            : $"{(milliseconds / 1000.0).ToString("0.###", French)} s";

    private static string DescribeMouse(MouseCommand m)
    {
        var button = m.Button switch
        {
            MouseButton.Right => "droit",
            MouseButton.Middle => "du milieu",
            _ => "gauche",
        };

        return m.Action switch
        {
            MouseAction.Move => $"Déplacer vers {Point(m)}",
            MouseAction.Click => $"Clic {button} à {Point(m)}",
            MouseAction.DoubleClick => $"Double-clic {button} à {Point(m)}",
            MouseAction.Down => $"Bouton {button} enfoncé à {Point(m)}",
            MouseAction.Up => $"Bouton {button} relâché à {Point(m)}",
            MouseAction.Wheel => DescribeWheel(m.WheelDelta),
            _ => "",
        };
    }

    private static string DescribeWheel(int notches)
    {
        var direction = notches >= 0 ? "haut" : "bas";
        var count = Math.Abs(notches);
        return $"Molette vers le {direction} ({count} {(count > 1 ? "crans" : "cran")})";
    }

    private static string Point(MouseCommand m) => m.CoordinateMode switch
    {
        CoordinateMode.ActiveWindow => string.IsNullOrEmpty(m.WindowTitle)
            ? $"({m.X}, {m.Y}) dans la fenêtre active"
            : $"({m.X}, {m.Y}) dans « {m.WindowTitle} »",
        CoordinateMode.Relative => $"({Signed(m.X)}, {Signed(m.Y)}) depuis la position actuelle",
        _ => $"({m.X}, {m.Y})",
    };

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    private static string DescribeKeyboard(KeyboardCommand k)
    {
        var key = VirtualKeyNames.Format(k.Modifiers, k.VirtualKey);
        return k.Action switch
        {
            KeyAction.Down => $"Maintenir {key} enfoncée",
            KeyAction.Up => $"Relâcher {key}",
            _ => $"Appuyer sur {key}",
        };
    }

    private static string DescribeText(TextCommand t)
    {
        if (t.Text.Length == 0)
        {
            return "Saisir du texte (vide)";
        }

        var flat = t.Text.Replace("\r\n", "↵").Replace('\n', '↵').Replace('\r', '↵');
        if (flat.Length > MaxTextLength)
        {
            flat = flat[..MaxTextLength] + "…";
        }

        return $"Saisir « {flat} »";
    }

    private static string DescribeWait(WaitCommand w) => w.Mode switch
    {
        WaitMode.WindowAppears => string.IsNullOrEmpty(w.WindowTitle)
            ? "Attendre qu'une fenêtre apparaisse"
            : $"Attendre que « {w.WindowTitle} » apparaisse",
        WaitMode.WindowDisappears => string.IsNullOrEmpty(w.WindowTitle)
            ? "Attendre qu'une fenêtre disparaisse"
            : $"Attendre que « {w.WindowTitle} » disparaisse",
        WaitMode.KeyPress => w.VirtualKey == 0
            ? "Attendre n'importe quelle touche"
            : $"Attendre la touche {VirtualKeyNames.GetName(w.VirtualKey)}",
        WaitMode.PixelMatch => $"Attendre que ({w.PixelX}, {w.PixelY}) devienne {w.PixelColorHex}",
        WaitMode.ImageFound => "Attendre qu'une image apparaisse",
        _ => w.RandomExtraMs > 0
            ? $"Attendre de {FormatDuration(w.DurationMs)} à {FormatDuration(w.DurationMs + w.RandomExtraMs)}"
            : $"Attendre {FormatDuration(w.DurationMs)}",
    };

    private static string DescribeClipboard(ClipboardCommand p) => p.Action switch
    {
        ClipboardAction.Copy => $"Copier « {FirstLine(p.Text)} »",
        ClipboardAction.Paste => "Coller (Ctrl+V)",
        ClipboardAction.ReadToVariable => $"Lire dans {{var:{p.VariableName}}}",
        _ => "",
    };

    private static string DescribeLaunch(LaunchCommand l)
    {
        var suffix = l.WaitForExit ? " (attendre la fin)" : "";
        return l.Mode == LaunchMode.ShellCommand
            ? $"Commande shell « {l.Path} »{suffix}"
            : $"Lancer « {l.Path} »{suffix}";
    }

    private static string DescribeWindow(WindowCommand w)
    {
        var name = string.IsNullOrEmpty(w.WindowTitle) ? "la fenêtre" : $"« {w.WindowTitle} »";
        return w.Action switch
        {
            WindowAction.Activate => $"Activer {name}",
            WindowAction.Minimize => $"Réduire {name}",
            WindowAction.Maximize => $"Agrandir {name}",
            WindowAction.Restore => $"Restaurer {name}",
            WindowAction.Close => $"Fermer {name}",
            WindowAction.MoveResize => $"Déplacer {name} à ({w.X}, {w.Y}), {w.Width}×{w.Height}",
            _ => "",
        };
    }

    private static string DescribePixel(PixelCommand p) => p.Mode switch
    {
        PixelActionMode.Wait => $"Attendre que ({p.X}, {p.Y}) devienne {p.ExpectedColorHex}",
        _ => $"Tester si ({p.X}, {p.Y}) vaut {p.ExpectedColorHex}",
    };

    private static string DescribeImageSearch(ImageSearchCommand img)
    {
        var suffix = img.ClickIfFound ? (img.DoubleClick ? " puis double-cliquer" : " puis cliquer") : "";
        return $"Chercher l'image ({img.TemplateWidth}×{img.TemplateHeight}, tolérance {img.TolerancePercent}%){suffix}";
    }

    private static string DescribeIf(IfCommand ifCmd)
    {
        var condition = DescribeCondition(ifCmd.Condition);
        return ifCmd.Negate ? $"Si NON {condition}" : $"Si {condition}";
    }

    private static string DescribeCondition(ConditionSpec spec) => spec.Kind switch
    {
        ConditionKind.WindowExists => string.IsNullOrEmpty(spec.WindowTitle) ? "une fenêtre existe" : $"« {spec.WindowTitle} » existe",
        ConditionKind.PixelMatches => $"({spec.PixelX}, {spec.PixelY}) vaut {spec.PixelColorHex}",
        ConditionKind.ImageFound => "l'image est présente",
        ConditionKind.VariableCompare => $"{{var:{spec.VariableName}}} {ComparisonSymbol(spec.ComparisonOperator)} « {spec.ComparisonValue} »",
        ConditionKind.FileExists => $"le fichier « {spec.FilePath} » existe",
        _ => "",
    };

    private static string ComparisonSymbol(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equals => "=",
        ComparisonOperator.NotEquals => "≠",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.Contains => "contient",
        _ => "",
    };

    private static string DescribeLoop(LoopCommand loop) => loop.Mode switch
    {
        LoopMode.RepeatCount => $"Répéter {loop.RepeatCount} fois",
        LoopMode.While => $"Tant que {DescribeCondition(loop.WhileCondition ?? new ConditionSpec())}",
        LoopMode.ForEachLine => $"Pour chaque ligne de « {loop.FilePath} »",
        _ => "",
    };

    private static string DescribeVariable(VariableCommand v) => v.Mode switch
    {
        VariableMode.Set => $"Définir {{var:{v.Name}}} = « {v.Value} »",
        VariableMode.Increment => $"Incrémenter {{var:{v.Name}}} de {(v.Value.Length == 0 ? "1" : v.Value)}",
        VariableMode.Calculate => $"Calculer {{var:{v.Name}}} = {v.Value}",
        _ => "",
    };

    private static string DescribeSecureInput(SecureInputCommand secure) => secure.PromptAtPlayback
        ? $"Demander « {secure.PromptLabel} » à la lecture"
        : "Secret chiffré enregistré (ne jamais afficher)";

    private static string DescribeScript(ScriptCommand script) =>
        script.Code.Length == 0 ? "Script vide" : FirstLine(script.Code.TrimStart());

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return end < 0 ? text : text[..end] + "…";
    }
}
