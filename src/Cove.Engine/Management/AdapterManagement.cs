namespace Cove.Engine.Management;

public sealed class AdapterRetentionPolicy
{
    private readonly int _maxSessions;
    private readonly TimeSpan _maxAge;

    public AdapterRetentionPolicy(int maxSessions, TimeSpan maxAge)
    {
        _maxSessions = maxSessions;
        _maxAge = maxAge;
    }

    public IEnumerable<string> Enforce(string sessionDir)
    {
        if (!Directory.Exists(sessionDir))
            return Array.Empty<string>();

        var files = Directory.GetFiles(sessionDir, "*.json")
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.LastWriteTime)
            .ToList();

        var removed = new List<string>();
        var cutoff = DateTimeOffset.UtcNow - _maxAge;

        foreach (var file in files.ToList())
        {
            if (file.LastWriteTime < cutoff.LocalDateTime)
            {
                try { File.Delete(file.FullName); removed.Add(file.Name); }
                catch { }
            }
        }

        var remaining = files.Where(f => File.Exists(f.FullName)).ToList();
        while (remaining.Count > _maxSessions)
        {
            var oldest = remaining[0];
            try { File.Delete(oldest.FullName); removed.Add(oldest.Name); }
            catch { }
            remaining.RemoveAt(0);
        }

        return removed;
    }
}

public sealed record RecommendedAdapter(string Name, string DisplayName, string Description);

public sealed class FirstRunWizard
{
    private readonly string _dataDir;
    private readonly string _markerPath;

    public FirstRunWizard(string dataDir)
    {
        _dataDir = dataDir;
        _markerPath = Path.Combine(dataDir, ".firstrun-complete");
    }

    public bool IsFirstRun()
    {
        if (File.Exists(_markerPath))
            return false;
        var adaptersDir = Path.Combine(_dataDir, "adapters");
        if (!Directory.Exists(adaptersDir))
            return true;
        return !Directory.EnumerateDirectories(adaptersDir).Any();
    }

    public void MarkComplete()
    {
        Directory.CreateDirectory(_dataDir);
        File.WriteAllText(_markerPath, DateTimeOffset.UtcNow.ToString("O"));
    }

    public IReadOnlyList<RecommendedAdapter> GetRecommendedAdapters()
    {
        return new List<RecommendedAdapter>
        {
            new("claude-code", "Claude Code", "Anthropic's Claude Code CLI agent"),
            new("codex", "Codex", "OpenAI's Codex CLI agent"),
            new("gemini", "Gemini CLI", "Google's Gemini CLI agent"),
        };
    }
}
