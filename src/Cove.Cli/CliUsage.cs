using System.Linq;

namespace Cove.Cli;

internal static class CliUsage
{
    internal static bool IsHelpRequested(string[] args)
    {
        foreach (var arg in args)
            if (arg == "--help" || arg == "-h" || arg == "help")
                return true;
        return false;
    }

    internal static void Write(System.IO.TextWriter stdout)
    {
        stdout.WriteLine("cove — an open-source AI-native terminal bay");
        stdout.WriteLine("usage: cove [--channel <stable|beta|dev>] <command> [args]");
        stdout.WriteLine("");
        stdout.WriteLine("Commands:");
        var all = new System.Collections.Generic.List<(string Command, string Source)>();
        foreach (var entry in Cove.Generated.CoveCommandRegistry.Catalogue)
            all.Add((entry.Command, entry.Source));
        foreach (var entry in Cove.Engine.EngineCommandCatalogue.Entries)
            all.Add((entry.Command, entry.Source));
        foreach (var entry in all.OrderBy(c => c.Source).ThenBy(c => c.Command))
            stdout.WriteLine($"  [{entry.Source}] {entry.Command}");
    }
}
