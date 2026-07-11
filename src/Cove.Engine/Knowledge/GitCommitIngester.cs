using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class GitCommitIngester
{
    private readonly TimelineStore _timeline;
    private readonly Bays.IGitRunner _git;
    private readonly ILogger _logger;

    public GitCommitIngester(TimelineStore timeline, Bays.IGitRunner git, ILogger logger)
    {
        _timeline = timeline;
        _git = git;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<int> IngestAsync(string repoDir, string bayId, System.Threading.CancellationToken ct = default)
    {
        if (!System.IO.Directory.Exists(System.IO.Path.Combine(repoDir, ".git")))
        {
            _logger.LogWarning("timeline-ingester: {dir} is not a git repository, skipping", repoDir);
            return 0;
        }

        var result = await _git.RunAsync(repoDir, ["log", "--format=%H%x1f%s%x1f%aI", "--no-merges", "-n", "200"], ct);
        if (!result.Ok)
        {
            _logger.LogWarning("timeline-ingester: git log failed in {dir}: {err}", repoDir, result.Stderr);
            return 0;
        }

        int count = 0;
        var lines = result.Stdout.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\x1f');
            if (parts.Length < 3) continue;
            var sha = parts[0];
            var subject = parts[1];
            var authorDate = System.DateTimeOffset.Parse(parts[2], null, System.Globalization.DateTimeStyles.RoundtripKind);

            _timeline.Append(new TimelineEntry
            {
                BayId = bayId,
                Kind = "git.commit",
                Source = "git-ingester",
                Scope = "bay",
                Summary = subject,
                JsonPayload = "{\"sha\":\"" + sha + "\"}",
                Id = "git-" + sha[..12],
                Timestamp = authorDate,
            });
            count++;
        }

        _logger.LogWarning("timeline-ingester: processed {count} commits from {dir}", count, repoDir);
        return count;
    }
}
