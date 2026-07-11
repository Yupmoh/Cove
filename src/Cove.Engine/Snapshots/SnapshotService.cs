using System.Security.Cryptography;
using Cove.Engine.Bays;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Snapshots;

public enum SnapshotTrigger { Interval, Shutdown, PreUpdate, PreRestore, Manual, Event }

public sealed record Snapshot(string Id, string Hash, SnapshotTrigger Trigger, DateTimeOffset TakenAtUtc, bool Pinned);
public sealed record SnapshotDiff(string Key, string? OldValue, string? NewValue, string ChangeType);

public sealed class SnapshotService
{
    private readonly string _coveDir;
    private readonly string _snapshotsDir;
    private readonly IGitRunner _git;
    private readonly ILogger _logger;
    private string? _lastHash;

    public SnapshotService(string coveDir, string snapshotsDir, IGitRunner git, ILogger logger)
    {
        _coveDir = coveDir;
        _snapshotsDir = snapshotsDir;
        _git = git;
        _logger = logger;
    }

    public async Task<Snapshot?> TakeAsync(IReadOnlyDictionary<string, string> content, SnapshotTrigger trigger)
    {
        await EnsureRepoAsync(_coveDir).ConfigureAwait(false);
        await EnsureRepoAsync(_snapshotsDir).ConfigureAwait(false);

        var filtered = FilterSecrets(content);
        var hash = ComputeHash(filtered);
        var skipOnHashMatch = trigger is SnapshotTrigger.Interval or SnapshotTrigger.Shutdown or SnapshotTrigger.PreUpdate;
        if (skipOnHashMatch && hash == _lastHash)
            return null;

        var label = trigger.ToString().ToLowerInvariant();
        var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("X");

        foreach (var kv in filtered)
        {
            var path = Path.Combine(_snapshotsDir, kv.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, kv.Value).ConfigureAwait(false);
        }
        await _git.RunAsync(_coveDir, ["add", "-A"], CancellationToken.None).ConfigureAwait(false);
        await _git.RunAsync(_coveDir, ["commit", "-m", $"{label}:{id} v{Cove.Platform.CoveBuild.InformationalVersion}", "--allow-empty"], CancellationToken.None).ConfigureAwait(false);


        await _git.RunAsync(_snapshotsDir, ["add", "-A"], CancellationToken.None).ConfigureAwait(false);
        var commitResult = await _git.RunAsync(_snapshotsDir, ["commit", "-m", $"{label}:{id}", "--allow-empty"], CancellationToken.None).ConfigureAwait(false);
        if (!commitResult.Ok)
        {
            _logger.LogWarning("snapshot commit failed: {Error}", commitResult.Stderr);
            return null;
        }
        var revResult = await _git.RunAsync(_snapshotsDir, ["rev-parse", "HEAD"], CancellationToken.None).ConfigureAwait(false);
        var commitId = revResult.Ok ? revResult.Stdout.Trim() : hash;

        _lastHash = hash;
        return new Snapshot(id, commitId, trigger, DateTimeOffset.UtcNow, false);
    }

    private static IReadOnlyDictionary<string, string> FilterSecrets(IReadOnlyDictionary<string, string> content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in content)
        {
            if (IsSecretPath(kv.Key))
                continue;
            result[kv.Key] = kv.Value;
        }
        return result;
    }

    private static bool IsSecretPath(string path)
    {
        var lower = path.ToLowerInvariant();
        if (lower.Contains("secret") || lower.Contains(".env") || lower.Contains("credential") || lower.Contains("token"))
            return true;
        if (lower.EndsWith("cookies") || lower.EndsWith(".key") || lower.EndsWith(".pem"))
            return true;
        return false;
    }

    public async Task<IReadOnlyList<Snapshot>> ListAsync()
    {
        await EnsureRepoAsync(_snapshotsDir).ConfigureAwait(false);
        var result = await _git.RunAsync(_snapshotsDir, ["log", "--pretty=format:%H|%s|%ci"], CancellationToken.None).ConfigureAwait(false);
        if (!result.Ok)
            return [];
        var snapshots = new List<Snapshot>();
        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 3);
            if (parts.Length < 3)
                continue;
            var commitHash = parts[0];
            var message = parts[1];
            var date = parts[2];
            var colonIdx = message.IndexOf(':');
            if (colonIdx < 0)
                continue;
            var label = message[..colonIdx];
            var id = message[(colonIdx + 1)..];
            if (Enum.TryParse<SnapshotTrigger>(label, ignoreCase: true, out var trig) && DateTimeOffset.TryParse(date, out var takenAt))
                snapshots.Add(new Snapshot(id, commitHash, trig, takenAt, false));
        }
        return snapshots.OrderByDescending(s => s.TakenAtUtc).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>?> RestoreAsync(string snapshotId)
    {
        var list = await ListAsync().ConfigureAwait(false);
        var snap = list.FirstOrDefault(s => s.Id == snapshotId);
        if (snap is null)
            return null;

        var currentContent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(_snapshotsDir, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_snapshotsDir, file).Replace('\\', '/');
            currentContent[rel] = await File.ReadAllTextAsync(file).ConfigureAwait(false);
        }
        await TakeAsync(currentContent, SnapshotTrigger.PreRestore).ConfigureAwait(false);

        var checkoutResult = await _git.RunAsync(_snapshotsDir, ["checkout", snap.Hash, "--", "."], CancellationToken.None).ConfigureAwait(false);
        if (!checkoutResult.Ok)
        {
            _logger.LogWarning("snapshot restore checkout failed: {Error}", checkoutResult.Stderr);
            return null;
        }

        var content = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(_snapshotsDir, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_snapshotsDir, file).Replace('\\', '/');
            content[rel] = await File.ReadAllTextAsync(file).ConfigureAwait(false);
        }
        return content;
    }
    public async Task<IReadOnlyList<SnapshotDiff>?> InspectAsync(string snapshotId)
    {
        var list = await ListAsync().ConfigureAwait(false);
        var snap = list.FirstOrDefault(s => s.Id == snapshotId);
        if (snap is null)
        {
            _logger.LogWarning("snapshots: inspect — snapshot {id} not found", snapshotId);
            return null;
        }

        var snapshotFiles = new HashSet<string>(StringComparer.Ordinal);
        var treeResult = await _git.RunAsync(_snapshotsDir, ["ls-tree", "-r", "--name-only", snap.Hash], CancellationToken.None).ConfigureAwait(false);
        if (treeResult.Ok)
        {
            foreach (var line in treeResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                snapshotFiles.Add(line.Trim());
        }

        var currentFiles = new HashSet<string>(StringComparer.Ordinal);
        if (Directory.Exists(_snapshotsDir))
        {
            foreach (var file in Directory.EnumerateFiles(_snapshotsDir, "*.json", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(_snapshotsDir, file).Replace('\\', '/');
                currentFiles.Add(rel);
            }
        }

        var allKeys = snapshotFiles.Union(currentFiles).OrderBy(k => k, StringComparer.Ordinal);
        var diffs = new List<SnapshotDiff>();
        foreach (var key in allKeys)
        {
            var inSnapshot = snapshotFiles.Contains(key);
            var inCurrent = currentFiles.Contains(key);

            if (inSnapshot && !inCurrent)
            {
                var snapContent = await ReadGitFileAsync(snap.Hash, key).ConfigureAwait(false);
                diffs.Add(new SnapshotDiff(key, snapContent, null, "removed"));
            }
            else if (!inSnapshot && inCurrent)
            {
                var curContent = await File.ReadAllTextAsync(Path.Combine(_snapshotsDir, key)).ConfigureAwait(false);
                diffs.Add(new SnapshotDiff(key, null, curContent, "added"));
            }
            else
            {
                var snapContent = await ReadGitFileAsync(snap.Hash, key).ConfigureAwait(false);
                var curContent = await File.ReadAllTextAsync(Path.Combine(_snapshotsDir, key)).ConfigureAwait(false);
                if (!string.Equals(snapContent, curContent, StringComparison.Ordinal))
                    diffs.Add(new SnapshotDiff(key, snapContent, curContent, "changed"));
            }
        }
        return diffs;
    }

    private async Task<string?> ReadGitFileAsync(string commitHash, string relativePath)
    {
        var result = await _git.RunAsync(_snapshotsDir, ["show", $"{commitHash}:{relativePath}"], CancellationToken.None).ConfigureAwait(false);
        if (!result.Ok)
        {
            _logger.LogWarning("snapshots: git show {hash}:{path} failed: {err}", commitHash, relativePath, result.Stderr);
            return null;
        }
        return result.Stdout;
    }
    public async Task PruneAsync()
    {
        await _git.RunAsync(_snapshotsDir, ["gc", "--prune=now"], CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Snapshot>> ListRetainedAsync()
    {
        var all = await ListAsync().ConfigureAwait(false);
        var keep = await ComputeRetainedIdsAsync(all).ConfigureAwait(false);
        return all.Where(s => keep.Contains(s.Id)).ToList();
    }

    private async Task<HashSet<string>> ComputeRetainedIdsAsync(IReadOnlyList<Snapshot> all)
    {
        var now = DateTimeOffset.UtcNow;
        var keep = new HashSet<string>(StringComparer.Ordinal);

        foreach (var s in all.Where(x => x.TakenAtUtc > now.AddHours(-24)).Take(24))
            keep.Add(s.Id);

        var older = all.Where(x => x.TakenAtUtc <= now.AddHours(-24)).ToList();
        var byDay = older.GroupBy(x => x.TakenAtUtc.Date).OrderByDescending(g => g.Key).Take(6);
        foreach (var day in byDay)
            foreach (var s in day.Take(1))
                keep.Add(s.Id);

        foreach (var s in all)
        {
            if (keep.Contains(s.Id))
                continue;
            var tags = await _git.RunAsync(_snapshotsDir, ["tag", "-l", $"pin-{s.Id}"], CancellationToken.None).ConfigureAwait(false);
            if (tags.Ok && !string.IsNullOrWhiteSpace(tags.Stdout))
                keep.Add(s.Id);
        }

        return keep;
    }

    public async Task PinAsync(string snapshotId)
    {
        var list = await ListAsync().ConfigureAwait(false);
        var snap = list.FirstOrDefault(s => s.Id == snapshotId);
        if (snap is null)
            return;
        await _git.RunAsync(_snapshotsDir, ["tag", $"pin-{snapshotId}", snap.Hash], CancellationToken.None).ConfigureAwait(false);
    }

    private async Task EnsureRepoAsync(string dir)
    {
        Directory.CreateDirectory(dir);
        if (!Directory.Exists(Path.Combine(dir, ".git")))
        {
            await _git.RunAsync(dir, ["init"], CancellationToken.None).ConfigureAwait(false);
            try { await _git.RunAsync(dir, ["config", "user.email", "cove@cove.local"], CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await _git.RunAsync(dir, ["config", "user.name", "Cove"], CancellationToken.None).ConfigureAwait(false); } catch { }
        }
    }

    private static string ComputeHash(IReadOnlyDictionary<string, string> content)
    {
        var ordered = content.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        using var sha = SHA256.Create();
        using var ms = new MemoryStream();
        foreach (var kv in ordered)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(kv.Key);
            var valBytes = System.Text.Encoding.UTF8.GetBytes(kv.Value);
            WriteLengthPrefixed(ms, keyBytes);
            WriteLengthPrefixed(ms, valBytes);
        }
        ms.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(ms)).ToLowerInvariant();
    }

    private static void WriteLengthPrefixed(MemoryStream ms, byte[] bytes)
    {
        var len = bytes.Length;
        ms.WriteByte((byte)(len & 0xFF));
        ms.WriteByte((byte)((len >> 8) & 0xFF));
        ms.WriteByte((byte)((len >> 16) & 0xFF));
        ms.WriteByte((byte)((len >> 24) & 0xFF));
        ms.Write(bytes, 0, bytes.Length);
    }
}
