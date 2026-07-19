using System.Collections.Concurrent;
using System.Text.Json;
using Cove.Adapters;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Hooks;

public enum AwarenessLevel { Off, Minimal, Full }

public sealed record ContextChipEntry(string Source, bool Injected);

public sealed record HookEnvelopeCapability(HookEnvelopeKind Kind, bool IncludeSystemMessage);

public sealed class HookEnvelopeMatrix
{
    public static readonly IReadOnlySet<string> SupportedEvents = new HashSet<string>
    {
        "sessionStartManifest", "userPromptSubmit", "preToolUse", "postToolUse",
    };

    private static readonly IReadOnlySet<string> AmbientEvents = new HashSet<string>
    {
        "sessionStartManifest", "preToolUse", "postToolUse",
    };

    public static bool IsAmbient(string eventName) => AmbientEvents.Contains(eventName);

    private readonly Dictionary<(string adapter, string @event), HookEnvelopeCapability> _capabilities = new();

    public void Register(string adapter, string eventName, HookEnvelopeKind kind, bool includeSystemMessage = false)
    {
        _capabilities[(adapter, eventName)] = new HookEnvelopeCapability(kind, includeSystemMessage);
    }

    public void RegisterFromManifest(AdapterManifest manifest)
    {
        foreach (var (eventName, decl) in manifest.HookEnvelopes)
            _capabilities[(manifest.Name, eventName)] = new HookEnvelopeCapability(decl.Kind, decl.IncludeSystemMessage ?? false);
    }

    public HookEnvelopeCapability GetCapability(string adapter, string eventName)
    {
        return _capabilities.TryGetValue((adapter, eventName), out var cap) ? cap : new HookEnvelopeCapability(HookEnvelopeKind.None, false);
    }

    public bool CanInject(string adapter, string eventName)
    {
        return GetCapability(adapter, eventName).Kind != HookEnvelopeKind.None;
    }
}

public sealed class ContextInjector
{
    private HookEnvelopeMatrix _matrix;
    private readonly AwarenessLevel _awareness;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _chips = new();

    public ContextInjector(HookEnvelopeMatrix matrix, AwarenessLevel awareness = AwarenessLevel.Full, ILogger? logger = null)
    {
        _matrix = matrix;
        _awareness = awareness;
        _logger = logger;
    }

    public void SwapMatrix(HookEnvelopeMatrix matrix)
    {
        System.Threading.Volatile.Write(ref _matrix, matrix);
    }

    public string Render(string adapter, string eventName, JsonElement context)
    {
        var matrix = System.Threading.Volatile.Read(ref _matrix);
        var cap = matrix.GetCapability(adapter, eventName);

        bool injected = false;
        string result = "{}";

        try
        {
            if (cap.Kind != HookEnvelopeKind.None && ShouldInject(eventName))
            {
                result = RenderKind(cap, context);
                injected = result != "{}";
            }
        }
        catch (Exception ex)
        {
            _logger?.ContextInjectionFailed(adapter, eventName, ex.Message);
            injected = false;
            result = "{}";
        }

        RecordChip(adapter, eventName, injected);
        return result;
    }

    private bool ShouldInject(string eventName)
    {
        if (_awareness == AwarenessLevel.Full)
            return true;
        if (_awareness == AwarenessLevel.Minimal)
            return !HookEnvelopeMatrix.IsAmbient(eventName) || eventName == "sessionStartManifest";
        return !HookEnvelopeMatrix.IsAmbient(eventName);
    }

    private static string RenderKind(HookEnvelopeCapability cap, JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Undefined)
            return "{}";

        return cap.Kind switch
        {
            HookEnvelopeKind.None => "{}",
            HookEnvelopeKind.Identity => context.ValueKind == JsonValueKind.String ? (context.GetString() ?? "") : context.GetRawText(),
            HookEnvelopeKind.HookSpecificOutput => RenderHookSpecificOutput(context, cap.IncludeSystemMessage),
            HookEnvelopeKind.FlatAdditionalContext => RenderFlatAdditionalContext(context),
            _ => "{}",
        };
    }

    private void RecordChip(string adapter, string eventName, bool injected)
    {
        var perAdapter = _chips.GetOrAdd(adapter, _ => new ConcurrentDictionary<string, bool>());
        perAdapter[eventName] = injected;
    }

    public IReadOnlyList<ContextChipEntry> GetContextChip(string adapter)
    {
        if (!_chips.TryGetValue(adapter, out var perAdapter))
            return Array.Empty<ContextChipEntry>();
        return perAdapter.Select(kv => new ContextChipEntry(kv.Key, kv.Value)).ToList();
    }

    private static string RenderHookSpecificOutput(JsonElement context, bool includeSystemMessage)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("hookSpecificOutput");
            writer.WritePropertyName("additionalContext");
            context.WriteTo(writer);
            if (includeSystemMessage)
                writer.WriteString("systemMessage", "context loaded");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string RenderFlatAdditionalContext(JsonElement context)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("additionalContext");
            context.WriteTo(writer);
            writer.WriteEndObject();
            writer.Flush();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
