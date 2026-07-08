namespace Cove.Engine.Config;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SettingAttribute(string label, string tab, string control = "text", string? description = null) : Attribute
{
    public string Label { get; } = label;
    public string Tab { get; } = tab;
    public string Control { get; } = control;
    public string? Description { get; } = description;
}

public sealed record ConfigSchemaEntry(string Key, string Label, string Tab, string Control, string? Description, string Type);

public static class ConfigSchemaGenerator
{
    public static IReadOnlyList<ConfigSchemaEntry> Generate()
    {
        return Cove.Generated.CoveSettingSchema.Entries
            .Select(e => new ConfigSchemaEntry(e.Key, e.Label, e.Tab, e.Control, e.Description, e.Type))
            .ToList();
    }

    public static string GenerateReferenceDoc()
    {
        var entries = Generate();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Configuration Reference");
        sb.AppendLine();
        sb.AppendLine("All configuration keys for Cove, grouped by settings tab.");
        sb.AppendLine();

        var byTab = entries.GroupBy(e => e.Tab).OrderBy(g => g.Key);
        foreach (var group in byTab)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            sb.AppendLine("| Key | Label | Type | Control | Description |");
            sb.AppendLine("|-----|-------|------|---------|-------------|");
            foreach (var entry in group.OrderBy(e => e.Key))
            {
                sb.AppendLine($"| `{entry.Key}` | {entry.Label} | {entry.Type} | {entry.Control} | {entry.Description ?? ""} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
    public static void WriteReferenceDoc(string path)
    {
        System.IO.File.WriteAllText(path, GenerateReferenceDoc());
    }
}
