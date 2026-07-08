using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Engine.Keybindings;

public sealed record KeyBinding(string Chord, string ActionType, string Action, string? Description);

public sealed record TryOverrideResult(bool Success, string? Error, string? Warning);

public sealed class KeybindingEngine
{
    private readonly Dictionary<string, KeyBinding> _defaults = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KeyBinding?> _overrides = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Chord, string Action)> _allRegistrations = new();
    private static readonly HashSet<string> ReservedKeys = new(System.StringComparer.OrdinalIgnoreCase) { "cmd+q", "cmd+tab", "ctrl+q" };

    public void RegisterDefault(string chord, string actionType, string action, string? description = null)
    {
        var normalized = NormalizeChord(chord);
        _defaults[normalized] = new KeyBinding(normalized, actionType, action, description);
        _allRegistrations.Add((normalized, action));
    }

    public void SetOverride(string chord, KeyBinding? binding)
    {
        var normalized = NormalizeChord(chord);
        if (binding is null)
        {
            _overrides[normalized] = null;
            return;
        }
        _overrides[normalized] = new KeyBinding(normalized, binding.ActionType, binding.Action, binding.Description);
        _allRegistrations.Add((normalized, binding.Action));
    }

    public TryOverrideResult TrySetOverride(string chord, KeyBinding binding)
    {
        var normalized = NormalizeChord(chord);
        if (IsReserved(normalized))
            return new TryOverrideResult(false, $"chord '{normalized}' is reserved and cannot be overridden", null);

        string? warning = null;
        if (_defaults.TryGetValue(normalized, out var existing) &&
            !string.Equals(existing.Action, binding.Action, System.StringComparison.OrdinalIgnoreCase))
            warning = $"conflict: '{normalized}' was bound to '{existing.Action}', rebinding to '{binding.Action}'";

        SetOverride(normalized, binding);
        return new TryOverrideResult(true, null, warning);
    }

    public void ClearOverride(string chord)
    {
        var normalized = NormalizeChord(chord);
        _overrides.Remove(normalized);
    }

    public KeyBinding? Resolve(string chord)
    {
        var normalized = NormalizeChord(chord);
        if (_overrides.TryGetValue(normalized, out var overrideBinding))
            return overrideBinding;
        return _defaults.TryGetValue(normalized, out var defaultBinding) ? defaultBinding : null;
    }

    public IReadOnlyList<KeyBinding> GetResolvedBindings()
    {
        var result = new Dictionary<string, KeyBinding>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _defaults)
            result[kv.Key] = kv.Value;
        foreach (var kv in _overrides)
        {
            if (kv.Value is null)
                result.Remove(kv.Key);
            else
                result[kv.Key] = kv.Value;
        }
        return result.Values.ToList();
    }

    public IReadOnlyList<string> GetConflicts()
    {
        var chordToActions = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (chord, action) in _allRegistrations)
        {
            if (!chordToActions.TryGetValue(chord, out var actions))
            {
                actions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                chordToActions[chord] = actions;
            }
            actions.Add(action);
        }
        return chordToActions.Where(kv => kv.Value.Count > 1).Select(kv => kv.Key).ToList();
    }

    public bool IsReserved(string chord)
    {
        var normalized = NormalizeChord(chord);
        return ReservedKeys.Contains(normalized);
    }

    public static string NormalizeChord(string chord)
    {
        var parts = chord.Split('+', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToLowerInvariant())
            .ToList();

        var modifiers = parts.Where(p => p is "cmd" or "ctrl" or "alt" or "shift" or "opt" or "win").ToList();
        var keys = parts.Except(modifiers).ToList();

        var normalizedModifiers = modifiers.Select(m => m switch
        {
            "opt" => "alt",
            "win" => "ctrl",
            _ => m
        }).Distinct().OrderBy(m => m switch
        {
            "cmd" => 0,
            "ctrl" => 1,
            "alt" => 2,
            "shift" => 3,
            _ => 9
        }).ToList();

        var result = string.Join("+", normalizedModifiers);
        if (keys.Count > 0)
        {
            if (result.Length > 0) result += "+";
            result += keys[0];
        }
        return result;
    }

    public void LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var chord = NormalizeChord(prop.Name);
            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                _overrides[chord] = null;
            }
            else
            {
                var actionType = prop.Value.TryGetProperty("actionType", out var at) ? at.GetString() ?? "app-command" : "app-command";
                var action = prop.Value.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() : null;
                _overrides[chord] = new KeyBinding(chord, actionType, action, desc);
                _allRegistrations.Add((chord, action));
            }
        }
    }

    public string ToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var kv in _overrides)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(kv.Key).Append("\":");
            if (kv.Value is null)
                sb.Append("null");
            else
            {
                sb.Append('{');
                sb.Append("\"actionType\":\"").Append(kv.Value.ActionType).Append("\",");
                sb.Append("\"action\":\"").Append(kv.Value.Action).Append("\"");
                if (kv.Value.Description is not null)
                    sb.Append(",\"description\":\"").Append(kv.Value.Description).Append("\"");
                sb.Append('}');
            }
        }
        sb.Append('}');
        return sb.ToString();
    }
}
