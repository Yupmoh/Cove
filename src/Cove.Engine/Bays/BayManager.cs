using Cove.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Engine.Bays;

public enum BayChangeKind { Created, Switched, Deleted, Updated }

public sealed record BayChange(BayChangeKind Kind, string BayId);

public readonly record struct BayCreateOutcome(BayModel? Bay, string? ErrorCode, string? ErrorMessage);

public sealed class BayManager : IAsyncDisposable
{
    private readonly Actor<RegistryModel> _registry;
    private readonly Dictionary<string, Actor<BayModel>> _bays = new(StringComparer.Ordinal);
    private readonly object _mapGate = new();
    private readonly Action<BayChange>? _emit;
    private readonly Func<string> _newId;
    private readonly WorktreeService _worktrees;
    private readonly ILogger _logger;
    private readonly Dictionary<string, GitWatchService> _watchers = new(StringComparer.Ordinal);
    private readonly Action<IReadOnlyList<string>>? _persistOrder;
    public BayManager(
        RegistryModel? registry = null,
        IEnumerable<BayModel>? bays = null,
        Action<BayChange>? emit = null,
        Func<string>? newId = null,
        IGitRunner? gitRunner = null,
        ILogger? logger = null,
        Action<IReadOnlyList<string>>? persistOrder = null)
    {
        _newId = newId ?? (() => Guid.NewGuid().ToString("N"));
        _emit = emit;
        _logger = logger ?? NullLogger.Instance;
        _worktrees = new WorktreeService(gitRunner ?? new ProcessGitRunner());
        _registry = new Actor<RegistryModel>(registry ?? new RegistryModel());
        _persistOrder = persistOrder;
        if (bays is not null)
            foreach (var bay in bays)
                _bays[bay.Id] = new Actor<BayModel>(bay);
    }

    public static bool TryResolveProjectDir(string? raw, out string resolved, out string? error)
    {
        resolved = "";
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "path is required";
            return false;
        }
        var trimmed = raw.Trim();
        if (trimmed == "~"
            || trimmed.StartsWith("~/", StringComparison.Ordinal)
            || trimmed.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            trimmed = trimmed.Length <= 1 ? home : Path.Combine(home, trimmed[2..]);
        }
        try
        {
            resolved = Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "invalid path: " + ex.Message;
            return false;
        }
        return true;
    }

    public async Task<BayCreateOutcome> CreateValidatedBayAsync(string? name, string? rawProjectDir, string? collectionId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("bay.create rejected: empty name");
            return new BayCreateOutcome(null, "bad_params", "name is required");
        }
        if (!TryResolveProjectDir(rawProjectDir, out var resolved, out var pathError))
        {
            _logger.LogWarning("bay.create rejected: invalid path {Path}", rawProjectDir);
            return new BayCreateOutcome(null, "bad_params", pathError ?? "path is required");
        }
        try
        {
            Directory.CreateDirectory(resolved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "bay.create could not create directory {Path}", resolved);
            return new BayCreateOutcome(null, "invalid_path", "could not create directory: " + ex.Message);
        }
        var bay = await CreateBayAsync(name, resolved, collectionId).ConfigureAwait(false);
        return new BayCreateOutcome(bay, null, null);
    }

    public RegistryModel Registry => _registry.State;

    public string NewId() => _newId();

    public Actor<BayModel>? Get(string id)
    {
        lock (_mapGate)
            return _bays.TryGetValue(id, out var actor) ? actor : null;
    }

    public async Task<BayModel> CreateBayAsync(string name, string projectDir, string? collectionId = null)
    {
        var id = _newId();
        var model = new BayModel
        {
            Id = id,
            Name = name,
            ProjectDir = projectDir,
            CollectionId = collectionId ?? BayModel.DefaultCollectionId,
            Nooks = new Dictionary<string, NookRecord>(),
        };

        lock (_mapGate)
            _bays[id] = new Actor<BayModel>(model);

        await _registry.Mutate(r => r with
        {
            OpenBays = Append(r.OpenBays, id),
            FocusedBayId = r.FocusedBayId ?? id,
        }).ConfigureAwait(false);

        _emit?.Invoke(new BayChange(BayChangeKind.Created, id));
        return model;
    }

    public async Task<BayModel> AdoptExistingAsync(string id, string name, string projectDir, string? collectionId = null, BayIcon? icon = null)
    {
        var model = new BayModel
        {
            Id = id,
            Name = name,
            ProjectDir = projectDir,
            CollectionId = collectionId ?? BayModel.DefaultCollectionId,
            Nooks = new Dictionary<string, NookRecord>(),
            Icon = icon,
        };
        lock (_mapGate)
            _bays[id] = new Actor<BayModel>(model);
        await _registry.Mutate(r => r with
        {
            OpenBays = Append(r.OpenBays, id),
            FocusedBayId = r.FocusedBayId ?? id,
        }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Created, id));
        return model;
    }

    public async Task<bool> SwitchBayAsync(string id)
    {
        if (Get(id) is null)
            return false;
        await _registry.Mutate(r => r with { FocusedBayId = id }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Switched, id));
        return true;
    }

    public IReadOnlyList<BaySummary> ListBays()
    {
        var registry = _registry.State;
        var focused = registry.FocusedBayId;
        var result = new List<BaySummary>();
        foreach (var id in registry.OpenBays)
        {
            var actor = Get(id);
            if (actor is null)
                continue;
            var w = actor.State;
            result.Add(new BaySummary(w.Id, w.Name, w.ProjectDir, w.CollectionId, w.IsWorktree, w.Id == focused, w.Hidden, w.Icon?.Kind, w.Icon?.Value));
        }
        return result;
    }

    public async Task<bool> DeleteBayAsync(string id)
    {
        Actor<BayModel>? actor;
        lock (_mapGate)
        {
            if (!_bays.TryGetValue(id, out actor))
                return false;
            _bays.Remove(id);
        }
        await actor.DisposeAsync().ConfigureAwait(false);

        await _registry.Mutate(r =>
        {
            var open = r.OpenBays.Where(x => x != id).ToList();
            var focused = r.FocusedBayId == id ? (open.Count > 0 ? open[0] : null) : r.FocusedBayId;
            return r with { OpenBays = open, FocusedBayId = focused };
        }).ConfigureAwait(false);

        _emit?.Invoke(new BayChange(BayChangeKind.Deleted, id));
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
        if (id == BayModel.DefaultCollectionId || !_registry.State.Collections.Any(c => c.Id == id))
            return false;
        await _registry.Mutate(r => r with { Collections = r.Collections.Select(c => c.Id == id ? c with { Name = name } : c).ToList() }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RemoveCollectionAsync(string id)
    {
        if (id == BayModel.DefaultCollectionId || !_registry.State.Collections.Any(c => c.Id == id))
            return false;

        List<Actor<BayModel>> affected;
        lock (_mapGate)
            affected = _bays.Values.Where(a => a.State.CollectionId == id).ToList();
        foreach (var actor in affected)
            await actor.Mutate(m => m with { CollectionId = BayModel.DefaultCollectionId }).ConfigureAwait(false);

        await _registry.Mutate(r => r with
        {
            Collections = r.Collections.Where(c => c.Id != id).ToList(),
            ActiveCollectionId = r.ActiveCollectionId == id ? BayModel.DefaultCollectionId : r.ActiveCollectionId,
        }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> SwitchCollectionAsync(string id)
    {
        if (id != BayModel.DefaultCollectionId && !_registry.State.Collections.Any(c => c.Id == id))
            return false;
        await _registry.Mutate(r => r with { ActiveCollectionId = id }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> MoveBayToCollectionAsync(string bayId, string collectionId)
    {
        if (Get(bayId) is not { } actor)
            return false;
        if (collectionId != BayModel.DefaultCollectionId && !_registry.State.Collections.Any(c => c.Id == collectionId))
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
            foreach (var actor in _bays.Values)
            {
                var cid = actor.State.CollectionId;
                var bucket = userIds.Contains(cid) ? cid : BayModel.DefaultCollectionId;
                counts[bucket] = counts.GetValueOrDefault(bucket) + 1;
            }

        var result = new List<CollectionSummary>
        {
            new(BayModel.DefaultCollectionId, "Default", counts.GetValueOrDefault(BayModel.DefaultCollectionId).ToString(), registry.ActiveCollectionId == BayModel.DefaultCollectionId),
        };
        foreach (var c in registry.Collections)
            result.Add(new CollectionSummary(c.Id, c.Name, counts.GetValueOrDefault(c.Id).ToString(), registry.ActiveCollectionId == c.Id));
        return result;
    }

    public async Task<bool> RenameBayAsync(string id, string name)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { Name = name }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Updated, id));
        return true;
    }

    public async Task<bool> SetBayHiddenAsync(string id, bool hidden)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { Hidden = hidden }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Updated, id));
        return true;
    }

    public async Task<bool> SetBayIconAsync(string id, BayIcon? icon)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { Icon = icon }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Updated, id));
        return true;
    }

    public async Task<bool> SetBayAccentAsync(string id, string? accent)
    {
        if (Get(id) is not { } actor)
            return false;
        await actor.Mutate(m => m with { AccentColor = accent }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Updated, id));
        return true;
    }

    public async Task ReorderBaysAsync(IReadOnlyList<string> orderedIds)
    {
        await _registry.Mutate(r =>
        {
            var known = new HashSet<string>(r.OpenBays, StringComparer.Ordinal);
            var next = new List<string>();
            foreach (var id in orderedIds)
                if (known.Contains(id) && !next.Contains(id))
                    next.Add(id);
            foreach (var id in r.OpenBays)
                if (!next.Contains(id))
                    next.Add(id);
            return r with { OpenBays = next };
        }).ConfigureAwait(false);
        _persistOrder?.Invoke(_registry.State.OpenBays);
    }
    public async Task<string?> DockResidentAsync(string bayId, string? nookId, string scope, int slot)
    {
        if (Get(bayId) is not { } actor)
            return null;
        var id = nookId ?? _newId();
        await actor.Mutate(m =>
        {
            var nooks = new Dictionary<string, NookRecord>(m.Nooks);
            var existing = nooks.TryGetValue(id, out var record) ? record : new NookRecord { NookId = id };
            nooks[id] = existing with { ResidentScope = scope, ResidentSlot = slot };
            return m with { Nooks = nooks };
        }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Updated, bayId));
        return id;
    }

    public async Task<bool> UndockResidentAsync(string bayId, string nookId)
    {
        if (Get(bayId) is not { } actor || !actor.State.Nooks.ContainsKey(nookId))
            return false;
        await actor.Mutate(m =>
        {
            var nooks = new Dictionary<string, NookRecord>(m.Nooks);
            if (nooks.TryGetValue(nookId, out var record))
                nooks[nookId] = record with { ResidentScope = "none", ResidentSlot = -1 };
            return m with { Nooks = nooks };
        }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Updated, bayId));
        return true;
    }

    public async Task<bool> SetResidentHeightAsync(string bayId, string nookId, int height)
    {
        if (Get(bayId) is not { } actor || !actor.State.Nooks.ContainsKey(nookId))
            return false;
        await actor.Mutate(m =>
        {
            var nooks = new Dictionary<string, NookRecord>(m.Nooks);
            if (nooks.TryGetValue(nookId, out var record))
                nooks[nookId] = record with { ResidentHeight = height };
            return m with { Nooks = nooks };
        }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> SetResidentCollapsedAsync(string bayId, string nookId, bool collapsed)
    {
        if (Get(bayId) is not { } actor || !actor.State.Nooks.ContainsKey(nookId))
            return false;
        await actor.Mutate(m =>
        {
            var nooks = new Dictionary<string, NookRecord>(m.Nooks);
            if (nooks.TryGetValue(nookId, out var record))
                nooks[nookId] = record with { ResidentCollapsed = collapsed };
            return m with { Nooks = nooks };
        }).ConfigureAwait(false);
        return true;
    }

    public IReadOnlyList<ResidentSummary> ListResidents(string bayId)
    {
        var result = new List<ResidentSummary>();
        lock (_mapGate)
        {
            if (_bays.TryGetValue(bayId, out var owner))
                foreach (var p in owner.State.Nooks.Values)
                    if (p.ResidentScope == "bay" && p.ResidentSlot >= 0)
                        result.Add(new ResidentSummary(p.NookId, bayId, "bay", p.ResidentSlot, p.ResidentCollapsed));
            foreach (var kv in _bays)
                foreach (var p in kv.Value.State.Nooks.Values)
                    if (p.ResidentScope == "global" && p.ResidentSlot >= 0)
                        result.Add(new ResidentSummary(p.NookId, kv.Key, "global", p.ResidentSlot, p.ResidentCollapsed));
        }
        return result;
    }

    public async Task<BayModel?> CreateWorktreeAsync(string parentId, string branch, string location, bool newBranch, string? baseRef = null)
    {
        if (Get(parentId) is not { } parent)
            return null;
        var result = await _worktrees.CreateAsync(parent.State.ProjectDir, location, branch, newBranch, baseRef).ConfigureAwait(false);
        if (!result.Ok)
            return null;

        var id = _newId();
        var model = new BayModel
        {
            Id = id,
            Name = branch,
            ProjectDir = location,
            CollectionId = parent.State.CollectionId,
            IsWorktree = true,
            ParentBayId = parentId,
            WorktreeBranch = branch,
            Nooks = new Dictionary<string, NookRecord>(),
        };
        lock (_mapGate)
            _bays[id] = new Actor<BayModel>(model);
        await _registry.Mutate(r => r with { OpenBays = Append(r.OpenBays, id) }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Created, id));
        return model;
    }

    public IReadOnlyList<BayModel> ListWorktrees(string parentId)
    {
        lock (_mapGate)
            return _bays.Values
                .Where(a => a.State.IsWorktree && a.State.ParentBayId == parentId)
                .Select(a => a.State)
                .ToList();
    }

    public async Task<BayModel?> AdoptWorktreeAsync(string parentId, string location, string branch)
    {
        if (Get(parentId) is not { } parent)
            return null;
        var bound = new HashSet<string>(StringComparer.Ordinal);
        lock (_mapGate)
            foreach (var a in _bays.Values)
                if (a.State.IsWorktree)
                    bound.Add(PathRealpath.Normalize(a.State.ProjectDir));
        var orphans = await _worktrees.OrphansAsync(parent.State.ProjectDir, bound).ConfigureAwait(false);
        var target = PathRealpath.Normalize(location);
        if (!orphans.Any(o => PathRealpath.Normalize(o) == target))
            return null;

        var id = _newId();
        var model = new BayModel
        {
            Id = id,
            Name = branch,
            ProjectDir = location,
            CollectionId = parent.State.CollectionId,
            IsWorktree = true,
            ParentBayId = parentId,
            WorktreeBranch = branch,
            Nooks = new Dictionary<string, NookRecord>(),
        };
        lock (_mapGate)
            _bays[id] = new Actor<BayModel>(model);
        await _registry.Mutate(r => r with { OpenBays = Append(r.OpenBays, id) }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Created, id));
        return model;
    }

    public async Task<bool> RemoveWorktreeAsync(string worktreeBayId, bool force = true)
    {
        if (Get(worktreeBayId) is not { } actor)
            return false;
        var w = actor.State;
        if (!w.IsWorktree || w.ParentBayId is not { } parentId || Get(parentId) is not { } parent)
            return false;
        var result = await _worktrees.RemoveAsync(parent.State.ProjectDir, w.ProjectDir, force).ConfigureAwait(false);
        if (!result.Ok)
            return false;

        lock (_mapGate)
            _bays.Remove(worktreeBayId);
        await actor.DisposeAsync().ConfigureAwait(false);
        await _registry.Mutate(r =>
        {
            var open = r.OpenBays.Where(x => x != worktreeBayId).ToList();
            var focused = r.FocusedBayId == worktreeBayId ? parentId : r.FocusedBayId;
            return r with { OpenBays = open, FocusedBayId = focused };
        }).ConfigureAwait(false);
        _emit?.Invoke(new BayChange(BayChangeKind.Deleted, worktreeBayId));
        return true;
    }

    public Task<IReadOnlyList<string>> WorktreeOrphansAsync(string parentId)
    {
        if (Get(parentId) is not { } parent)
            return Task.FromResult<IReadOnlyList<string>>([]);
        List<string> bound;
        lock (_mapGate)
            bound = _bays.Values
                .Where(a => a.State.IsWorktree && a.State.ParentBayId == parentId)
                .Select(a => PathRealpath.Normalize(a.State.ProjectDir))
                .ToList();
        return _worktrees.OrphansAsync(parent.State.ProjectDir, bound);
    }

    public async Task<bool> PruneWorktreesAsync(string parentId)
    {
        if (Get(parentId) is not { } parent)
            return false;
        var result = await _worktrees.PruneAsync(parent.State.ProjectDir).ConfigureAwait(false);
        return result.Ok;
    }

    public async Task RefreshWorktreesAsync(string parentId)
    {
        if (Get(parentId) is not { } parent)
            return;
        var entries = await _worktrees.ListAsync(parent.State.ProjectDir).ConfigureAwait(false);
        if (entries.Count == 0)
            return;
        var onDisk = entries.Where(e => !string.IsNullOrEmpty(e.Path)).Skip(1).ToDictionary(e => PathRealpath.Normalize(e.Path!), StringComparer.Ordinal);

        List<Actor<BayModel>> bound;
        lock (_mapGate)
            bound = _bays.Values
                .Where(a => a.State.IsWorktree && a.State.ParentBayId == parentId)
                .ToList();

        var deletedIds = new List<string>();
        var updatedIds = new List<string>();
        foreach (var actor in bound)
        {
            var w = actor.State;
            var key = PathRealpath.Normalize(w.ProjectDir);
            if (!onDisk.ContainsKey(key))
            {
                deletedIds.Add(w.Id);
                continue;
            }
            if (onDisk[key].Branch is { } rawBranch)
            {
                var shortBranch = rawBranch.StartsWith("refs/heads/", StringComparison.Ordinal) ? rawBranch["refs/heads/".Length..] : rawBranch;
                if (!string.Equals(w.WorktreeBranch, shortBranch, StringComparison.Ordinal))
                {
                    await actor.Mutate(m => m with { WorktreeBranch = shortBranch }).ConfigureAwait(false);
                    updatedIds.Add(w.Id);
                }
            }
        }

        foreach (var id in deletedIds)
        {
            Actor<BayModel>? actor;
            lock (_mapGate)
            {
                if (!_bays.TryGetValue(id, out actor))
                    continue;
                _bays.Remove(id);
            }
            await actor.DisposeAsync().ConfigureAwait(false);
            await _registry.Mutate(r =>
            {
                var open = r.OpenBays.Where(x => x != id).ToList();
                var focused = r.FocusedBayId == id ? parentId : r.FocusedBayId;
                return r with { OpenBays = open, FocusedBayId = focused };
            }).ConfigureAwait(false);
            _emit?.Invoke(new BayChange(BayChangeKind.Deleted, id));
        }

        foreach (var id in updatedIds)
            _emit?.Invoke(new BayChange(BayChangeKind.Updated, id));
    }

    public Task WatchWorktreeRepoAsync(string parentId)
    {
        if (Get(parentId) is not { } parent)
            return Task.CompletedTask;
        lock (_watchers)
        {
            if (_watchers.ContainsKey(parentId))
                return Task.CompletedTask;
            var watch = new GitWatchService(async _ => await RefreshWorktreesAsync(parentId).ConfigureAwait(false));
            watch.Start(parent.State.ProjectDir);
            _watchers[parentId] = watch;
        }
        return Task.CompletedTask;
    }

    public Task UnwatchWorktreeRepoAsync(string parentId)
    {
        lock (_watchers)
        {
            if (_watchers.Remove(parentId, out var watch))
                watch.Dispose();
        }
        return Task.CompletedTask;
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
        List<GitWatchService> watchers;
        lock (_watchers)
        {
            watchers = _watchers.Values.ToList();
            _watchers.Clear();
        }
        foreach (var w in watchers)
            w.Dispose();
        List<Actor<BayModel>> actors;
        lock (_mapGate)
        {
            actors = _bays.Values.ToList();
            _bays.Clear();
        }
        foreach (var actor in actors)
            await actor.DisposeAsync().ConfigureAwait(false);
        await _registry.DisposeAsync().ConfigureAwait(false);
    }
}
