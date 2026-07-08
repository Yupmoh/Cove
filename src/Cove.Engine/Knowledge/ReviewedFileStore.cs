using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record ReviewedFile(string RepoRoot, string Scope, string FilePath, string ReviewedBy, System.DateTimeOffset ReviewedAt);

public sealed class ReviewedFileStore
{
    private readonly string _dataDir;
    private readonly ILogger _logger;

    public ReviewedFileStore(string dataDir, ILogger logger)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    public ReviewedFile MarkReviewed(string repoRoot, string scope, string filePath, string reviewedBy)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            _logger.LogWarning("reviewed-file: repoRoot required");
            throw new ArgumentException("repoRoot required", nameof(repoRoot));
        }
        if (string.IsNullOrWhiteSpace(scope))
        {
            _logger.LogWarning("reviewed-file: scope required");
            throw new ArgumentException("scope required", nameof(scope));
        }
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("reviewed-file: filePath required");
            throw new ArgumentException("filePath required", nameof(filePath));
        }

        var entry = new ReviewedFile(repoRoot, scope, filePath, reviewedBy, System.DateTimeOffset.UtcNow);
        var path = GetStorePath(repoRoot, scope);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var lines = new System.Collections.Generic.List<string>();
        if (System.IO.File.Exists(path))
        {
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 3 && parts[2] != filePath)
                    lines.Add(line);
            }
        }
        lines.Add($"{entry.RepoRoot}\t{entry.Scope}\t{entry.FilePath}\t{entry.ReviewedBy}\t{entry.ReviewedAt:o}");
        var tmp = path + ".tmp";
        System.IO.File.WriteAllLines(tmp, lines);
        System.IO.File.Move(tmp, path, true);
        _logger.LogInformation("reviewed-file: marked {file} reviewed in {repo}/{scope} by {by}", filePath, repoRoot, scope, reviewedBy);
        return entry;
    }

    public bool IsReviewed(string repoRoot, string scope, string filePath)
    {
        var path = GetStorePath(repoRoot, scope);
        if (!System.IO.File.Exists(path)) return false;
        foreach (var line in System.IO.File.ReadAllLines(path))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 3 && parts[2] == filePath)
                return true;
        }
        return false;
    }

    public System.Collections.Generic.IReadOnlyList<ReviewedFile> ListReviewed(string repoRoot, string scope)
    {
        var path = GetStorePath(repoRoot, scope);
        var result = new System.Collections.Generic.List<ReviewedFile>();
        if (!System.IO.File.Exists(path)) return result;

        foreach (var line in System.IO.File.ReadAllLines(path))
        {
            var parts = line.Split('\t');
            if (parts.Length < 5) continue;
            var reviewedAt = System.DateTimeOffset.TryParse(parts[4], out var dt) ? dt : System.DateTimeOffset.UtcNow;
            result.Add(new ReviewedFile(parts[0], parts[1], parts[2], parts[3], reviewedAt));
        }
        return result;
    }

    public bool UnmarkReviewed(string repoRoot, string scope, string filePath)
    {
        var path = GetStorePath(repoRoot, scope);
        if (!System.IO.File.Exists(path)) return false;

        var lines = new System.Collections.Generic.List<string>();
        var removed = false;
        foreach (var line in System.IO.File.ReadAllLines(path))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 3 && parts[2] == filePath)
            {
                removed = true;
                continue;
            }
            lines.Add(line);
        }

        if (removed)
        {
            var tmp = path + ".tmp";
            System.IO.File.WriteAllLines(tmp, lines);
            System.IO.File.Move(tmp, path, true);
            _logger.LogInformation("reviewed-file: unmarked {file} in {repo}/{scope}", filePath, repoRoot, scope);
        }
        return removed;
    }

    private string GetStorePath(string repoRoot, string scope)
    {
        var repoHash = System.IO.Path.GetFileName(repoRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        var safeScope = string.Concat(scope.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        return System.IO.Path.Combine(_dataDir, "reviewed", repoHash, $"{safeScope}.tsv");
    }
}
