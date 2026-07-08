using System.IO;
using Cove.Generated;

namespace Cove.Cli;

internal static class CliReferenceDoc
{
    public static string Generate()
    {
        var all = new System.Collections.Generic.List<(string Command, string? Description, string Source)>();
        foreach (var entry in CoveCommandRegistry.Catalogue)
            all.Add((entry.Command, entry.Description, entry.Source));
        foreach (var entry in Cove.Engine.EngineCommandCatalogue.Entries)
            all.Add((entry.Command, entry.Description, entry.Source));
        all.Sort((a, b) => string.CompareOrdinal(a.Command, b.Command));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Cove CLI Reference");
        sb.AppendLine();
        sb.AppendLine("Generated from the command registry. Do not edit by hand.");
        sb.AppendLine();
        sb.AppendLine("| Command | Source | Description |");
        sb.AppendLine("|---------|--------|-------------|");
        foreach (var entry in all)
        {
            var desc = entry.Description ?? "";
            sb.AppendLine($"| `{entry.Command}` | {entry.Source} | {desc} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Total: {all.Count} commands");
        return sb.ToString();
    }

    public static void WriteTo(string path)
    {
        File.WriteAllText(path, Generate());
    }
}
