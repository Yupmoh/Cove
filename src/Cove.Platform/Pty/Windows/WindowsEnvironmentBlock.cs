using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Cove.Platform.Pty.Windows;

public static class WindowsEnvironmentBlock
{
    public static List<string> BuildEntries(
        IReadOnlyDictionary<string, string> baseEnvironment,
        IReadOnlyDictionary<string, string>? overrides)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in baseEnvironment)
            map[kv.Key] = kv.Value;
        if (overrides is not null)
            foreach (var kv in overrides)
                map[kv.Key] = kv.Value;

        var keys = new List<string>(map.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);

        var entries = new List<string>(keys.Count);
        foreach (var key in keys)
            entries.Add($"{key}={map[key]}");
        return entries;
    }

    public static List<string> BuildEntries(IReadOnlyDictionary<string, string>? overrides)
        => BuildEntries(CurrentProcessEnvironment(), overrides);

    public static char[] BuildBlock(IReadOnlyDictionary<string, string>? overrides)
        => ToNullDelimitedBlock(BuildEntries(overrides));

    public static char[] ToNullDelimitedBlock(IReadOnlyList<string> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append(entry);
            builder.Append('\0');
        }
        builder.Append('\0');
        return builder.ToString().ToCharArray();
    }

    private static Dictionary<string, string> CurrentProcessEnvironment()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            map[(string)entry.Key] = (string)(entry.Value ?? string.Empty);
        return map;
    }
}
