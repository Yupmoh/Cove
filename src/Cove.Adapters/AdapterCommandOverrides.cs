namespace Cove.Adapters;

public sealed class AdapterCommandOverrides : Dictionary<string, IReadOnlyList<string>>
{
    public IReadOnlyList<string> Resolve(string adapter, IReadOnlyList<string> originalCommand)
    {
        if (!TryGetValue(adapter, out var prefix) || prefix.Count == 0)
            return originalCommand;
        var result = new string[prefix.Count + originalCommand.Count];
        for (int i = 0; i < prefix.Count; i++)
            result[i] = prefix[i];
        for (int i = 0; i < originalCommand.Count; i++)
            result[prefix.Count + i] = originalCommand[i];
        return result;
    }
}
