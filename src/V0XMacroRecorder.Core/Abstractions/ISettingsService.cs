using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.Core.Abstractions;

public interface ISettingsService
{
    AppSettings Current { get; }

    Task SaveAsync(CancellationToken cancellationToken = default);

    event EventHandler? SettingsChanged;
}
