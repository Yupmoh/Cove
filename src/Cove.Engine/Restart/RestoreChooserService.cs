using System.Text.Json.Serialization;
using Cove.Persistence;

namespace Cove.Engine.Restart;

public sealed record RestoreChoiceItem(string WorkspaceId, string RoomId, string PaneId, string Label, bool WasRunning, bool Hidden);

public sealed record RestoreChooserResult(bool AutoRelaunch, IReadOnlyList<RestoreChoiceItem> Items);

public sealed class RestoreChooserService
{
    private readonly RestorationService _restoration;

    public RestoreChooserService(RestorationService restoration) => _restoration = restoration;

    public RestoreChooserResult Evaluate(IReadOnlyList<RestoreChoiceItem> knownPanes)
    {
        var state = _restoration.LoadState();
        var settings = LoadSettings();
        if (settings.AutoRestoreOnLaunch)
            return new RestoreChooserResult(true, knownPanes);

        var wasClean = state.CleanShutdown;
        if (wasClean)
            return new RestoreChooserResult(true, knownPanes);

        var restorable = knownPanes
            .Where(p => !string.IsNullOrEmpty(p.PaneId))
            .ToList();
        return new RestoreChooserResult(false, restorable);
    }

    public RestoreSettings LoadSettings()
    {
        var state = _restoration.LoadState();
        return new RestoreSettings(state.AutoRestoreOnLaunch);
    }

    public void SaveSettings(RestoreSettings settings)
    {
        var state = _restoration.LoadState();
        var updated = state with { AutoRestoreOnLaunch = settings.AutoRestoreOnLaunch };
        _restoration.SaveState(updated);
    }
}

public sealed record RestoreSettings(bool AutoRestoreOnLaunch);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestoreChoiceItem))]
[JsonSerializable(typeof(RestoreChooserResult))]
[JsonSerializable(typeof(RestoreSettings))]
public sealed partial class RestoreChooserJsonContext : JsonSerializerContext { }
