using Cove.Persistence;

namespace Cove.Engine.Workspaces;

public enum WorkspaceChangeKind { Created, Switched, Deleted, Updated }

public sealed record WorkspaceChange(WorkspaceChangeKind Kind, string WorkspaceId);

public sealed class WorkspaceManager : IAsyncDisposable
{
    private readonly Actor<RegistryModel> _registry;
    private readonly Dictionary<string, Actor<WorkspaceModel>> _workspaces = new(StringComparer.Ordinal);
    private readonly object _mapGate = new();
    private readonly Action<WorkspaceChange>? _emit;
    private readonly Func<string> _newId;

    public WorkspaceManager(
        RegistryModel? registry = null,
        IEnumerable<WorkspaceModel>? workspaces = null,
        Action<WorkspaceChange>? emit = null,
        Func<string>? newId = null)
    {
        _newId = newId ?? (() => Guid.NewGuid().ToString("N"));
        _emit = emit;
        _registry = new Actor<RegistryModel>(registry ?? new RegistryModel());
        if (workspaces is not null)
            foreach (var workspace in workspaces)
                _workspaces[workspace.Id] = new Actor<WorkspaceModel>(workspace);
    }

    public RegistryModel Registry => _registry.State;

    public Actor<WorkspaceModel>? Get(string id)
    {
        lock (_mapGate)
            return _workspaces.TryGetValue(id, out var actor) ? actor : null;
    }

    public async Task<WorkspaceModel> CreateWorkspaceAsync(string name, string projectDir, string? collectionId = null)
    {
        var id = _newId();
        var paneId = _newId();
        var roomId = _newId();
        var model = new WorkspaceModel
        {
            Id = id,
            Name = name,
            ProjectDir = projectDir,
            CollectionId = collectionId ?? WorkspaceModel.DefaultCollectionId,
            Wings = [new Wing { Id = WorkspaceModel.MainWingId, Name = "main" }],
            Rooms = [new Room { Id = roomId, Name = "shell", WingId = WorkspaceModel.MainWingId, ActivePaneId = paneId, LayoutTree = new PaneLeaf { PaneId = paneId } }],
            Panes = new Dictionary<string, PaneRecord> { [paneId] = new PaneRecord { PaneId = paneId } },
            ActiveRoomId = roomId,
            FocusedPaneId = paneId,
        };

        lock (_mapGate)
            _workspaces[id] = new Actor<WorkspaceModel>(model);

        await _registry.Mutate(r => r with
        {
            OpenWorkspaces = Append(r.OpenWorkspaces, id),
            FocusedWorkspaceId = r.FocusedWorkspaceId ?? id,
        }).ConfigureAwait(false);

        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Created, id));
        return model;
    }

    public async Task<bool> SwitchWorkspaceAsync(string id)
    {
        if (Get(id) is null)
            return false;
        await _registry.Mutate(r => r with { FocusedWorkspaceId = id }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Switched, id));
        return true;
    }

    public IReadOnlyList<WorkspaceSummary> ListWorkspaces()
    {
        var registry = _registry.State;
        var focused = registry.FocusedWorkspaceId;
        var result = new List<WorkspaceSummary>();
        foreach (var id in registry.OpenWorkspaces)
        {
            var actor = Get(id);
            if (actor is null)
                continue;
            var w = actor.State;
            result.Add(new WorkspaceSummary(w.Id, w.Name, w.ProjectDir, w.CollectionId, w.IsWorktree, w.Id == focused));
        }
        return result;
    }

    public async Task<bool> DeleteWorkspaceAsync(string id)
    {
        Actor<WorkspaceModel>? actor;
        lock (_mapGate)
        {
            if (!_workspaces.TryGetValue(id, out actor))
                return false;
            _workspaces.Remove(id);
        }
        await actor.DisposeAsync().ConfigureAwait(false);

        await _registry.Mutate(r =>
        {
            var open = r.OpenWorkspaces.Where(x => x != id).ToList();
            var focused = r.FocusedWorkspaceId == id ? (open.Count > 0 ? open[0] : null) : r.FocusedWorkspaceId;
            return r with { OpenWorkspaces = open, FocusedWorkspaceId = focused };
        }).ConfigureAwait(false);

        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Deleted, id));
        return true;
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> list, string item)
    {
        var next = new List<string>(list);
        if (!next.Contains(item))
            next.Add(item);
        return next;
    }

    public async ValueTask DisposeAsync()
    {
        List<Actor<WorkspaceModel>> actors;
        lock (_mapGate)
        {
            actors = _workspaces.Values.ToList();
            _workspaces.Clear();
        }
        foreach (var actor in actors)
            await actor.DisposeAsync().ConfigureAwait(false);
        await _registry.DisposeAsync().ConfigureAwait(false);
    }
}
