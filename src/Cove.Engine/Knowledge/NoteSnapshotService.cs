using System.Diagnostics;
using Cove.Protocol;
using Cove.Platform;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Knowledge;

public sealed class NoteSnapshotService
{
    private readonly string _notesRoot;
    private readonly ILogger _logger;
    private bool _initialized;

    public NoteSnapshotService(string dataDir, ILogger logger)
    {
        _notesRoot = System.IO.Path.Combine(dataDir, "notes");
        _logger = logger;
    }

    public void EnsureRepo()
    {
        if (_initialized) return;
        System.IO.Directory.CreateDirectory(_notesRoot);

        var gitDir = System.IO.Path.Combine(_notesRoot, ".git");
        if (!System.IO.Directory.Exists(gitDir))
        {
            RunGit(["init", "--quiet"]);
            RunGit(["config", "user.email", "cove@cove.local"]);
            RunGit(["config", "user.name", "Cove Notes"]);
        }
        _initialized = true;
    }

    public void Snapshot(string bayId, string noteId, string message)
    {
        if (!AreSafeIdentifiers(bayId, noteId))
            return;
        EnsureRepo();
        var notePath = System.IO.Path.Combine(bayId, noteId);
        RunGit(["add", "--", notePath]);
        var result = RunGit(["commit", "--quiet", "-m", message]);
        if (!result.Ok)
            _logger.LogWarning("notes-snapshot: commit failed for {ws}/{id}: {err}", bayId, noteId, result.Stderr);
        else
            _logger.LogWarning("notes-snapshot: committed {ws}/{id}: {msg}", bayId, noteId, message);
    }

    public System.Collections.Generic.IReadOnlyList<NoteHistoryEntry> GetHistory(string bayId, string noteId)
    {
        if (!AreSafeIdentifiers(bayId, noteId))
            return [];
        EnsureRepo();
        var notePath = System.IO.Path.Combine(bayId, noteId);
        var result = RunGit(["log", "--format=%H%x1f%s%x1f%aI", "--", notePath]);
        if (!result.Ok)
        {
            _logger.LogWarning("notes-snapshot: git log failed for {ws}/{id}: {err}", bayId, noteId, result.Stderr);
            return [];
        }

        var entries = new System.Collections.Generic.List<NoteHistoryEntry>();
        var lines = result.Stdout.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\x1f');
            if (parts.Length < 3) continue;
            entries.Add(new NoteHistoryEntry(
                parts[0],
                parts[1],
                System.DateTimeOffset.Parse(parts[2], null, System.Globalization.DateTimeStyles.RoundtripKind)
            ));
        }
        return entries;
    }

    private bool AreSafeIdentifiers(string bayId, string noteId)
    {
        if (PathContainment.IsSafeSegment(bayId) && PathContainment.IsSafeSegment(noteId))
            return true;
        _logger.NoteSnapshotUnsafeIdentifiers(bayId, noteId);
        return false;
    }

    private GitResult RunGit(System.Collections.Generic.IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = _notesRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, stdout, stderr);
    }

    private sealed record GitResult(int ExitCode, string Stdout, string Stderr)
    {
        public bool Ok => ExitCode == 0;
    }
}

internal static partial class NoteSnapshotLog
{
    [ZLoggerMessage(LogLevel.Warning, "notes snapshot rejected unsafe identifiers bayId={bayId} noteId={noteId}")]
    public static partial void NoteSnapshotUnsafeIdentifiers(this ILogger logger, string bayId, string noteId);
}
