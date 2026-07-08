namespace Cove.Engine.Panes;

public sealed record PaneTypeDefinition(
    string Name,
    string DisplayName,
    string ContentSource,
    bool IsDockable);

public sealed class PaneTypeRegistry
{
    private readonly Dictionary<string, PaneTypeDefinition> _types = new();

    public void Register(PaneTypeDefinition def) => _types[def.Name] = def;

    public void Unregister(string name) => _types.Remove(name);

    public bool IsRegistered(string name) => _types.ContainsKey(name);

    public PaneTypeDefinition? Get(string name) =>
        _types.TryGetValue(name, out var def) ? def : null;

    public bool IsDockable(string name) =>
        _types.TryGetValue(name, out var def) && def.IsDockable;

    public IReadOnlyList<PaneTypeDefinition> List() => _types.Values.ToList();

    public static PaneTypeRegistry CreateWithBuiltins()
    {
        var registry = new PaneTypeRegistry();
        registry.Register(new PaneTypeDefinition("terminal", "Terminal", "pty", IsDockable: false));
        registry.Register(new PaneTypeDefinition("editor", "Editor", "filesystem", IsDockable: true));
        registry.Register(new PaneTypeDefinition("markdown", "Markdown", "note", IsDockable: true));
        registry.Register(new PaneTypeDefinition("image", "Image", "file", IsDockable: true));
        registry.Register(new PaneTypeDefinition("diff", "Diff", "git", IsDockable: true));
        registry.Register(new PaneTypeDefinition("git", "Git", "source-control", IsDockable: true));
        registry.Register(new PaneTypeDefinition("browser", "Browser", "webview", IsDockable: false));
        registry.Register(new PaneTypeDefinition("notepad", "Notepad", "note", IsDockable: true));
        registry.Register(new PaneTypeDefinition("tasks-kanban", "Tasks Board", "tasks", IsDockable: false));
        registry.Register(new PaneTypeDefinition("tasks-list", "Tasks List", "tasks", IsDockable: true));
        registry.Register(new PaneTypeDefinition("tasks-detail", "Task Detail", "tasks", IsDockable: true));
        return registry;
    }
}
