using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Cove.Adapters.Tests")]

namespace Cove.Adapters;

internal static class WindowsRecentSessionDiscovery
{
    public static List<RecentSession>? List(string adapter, string cwd)
        => adapter switch
        {
            "omp" => ListOmp(cwd, Environment.GetEnvironmentVariable("PI_CODING_AGENT_DIR")),
            "claude-code" => ListClaude(cwd, Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")),
            _ => null,
        };

    internal static List<RecentSession> ListOmp(string cwd, string? agentRoot)
    {
        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".omp", "agent");
        var roots = string.IsNullOrWhiteSpace(agentRoot) || PathsEqual(agentRoot, defaultRoot)
            ? [defaultRoot]
            : new[] { agentRoot, defaultRoot };
        var sessions = new List<RecentSession>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            var sessionsRoot = Path.Combine(root, "sessions");
            if (!Directory.Exists(sessionsRoot))
                continue;
            foreach (var file in Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories))
            {
                if (!TryReadLines(file, out var lines))
                    continue;
                string? id = null;
                string? sessionCwd = null;
                string? name = null;
                foreach (var line in lines)
                {
                    if (!TryParse(line, out var document))
                        continue;
                    using (document)
                    {
                        var rootElement = document.RootElement;
                        if (!rootElement.TryGetProperty("type", out var typeElement))
                            continue;
                        var type = typeElement.GetString();
                        if (type == "session")
                        {
                            id = StringProperty(rootElement, "id");
                            sessionCwd = StringProperty(rootElement, "cwd");
                        }
                        else if (type is "title" or "title_change")
                        {
                            var title = StringProperty(rootElement, "title");
                            if (!string.IsNullOrWhiteSpace(title))
                                name = title;
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(id) && seen.Add(id) && PathsEqual(sessionCwd, cwd))
                    sessions.Add(new RecentSession(id, name, sessionCwd, File.GetLastWriteTimeUtc(file)));
            }
        }
        return Sort(sessions);
    }

    internal static List<RecentSession> ListClaude(string cwd, string? configRoot)
    {
        var root = string.IsNullOrWhiteSpace(configRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configRoot;
        var projectDir = Path.Combine(root, "projects", ClaudeProjectSlug(cwd));
        if (!Directory.Exists(projectDir))
            return [];

        var names = LoadClaudeNames(Path.Combine(root, "sessions"));
        var sessions = new List<RecentSession>();
        foreach (var file in Directory.EnumerateFiles(projectDir, "*.jsonl", SearchOption.TopDirectoryOnly))
        {
            if (!TryReadLines(file, out var lines))
                continue;
            var id = Path.GetFileNameWithoutExtension(file);
            string? sessionCwd = null;
            string? name = names.GetValueOrDefault(id);
            foreach (var line in lines)
            {
                if (!TryParse(line, out var document))
                    continue;
                using (document)
                {
                    var rootElement = document.RootElement;
                    sessionCwd ??= StringProperty(rootElement, "cwd");
                    if (string.IsNullOrWhiteSpace(name))
                        name = StringProperty(rootElement, "aiTitle") ?? StringProperty(rootElement, "customTitle");
                }
            }
            if (PathsEqual(sessionCwd, cwd))
                sessions.Add(new RecentSession(id, name, sessionCwd, File.GetLastWriteTimeUtc(file)));
        }
        return Sort(sessions);
    }

    private static Dictionary<string, string> LoadClaudeNames(string sessionsDir)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(sessionsDir))
            return names;
        foreach (var file in Directory.EnumerateFiles(sessionsDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var id = StringProperty(document.RootElement, "sessionId");
                var name = StringProperty(document.RootElement, "name");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    names[id] = name;
            }
            catch (JsonException)
            {
            }
        }
        return names;
    }

    private static string ClaudeProjectSlug(string cwd)
    {
        var chars = cwd.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars);
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? StringProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryParse(string line, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(line);
            return true;
        }
        catch (JsonException)
        {
            document = null!;
            return false;
        }
    }

    private static bool TryReadLines(string path, out List<string> lines)
    {
        lines = [];
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
                lines.Add(line);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static List<RecentSession> Sort(List<RecentSession> sessions)
    {
        sessions.Sort((a, b) => (b.LastActive ?? DateTimeOffset.MinValue).CompareTo(a.LastActive ?? DateTimeOffset.MinValue));
        return sessions;
    }
}
