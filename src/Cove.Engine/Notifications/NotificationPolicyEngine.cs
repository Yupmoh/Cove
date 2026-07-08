using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Notifications;

public enum NotificationTier { Ambient, Toast, OsNotification, DockBadge }

public sealed record NotificationTrigger(bool NeedsInput, bool AppFocused, string PaneId, string? BannerId);

public sealed record NotificationEvaluation(
    bool SuppressAmbient,
    bool SuppressToast,
    bool SuppressOsNotification,
    bool SuppressBanner);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(HashSet<string>))]
public sealed partial class NotificationJsonContext : JsonSerializerContext { }

public sealed class NotificationPolicyEngine
{
    private readonly string _tierStatePath;
    private readonly string _dismissedPath;
    private readonly ILogger _logger;
    private readonly Dictionary<NotificationTier, bool> _tierEnabled = new()
    {
        [NotificationTier.Ambient] = true,
        [NotificationTier.Toast] = true,
        [NotificationTier.OsNotification] = true,
        [NotificationTier.DockBadge] = false,
    };
    private readonly HashSet<string> _dismissedBanners = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _needsInputNotifiedPanes = new(System.StringComparer.OrdinalIgnoreCase);

    public NotificationPolicyEngine(string dataDir, ILogger logger)
    {
        _tierStatePath = Path.Combine(dataDir, "notification-tiers.json");
        _dismissedPath = Path.Combine(dataDir, "notification-dismissed.json");
        _logger = logger;
        Load();
    }

    public NotificationEvaluation Evaluate(NotificationTrigger trigger)
    {
        var suppressOs = !trigger.NeedsInput || trigger.AppFocused || !_tierEnabled[NotificationTier.OsNotification];
        if (!suppressOs && _needsInputNotifiedPanes.Contains(trigger.PaneId))
            suppressOs = true;
        if (!suppressOs)
            _needsInputNotifiedPanes.Add(trigger.PaneId);

        var suppressToast = !_tierEnabled[NotificationTier.Toast];
        var suppressBanner = trigger.BannerId is not null && IsDismissed(trigger.BannerId);
        var suppressAmbient = !_tierEnabled[NotificationTier.Ambient];

        return new NotificationEvaluation(suppressAmbient, suppressToast, suppressOs, suppressBanner);
    }

    public void ClearNeedsInput(string paneId)
    {
        _needsInputNotifiedPanes.Remove(paneId);
    }

    public bool IsTierEnabled(NotificationTier tier) => _tierEnabled.GetValueOrDefault(tier, false);

    public void SetTierEnabled(NotificationTier tier, bool enabled)
    {
        _tierEnabled[tier] = enabled;
        SaveTiers();
    }

    public bool IsDismissed(string bannerId) => _dismissedBanners.Contains(bannerId);

    public void DismissBanner(string bannerId)
    {
        if (_dismissedBanners.Add(bannerId))
            SaveDismissed();
    }

    private void Load()
    {
        LoadTiers();
        LoadDismissed();
    }

    private void LoadTiers()
    {
        if (!File.Exists(_tierStatePath)) return;
        try
        {
            var json = File.ReadAllText(_tierStatePath);
            var dict = JsonSerializer.Deserialize(json, NotificationJsonContext.Default.DictionaryStringBoolean);
            if (dict is null) return;
            foreach (var kv in dict)
            {
                if (Enum.TryParse<NotificationTier>(kv.Key, true, out var tier))
                    _tierEnabled[tier] = kv.Value;
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "notification: failed to load tier state from {path}", _tierStatePath);
        }
    }

    private void LoadDismissed()
    {
        if (!File.Exists(_dismissedPath)) return;
        try
        {
            var json = File.ReadAllText(_dismissedPath);
            var set = JsonSerializer.Deserialize(json, NotificationJsonContext.Default.HashSetString);
            if (set is null) return;
            _dismissedBanners.Clear();
            foreach (var id in set)
                _dismissedBanners.Add(id);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "notification: failed to load dismissed banners from {path}", _dismissedPath);
        }
    }

    private void SaveTiers()
    {
        var dict = _tierEnabled.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        var json = JsonSerializer.Serialize(dict, NotificationJsonContext.Default.DictionaryStringBoolean);
        WriteAtomic(_tierStatePath, json);
    }

    private void SaveDismissed()
    {
        var json = JsonSerializer.Serialize(_dismissedBanners, NotificationJsonContext.Default.HashSetString);
        WriteAtomic(_dismissedPath, json);
    }

    private void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, true);
    }
}
