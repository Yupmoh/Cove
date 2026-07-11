namespace Cove.Engine.Nooks;

public sealed record NookTypeDefinition(
    string Name,
    string DisplayName,
    string ContentSource,
    bool IsDockable);

public sealed class NookTypeRegistry
{
    private readonly Dictionary<string, NookTypeDefinition> _types = new();

    public void Register(NookTypeDefinition def) => _types[def.Name] = def;

    public void Unregister(string name) => _types.Remove(name);

    public bool IsRegistered(string name) => _types.ContainsKey(name);

    public NookTypeDefinition? Get(string name) =>
        _types.TryGetValue(name, out var def) ? def : null;

    public bool IsDockable(string name) =>
        _types.TryGetValue(name, out var def) && def.IsDockable;

    public IReadOnlyList<NookTypeDefinition> List() => _types.Values.ToList();

    public static NookTypeRegistry CreateWithBuiltins()
    {
        var registry = new NookTypeRegistry();
        registry.Register(new NookTypeDefinition("terminal", "Terminal", "pty", IsDockable: false));
        registry.Register(new NookTypeDefinition("editor", "Editor", "filesystem", IsDockable: true));
        registry.Register(new NookTypeDefinition("markdown", "Markdown", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("image", "Image", "file", IsDockable: true));
        registry.Register(new NookTypeDefinition("diff", "Diff", "git", IsDockable: true));
        registry.Register(new NookTypeDefinition("git", "Git", "source-control", IsDockable: true));
        registry.Register(new NookTypeDefinition("browser", "Browser", "webview", IsDockable: false));
        registry.Register(new NookTypeDefinition("notepad", "Notepad", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("tasks-kanban", "Tasks Board", "tasks", IsDockable: false));
        registry.Register(new NookTypeDefinition("tasks-list", "Tasks List", "tasks", IsDockable: true));
        registry.Register(new NookTypeDefinition("tasks-detail", "Task Detail", "tasks", IsDockable: true));
        registry.Register(new NookTypeDefinition("timeline-feed", "Timeline", "timeline", IsDockable: true));
        registry.Register(new NookTypeDefinition("note-markdown", "Markdown Note", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("note-sketch", "Sketch Note", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("note-canvas", "Canvas Note", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("note-html", "HTML Note", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("note-mermaid", "Mermaid Note", "note", IsDockable: true));
        registry.Register(new NookTypeDefinition("session-picker", "Session Picker", "vault", IsDockable: true));
        registry.Register(new NookTypeDefinition("library", "Library", "library", IsDockable: true));
        registry.Register(new NookTypeDefinition("snapshot-inspector", "Snapshots", "snapshot", IsDockable: true));
        registry.Register(new NookTypeDefinition("diff-review", "Diff Review", "review", IsDockable: true));
        registry.Register(new NookTypeDefinition("search", "Search", "search", IsDockable: true));
        return registry;
    }
}
