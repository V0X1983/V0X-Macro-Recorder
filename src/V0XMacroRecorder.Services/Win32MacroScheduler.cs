using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Planifie la lecture d'une macro via le Planificateur de tâches Windows (COM, liaison dynamique — même approche
/// que V0X Cleaner pour son nettoyage automatique). Une tâche par macro, nommée
/// <c>"V0XMacroRecorder Scheduled - {nom du fichier}"</c>, jamais deux macros de fichiers différents mais de même
/// nom (limitation documentée, voir PROMPT.md étape 6) : le nom du fichier (sans extension) identifie la tâche.
/// La tâche relance l'exécutable avec <c>--play "chemin" --silent [--repeat N]</c> ; jeton d'ouverture de session
/// interactif (TASK_LOGON_INTERACTIVE_TOKEN) obligatoire — l'injection d'entrée exige une session interactive
/// (voir la limite UIPI/session verrouillée déjà documentée pour la lecture normale).
/// </summary>
public sealed class Win32MacroScheduler : IMacroScheduler
{
    private const string TaskNamePrefix = "V0XMacroRecorder Scheduled - ";
    private const int TaskTriggerDaily = 2;
    private const int TaskTriggerWeekly = 3;
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExec = 0;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskCreateOrUpdate = 6;

    public Task<bool> ConfigureAsync(MacroScheduleSpec spec, CancellationToken cancellationToken = default)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return Task.FromResult(false);
            }

            var taskName = TaskNameFor(spec.MacroFilePath);
            dynamic service = CreateConnectedService();
            dynamic rootFolder = service.GetFolder(@"\");
            TryDeleteTask(rootFolder, taskName);

            dynamic taskDefinition = service.NewTask(0);
            taskDefinition.RegistrationInfo.Description = DescriptionFor(spec.MacroFilePath);

            dynamic triggers = taskDefinition.Triggers;
            switch (spec.Kind)
            {
                case ScheduleTriggerKind.Weekly:
                    dynamic weekly = triggers.Create(TaskTriggerWeekly);
                    weekly.StartBoundary = NextBoundary(spec.TimeOfDay).ToString("yyyy-MM-ddTHH:mm:ss");
                    weekly.DaysOfWeek = 1 << (int)spec.DayOfWeek; // TASK_WEEKLYDOW : Dimanche=1, Lundi=2, ...
                    break;

                case ScheduleTriggerKind.IntervalMinutes:
                    dynamic interval = triggers.Create(TaskTriggerDaily);
                    interval.StartBoundary = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    interval.Repetition.Interval = $"PT{spec.IntervalMinutes}M";
                    break;

                case ScheduleTriggerKind.AtLogon:
                    triggers.Create(TaskTriggerLogon);
                    break;

                default: // Daily
                    dynamic daily = triggers.Create(TaskTriggerDaily);
                    daily.StartBoundary = NextBoundary(spec.TimeOfDay).ToString("yyyy-MM-ddTHH:mm:ss");
                    break;
            }

            dynamic action = taskDefinition.Actions.Create(TaskActionExec);
            action.Path = exe;
            action.Arguments = BuildArguments(spec);

            taskDefinition.Principal.LogonType = TaskLogonInteractiveToken;
            taskDefinition.Settings.Enabled = true;
            taskDefinition.Settings.StartWhenAvailable = true;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;

            rootFolder.RegisterTaskDefinition(taskName, taskDefinition, TaskCreateOrUpdate, null, null, TaskLogonInteractiveToken);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or RuntimeBinderException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> RemoveAsync(string macroFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            dynamic service = CreateConnectedService();
            dynamic rootFolder = service.GetFolder(@"\");
            TryDeleteTask(rootFolder, TaskNameFor(macroFilePath));
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or RuntimeBinderException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<ScheduledMacroInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ScheduledMacroInfo>();
        try
        {
            dynamic service = CreateConnectedService();
            dynamic rootFolder = service.GetFolder(@"\");
            dynamic tasks = rootFolder.GetTasks(0);
            foreach (dynamic task in tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string name;
                try
                {
                    name = (string)task.Name;
                }
                catch (Exception ex) when (ex is COMException or RuntimeBinderException)
                {
                    continue;
                }

                if (!name.StartsWith(TaskNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string description = "";
                bool enabled = true;
                try
                {
                    description = (string)task.Definition.RegistrationInfo.Description ?? "";
                    enabled = (bool)task.Enabled;
                }
                catch (Exception ex) when (ex is COMException or RuntimeBinderException)
                {
                    // Champ illisible : on garde les valeurs par défaut.
                }

                result.Add(new ScheduledMacroInfo(name, MacroPathFromDescription(description), description, enabled));
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or RuntimeBinderException)
        {
            // Planificateur indisponible : liste vide.
        }

        return Task.FromResult<IReadOnlyList<ScheduledMacroInfo>>(result);
    }

    private static string TaskNameFor(string macroFilePath) => TaskNamePrefix + Path.GetFileNameWithoutExtension(macroFilePath);

    private static string DescriptionFor(string macroFilePath) =>
        $"V0X Macro Recorder — lecture planifiée de « {macroFilePath} ».";

    private static string MacroPathFromDescription(string description)
    {
        var start = description.IndexOf('«');
        var end = description.IndexOf('»');
        return start >= 0 && end > start ? description[(start + 1)..end].Trim() : "";
    }

    private static string BuildArguments(MacroScheduleSpec spec)
    {
        var args = $"--play \"{spec.MacroFilePath}\" --silent";
        return spec.RepeatCount > 1 ? $"{args} --repeat {spec.RepeatCount}" : args;
    }

    /// <summary>Prochaine occurrence de <paramref name="timeOfDay"/> : aujourd'hui si pas encore passée, sinon demain.</summary>
    private static DateTime NextBoundary(TimeSpan timeOfDay)
    {
        var candidate = DateTime.Today.Add(timeOfDay);
        return candidate > DateTime.Now ? candidate : candidate.AddDays(1);
    }

    private static void TryDeleteTask(dynamic rootFolder, string taskName)
    {
        try
        {
            rootFolder.DeleteTask(taskName, 0);
        }
        catch (Exception ex) when (ex is COMException or FileNotFoundException or RuntimeBinderException)
        {
            // Aucune tâche existante à supprimer : normal au premier réglage (voir AutoCleanScheduler de V0X Cleaner,
            // même piège de marshaling COM : "fichier introuvable" remonte parfois en FileNotFoundException).
        }
    }

    private static dynamic CreateConnectedService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Le Planificateur de tâches Windows n'est pas disponible.");

        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }
}
