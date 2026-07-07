using System.Text.Json;

namespace Cove.Engine.Hooks;

public sealed partial class HookMarker
{
    public const string Prefix = "COVE_HOOK_MARKER=cove-runtime-hook";

    [System.Text.RegularExpressions.GeneratedRegex(@"COVE_HOOK_MARKER=(?:cove|atrium)-runtime-hook", System.Text.RegularExpressions.RegexOptions.None)]
    private static partial System.Text.RegularExpressions.Regex OwnershipRegex();

    public static bool OwnsLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
        return OwnershipRegex().IsMatch(line);
    }

    public static bool OwnsElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return OwnsLine(element.GetString() ?? "");
        return OwnsLine(element.GetRawText());
    }
}

public static class HookConfigMerger
{
    public static Dictionary<string, JsonElement> Merge(
        IReadOnlyDictionary<string, JsonElement> existing,
        string arrayKey,
        string coveEntry)
    {
        var result = new Dictionary<string, JsonElement>(existing);

        if (!result.TryGetValue(arrayKey, out var existingArr) || existingArr.ValueKind != JsonValueKind.Array)
        {
            result[arrayKey] = CreateArray(new[] { coveEntry });
            return result;
        }

        var survivors = new List<JsonElement>();
        foreach (var item in existingArr.EnumerateArray())
        {
            if (!HookMarker.OwnsElement(item))
                survivors.Add(item.Clone());
        }
        result[arrayKey] = CreateArray(survivors, coveEntry);
        return result;
    }

    public static Dictionary<string, JsonElement> Uninstall(
        IReadOnlyDictionary<string, JsonElement> existing,
        string arrayKey)
    {
        var result = new Dictionary<string, JsonElement>(existing);

        if (!result.TryGetValue(arrayKey, out var existingArr) || existingArr.ValueKind != JsonValueKind.Array)
            return result;

        var survivors = new List<JsonElement>();
        foreach (var item in existingArr.EnumerateArray())
        {
            if (!HookMarker.OwnsElement(item))
                survivors.Add(item.Clone());
        }

        if (survivors.Count == 0)
            result.Remove(arrayKey);
        else
            result[arrayKey] = CreateArray(survivors, null);

        return result;
    }

    public static void WriteAtomic(string path, JsonElement content)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (dir is not null)
            System.IO.Directory.CreateDirectory(dir);
        var tempPath = path + ".tmp";
        using (var buffer = new System.IO.MemoryStream())
        {
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
            content.WriteTo(writer);
            writer.Flush();
            System.IO.File.WriteAllBytes(tempPath, buffer.ToArray());
        }
        System.IO.File.Move(tempPath, path, overwrite: true);
    }

    private static JsonElement CreateArray(IReadOnlyList<string> values)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var v in values)
                writer.WriteStringValue(v);
            writer.WriteEndArray();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    private static JsonElement CreateArray(IReadOnlyList<JsonElement> elements, string? append)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var el in elements)
                el.WriteTo(writer);
            if (append is not null)
                writer.WriteStringValue(append);
            writer.WriteEndArray();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}
