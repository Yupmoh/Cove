using System.Text.Json;

namespace Cove.Gui;

public static class FsListing
{
    public static string ListDirectory(string path, int cap)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            if (!Directory.Exists(path))
            {
                w.WriteStartArray("entries");
                w.WriteEndArray();
                w.WriteBoolean("truncated", false);
                w.WriteString("error", "not_found");
            }
            else
            {
                var entries = new List<(string Name, bool IsDir)>();
                var truncated = false;
                foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                {
                    if (entries.Count >= cap) { truncated = true; break; }
                    var isDir = Directory.Exists(entry);
                    entries.Add((Path.GetFileName(entry), isDir));
                }
                entries.Sort((a, b) => a.IsDir == b.IsDir
                    ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                    : (a.IsDir ? -1 : 1));
                w.WriteStartArray("entries");
                foreach (var (name, isDir) in entries)
                {
                    w.WriteStartObject();
                    w.WriteString("name", name);
                    w.WriteBoolean("isDir", isDir);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteBoolean("truncated", truncated);
                w.WriteNull("error");
            }
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
