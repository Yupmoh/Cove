using System.Text.Json;
using Cove.Adapters;

namespace Cove.Engine.Hooks;

public sealed record HookEnvelopeCapability(HookEnvelopeKind Kind, bool IncludeSystemMessage);

public sealed class HookEnvelopeMatrix
{
    public static readonly IReadOnlySet<string> SupportedEvents = new HashSet<string>
    {
        "sessionStartManifest",
        "userPromptSubmit",
        "preToolUse",
        "postToolUse",
    };

    private readonly Dictionary<(string adapter, string @event), HookEnvelopeCapability> _capabilities = new();

    public void Register(string adapter, string eventName, HookEnvelopeKind kind, bool includeSystemMessage = false)
    {
        _capabilities[(adapter, eventName)] = new HookEnvelopeCapability(kind, includeSystemMessage);
    }

    public void RegisterFromManifest(AdapterManifest manifest)
    {
        foreach (var decl in manifest.HookEnvelopes)
            _capabilities[(manifest.Name, decl.Event)] = new HookEnvelopeCapability(decl.Kind, decl.IncludeSystemMessage);
    }

    public HookEnvelopeCapability GetCapability(string adapter, string eventName)
    {
        return _capabilities.TryGetValue((adapter, eventName), out var cap)
            ? cap
            : new HookEnvelopeCapability(HookEnvelopeKind.None, false);
    }

    public bool CanInject(string adapter, string eventName)
    {
        return GetCapability(adapter, eventName).Kind != HookEnvelopeKind.None;
    }
}

public sealed class ContextInjector
{
    private HookEnvelopeMatrix _matrix;

    public ContextInjector(HookEnvelopeMatrix matrix)
    {
        _matrix = matrix;
    }

    public void SwapMatrix(HookEnvelopeMatrix matrix)
    {
        System.Threading.Volatile.Write(ref _matrix, matrix);
    }
    public string Render(string adapter, string eventName, JsonElement context)
    {
        var cap = System.Threading.Volatile.Read(ref _matrix).GetCapability(adapter, eventName);

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

    private static string RenderHookSpecificOutput(JsonElement context, bool includeSystemMessage)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
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
        using (var writer = new Utf8JsonWriter(buffer))
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
