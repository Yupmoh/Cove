using System.IO;
using Cove.Adapters;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Skills;

public sealed class SkillsService : IDisposable
{
    private readonly SkillIndex _index;
    private readonly SkillIndexWatcher? _watcher;
    private bool _disposed;

    public SkillsService(string dataDir, string? baySkillsDir = null, bool includeHarnessRoots = true, ILogger? logger = null)
    {
        _index = new SkillIndex();
        var userSkills = Path.Combine(dataDir, "skills");
        _index.AddRoot(userSkills, SkillSource.CoveUser);
        if (baySkillsDir is not null)
            _index.AddRoot(baySkillsDir, SkillSource.CoveProject);
        if (includeHarnessRoots)
            AddHarnessRoots(_index);
        _index.Rebuild();
        _watcher = new SkillIndexWatcher(_index, logger);
        _watcher.Start();
    }

    private static void AddHarnessRoots(SkillIndex index)
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var harnessAdapters = new[] { "claude", "codex", "gemini", "qwen" };
        foreach (var adapter in harnessAdapters)
        {
            var harnessDir = Path.Combine(home, $".{adapter}", "skills");
            index.AddRoot(harnessDir, SkillSource.Harness, adapterName: adapter);
        }
    }

    public IReadOnlyList<SkillEntry> List() => _index.List();
    public IReadOnlyList<SkillEntry> Search(string query) => _index.Search(query);
    public SkillEntry? Resolve(string name) => _index.Resolve(name);
    public SkillIndex Index => _index;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
