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

    public string NewId() => _newId();

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
            result.Add(new WorkspaceSummary(w.Id, w.Name, w.ProjectDir, w.CollectionId, w.IsWorktree, w.Id == focused, w.Hidden));
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

    public async Task<Collection> CreateCollectionAsync(string name)
    {
        var collection = new Collection { Id = _newId(), Name = name };
        await _registry.Mutate(r => r with { Collections = new List<Collection>(r.Collections) { collection } }).ConfigureAwait(false);
        return collection;
    }

    public async Task<bool> RenameCollectionAsync(string id, string name)
    {
        if (id == WorkspaceModel.DefaultCollectionId || !_registry.State.Collections.Any(c => c.Id == id))
            return false;
        await _registry.Mutate(r => r with { Collections = r.Collections.Select(c => c.Id == id ? c with { Name = name } : c).ToList() }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RemoveCollectionAsync(string id)
    {
        if (id == WorkspaceModel.DefaultCollectionId || !_registry.State.Collections.Any(c => c.Id == id))
            return false;

        List<Actor<WorkspaceModel>> affected;
        lock (_mapGate)
            affected = _workspaces.Values.Where(a => a.State.CollectionId == id).ToList();
        foreach (var actor in affected)
            await actor.Mutate(m => m with { CollectionId = WorkspaceModel.DefaultCollectionId }).ConfigureAwait(false);

        await _registry.Mutate(r => r with
        {
            Collections = r.Collections.Where(c => c.Id != id).ToList(),
            ActiveCollectionId = r.ActiveCollectionId == id ? WorkspaceModel.DefaultCollectionId : r.ActiveCollectionId,
        }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> SwitchCollectionAsync(string id)
    {
        if (id != WorkspaceModel.DefaultCollectionId && !_registry.State.Collections.Any(c => c.Id == id))
            return false;
        await _registry.Mutate(r => r with { ActiveCollectionId = id }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> MoveWorkspaceToCollectionAsync(string workspaceId, string collectionId)
    {
        if (Get(workspaceId) is not { } actor)
            return false;
        if (collectionId != WorkspaceModel.DefaultCollectionId && !_registry.State.Collections.Any(c => c.Id == collectionId))
            return false;
        await actor.Mutate(m => m with { CollectionId = collectionId }).ConfigureAwait(false);
        return true;
    }

    public IReadOnlyList<CollectionSummary> ListCollections()
    {
        var registry = _registry.State;
        var userIds = registry.Collections.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        lock (_mapGate)
            foreach (var actor in _workspaces.Values)
            {
                var cid = actor.State.CollectionId;
                var bucket = userIds.Contains(cid) ? cid : WorkspaceModel.DefaultCollectionId;
                counts[bucket] = counts.GetValueOrDefault(bucket) + 1;
            }

        var result = new List<CollectionSummary>
        {
            new(WorkspaceModel.DefaultCollectionId, "Default", counts.GetValueOrDefault(WorkspaceModel.DefaultCollectionId).ToString(), registry.ActiveCollectionId == WorkspaceModel.DefaultCollectionId),
        };
        foreach (var c in registry.Collections)
            result.Add(new CollectionSummary(c.Id, c.Name, counts.GetValueOrDefault(c.Id).ToString(), registry.ActiveCollectionId == c.Id));
        return result;
    }

    public async Task<bool> SetWorkspaceHiddenAsync(string id, bool hidden)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { Hidden = hidden }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Updated, id));
        return true;
    }

    public async Task<bool> SetWorkspaceIconAsync(string id, WorkspaceIcon? icon)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { Icon = icon }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Updated, id));
        return true;
    }

    public async Task<bool> SetWorkspaceAccentAsync(string id, string? accent)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { AccentColor = accent }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Updated, id));
        return true;
    }

    public async Task ReorderWorkspacesAsync(IReadOnlyList<string> orderedIds)
    {
        await _registry.Mutate(r =>
        {
            var known = new HashSet<string>(r.OpenWorkspaces, StringComparer.Ordinal);
            var next = new List<string>();
            foreach (var id in orderedIds)
                if (known.Contains(id) && !next.Contains(id))
                    next.Add(id);
            foreach (var id in r.OpenWorkspaces)
                if (!next.Contains(id))
                    next.Add(id);
            return r with { OpenWorkspaces = next };
        }).ConfigureAwait(false);
    }

    public async Task<bool> MoveRoomAsync(string fromWorkspaceId, string roomId, string toWorkspaceId)
    {
        if (Get(fromWorkspaceId) is not { } from || Get(toWorkspaceId) is not { } to)
            return false;
        var moved = from.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (moved is null)
            return false;

        var movedPanes = new Dictionary<string, PaneRecord>();
        foreach (var paneId in WorkspaceInvariants.CollectPaneIds(moved.LayoutTree))
            if (from.State.Panes.TryGetValue(paneId, out var record))
                movedPanes[paneId] = record;

        await from.Mutate(m => WorkspaceInvariants.CloseRoom(m, roomId, NewId)).ConfigureAwait(false);
        await to.Mutate(m =>
        {
            var rooms = new List<Room>(m.Rooms) { moved with { WingId = WorkspaceModel.MainWingId } };
            var panes = new Dictionary<string, PaneRecord>(m.Panes);
            foreach (var kv in movedPanes)
                panes[kv.Key] = kv.Value;
            return m with { Rooms = rooms, Panes = panes, ActiveRoomId = moved.Id };
        }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Updated, toWorkspaceId));
        return true;
    }

    public async Task<string?> DockResidentAsync(string workspaceId, string? paneId, string scope, int slot)
    {
        if (Get(workspaceId) is not { } actor)
            return null;
        var id = paneId ?? _newId();
        await actor.Mutate(m =>
        {
            var panes = new Dictionary<string, PaneRecord>(m.Panes);
            var existing = panes.TryGetValue(id, out var record) ? record : new PaneRecord { PaneId = id };
            panes[id] = existing with { ResidentScope = scope, ResidentSlot = slot };
            return m with { Panes = panes };
        }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Updated, workspaceId));
        return id;
    }

    public async Task<bool> UndockResidentAsync(string workspaceId, string paneId)
    {
        if (Get(workspaceId) is not { } actor || !actor.State.Panes.ContainsKey(paneId))
            return false;
        await actor.Mutate(m =>
        {
            var panes = new Dictionary<string, PaneRecord>(m.Panes);
            if (panes.TryGetValue(paneId, out var record))
                panes[paneId] = record with { ResidentScope = "none", ResidentSlot = -1 };
            return m with { Panes = panes };
        }).ConfigureAwait(false);
        _emit?.Invoke(new WorkspaceChange(WorkspaceChangeKind.Updated, workspaceId));
        return true;
    }

    public async Task<bool> SetResidentCollapsedAsync(string workspaceId, string paneId, bool collapsed)
    {
        if (Get(workspaceId) is not { } actor || !actor.State.Panes.ContainsKey(paneId))
            return false;
        await actor.Mutate(m =>
        {
            var panes = new Dictionary<string, PaneRecord>(m.Panes);
            if (panes.TryGetValue(paneId, out var record))
                panes[paneId] = record with { ResidentCollapsed = collapsed };
            return m with { Panes = panes };
        }).ConfigureAwait(false);
        return true;
    }

    public IReadOnlyList<ResidentSummary> ListResidents(string workspaceId)
    {
        var result = new List<ResidentSummary>();
        lock (_mapGate)
        {
            if (_workspaces.TryGetValue(workspaceId, out var owner))
                foreach (var p in owner.State.Panes.Values)
                    if (p.ResidentScope == "workspace" && p.ResidentSlot >= 0)
                        result.Add(new ResidentSummary(p.PaneId, workspaceId, "workspace", p.ResidentSlot, p.ResidentCollapsed));
            foreach (var kv in _workspaces)
                foreach (var p in kv.Value.State.Panes.Values)
                    if (p.ResidentScope == "global" && p.ResidentSlot >= 0)
                        result.Add(new ResidentSummary(p.PaneId, kv.Key, "global", p.ResidentSlot, p.ResidentCollapsed));
        }
        return result;
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
