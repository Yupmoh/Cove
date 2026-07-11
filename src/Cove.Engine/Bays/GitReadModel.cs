using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Bays;

public sealed record GitStatusEntry(string FilePath, string StatusCode, bool IsStaged, bool IsUntracked, string? OldFilePath = null);
public sealed record GitStatus(IReadOnlyList<GitStatusEntry> Entries);
public sealed record GitDiffFile(string Path, int Additions, int Deletions, string? Patch);
public sealed record GitDiff(IReadOnlyList<GitDiffFile> Files);
public sealed record GitBlameLine(string Commit, int LineNumber, string Author, string? Content);
public sealed record GitBlame(IReadOnlyList<GitBlameLine> Lines);
public sealed record GitLogCommit(string Sha, string Author, string Message, System.DateTimeOffset Date);
public sealed record GitLog(IReadOnlyList<GitLogCommit> Commits);

public sealed class GitReadModel
{
    private readonly IGitRunner _git;
    private readonly ILogger _logger;

    public GitReadModel(IGitRunner git, ILogger logger)
    {
        _git = git;
        _logger = logger;
    }

    public async Task<GitStatus> GetStatusAsync(string repoDir, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["status", "--porcelain=v2", "-z"], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("git status failed: {err}", result.Stderr);
            return new GitStatus([]);
        }

        var entries = new System.Collections.Generic.List<GitStatusEntry>();
        var fields = result.Stdout.Split('\0');
        for (var i = 0; i < fields.Length; i++)
        {
            var line = fields[i];
            if (line.StartsWith("1 "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 9) continue;
                var statusCode = parts[1];
                var filePath = parts[^1];
                var isStaged = !statusCode.StartsWith(".");
                entries.Add(new GitStatusEntry(filePath, statusCode[..1], isStaged, false));
            }
            else if (line.StartsWith("2 "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10) continue;
                var statusCode = parts[1];
                var newFilePath = parts[^1];
                var oldFilePath = i + 1 < fields.Length ? fields[i + 1] : "";
                var isStaged = !statusCode.StartsWith(".");
                entries.Add(new GitStatusEntry(newFilePath, "R", isStaged, false, oldFilePath));
            }
            else if (line.StartsWith("u "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10) continue;
                var filePath = parts[^1];
                entries.Add(new GitStatusEntry(filePath, "U", true, false));
            }
            else if (line.StartsWith("? "))
            {
                var filePath = line[2..];
                entries.Add(new GitStatusEntry(filePath, "?", false, true));
            }
        }
        return new GitStatus(entries);
    }

    public async Task<GitDiff> GetDiffAsync(string repoDir, string? refSpec, CancellationToken ct = default)
    {
        var args = new System.Collections.Generic.List<string> { "diff", "--numstat", "-z" };
        if (refSpec is not null)
            args.Add(refSpec);
        var result = await _git.RunAsync(repoDir, [.. args], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("git diff failed: {err}", result.Stderr);
            return new GitDiff([]);
        }
        var files = new System.Collections.Generic.List<GitDiffFile>();
        var fields = result.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in fields)
        {
            var parts = entry.Split('\t');
            if (parts.Length < 3) continue;
            var adds = int.TryParse(parts[0], out var a) ? a : 0;
            var dels = int.TryParse(parts[1], out var d) ? d : 0;
            var path = parts[2];
            files.Add(new GitDiffFile(path, adds, dels, null));
        }
        return new GitDiff(files);
    }

    public async Task<GitDiffFile> GetFileDiffAsync(string repoDir, string filePath, string? refSpec = null, CancellationToken ct = default)
    {
        var args = new System.Collections.Generic.List<string> { "diff", "--no-color", "-U3" };
        if (refSpec is not null)
            args.Add(refSpec);
        args.Add("--");
        args.Add(filePath);
        var result = await _git.RunAsync(repoDir, [.. args], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("git diff for {file} failed: {err}", filePath, result.Stderr);
            return new GitDiffFile(filePath, 0, 0, null);
        }
        var patch = result.Stdout;
        var adds = 0;
        var dels = 0;
        foreach (var line in patch.Split('\n'))
        {
            if (line.StartsWith("+") && !line.StartsWith("+++")) adds++;
            else if (line.StartsWith("-") && !line.StartsWith("---")) dels++;
        }
        return new GitDiffFile(filePath, adds, dels, patch);
    }

    public async Task<GitBlame> GetBlameAsync(string repoDir, string filePath, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["blame", "--porcelain", filePath], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("git blame failed: {err}", result.Stderr);
            return new GitBlame([]);
        }

        var lines = new System.Collections.Generic.List<GitBlameLine>();
        var currentCommit = "";
        var currentAuthor = "";
        var lineNumber = 0;
        var outputLines = result.Stdout.Split('\n');
        foreach (var line in outputLines)
        {
            if (line.StartsWith("author "))
                currentAuthor = line[7..];
            else if (line.StartsWith("\t"))
            {
                lineNumber++;
                lines.Add(new GitBlameLine(currentCommit, lineNumber, currentAuthor, line[1..]));
            }
            else if (line.Length > 0 && IsHex(line[0]) && line.Contains(' '))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 2)
                    currentCommit = parts[0];
            }
        }
        return new GitBlame(lines);
    }

    public async Task<GitLog> GetLogAsync(string repoDir, int limit = 50, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["log", $"--max-count={limit}", "--format=%H%n%an%n%ad%n%s%n---", "--date=iso"], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("git log failed: {err}", result.Stderr);
            return new GitLog([]);
        }
        return ParseLog(result.Stdout);
    }
    public async Task<GitLog> GetUnpushedAsync(string repoDir, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["log", "@{u}..HEAD", "--format=%H%n%an%n%ad%n%s%n---", "--date=iso"], ct);
        if (!result.Ok)
        {
            if (!result.Stderr.Contains("no upstream", System.StringComparison.OrdinalIgnoreCase))
                _logger.LogWarning("git log unpushed failed: {err}", result.Stderr);
            return new GitLog([]);
        }
        return ParseLog(result.Stdout);
    }

    public async Task<GitLog> GetUnpulledAsync(string repoDir, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["log", "HEAD..@{u}", "--format=%H%n%an%n%ad%n%s%n---", "--date=iso"], ct);
        if (!result.Ok)
        {
            if (!result.Stderr.Contains("no upstream", System.StringComparison.OrdinalIgnoreCase))
                _logger.LogWarning("git log unpulled failed: {err}", result.Stderr);
            return new GitLog([]);
        }
        return ParseLog(result.Stdout);
    }

    private static GitLog ParseLog(string output)
    {
        var commits = new System.Collections.Generic.List<GitLogCommit>();
        var blocks = output.Split("\n---\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 4) continue;
            var sha = lines[0];
            var author = lines[1];
            var date = System.DateTimeOffset.Parse(lines[2].Trim());
            var message = lines[3];
            commits.Add(new GitLogCommit(sha, author, message, date));
        }
        return new GitLog(commits);
    }

    public async Task StageAsync(string repoDir, string filePath, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["add", filePath], ct);
        if (!result.Ok)
            _logger.LogWarning("git add failed: {err}", result.Stderr);
    }

    public async Task UnstageAsync(string repoDir, string filePath, CancellationToken ct = default)
    {
        var result = await _git.RunAsync(repoDir, ["restore", "--staged", filePath], ct);
        if (!result.Ok)
            _logger.LogWarning("git restore --staged failed: {err}", result.Stderr);
    }

    public async Task<bool> CommitAsync(string repoDir, string message, bool amend = false, bool sign = false, CancellationToken ct = default)
    {
        var args = new System.Collections.Generic.List<string> { "commit", "-q", "-m", message };
        if (amend) args.Add("--amend");
        if (sign) args.Add("-S");
        var result = await _git.RunAsync(repoDir, [.. args], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("git commit failed: {err}", result.Stderr);
            return false;
        }
        return true;
    }

    private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
