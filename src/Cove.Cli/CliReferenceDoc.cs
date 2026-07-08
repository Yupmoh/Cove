using System.IO;
using Cove.Generated;

namespace Cove.Cli;

internal static class CliReferenceDoc
{
    public static string Generate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Cove CLI Reference");
        sb.AppendLine();
        sb.AppendLine("Generated from the command registry. Do not edit by hand.");
        sb.AppendLine();
        sb.AppendLine("| Command | Source | Description |");
        sb.AppendLine("|---------|--------|-------------|");
        foreach (var entry in CoveCommandRegistry.Catalogue)
        {
            var desc = entry.Description ?? "";
            sb.AppendLine($"| `{entry.Command}` | {entry.Source} | {desc} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Total: {CoveCommandRegistry.Catalogue.Count} commands");
        return sb.ToString();
    }

    public static void WriteTo(string path)
    {
        File.WriteAllText(path, Generate());
    }
}
