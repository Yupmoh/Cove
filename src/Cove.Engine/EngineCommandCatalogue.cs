using Cove.Generated;

namespace Cove.Engine;

public static class EngineCommandCatalogue
{
    public static IReadOnlyList<(string Command, string? Description, string Source)> Entries
    {
        get
        {
            var list = new System.Collections.Generic.List<(string, string?, string)>();
            foreach (var entry in CoveCommandRegistry.Catalogue)
                list.Add((entry.Command, entry.Description, entry.Source));
            return list;
        }
    }
}
