using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Engine.Bays;

public sealed class GitSummaryService
{
    private static readonly string[] StatusArguments = ["status", "--porcelain=v2", "--branch"];
    private readonly IPlatformFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;

    public GitSummaryService(IPlatformFileSystem fileSystem, IProcessRunner processRunner, ILogger? logger = null)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<GitSummaryResult> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.DirectoryExists(path))
        {
            _logger.GitSummaryRejected(path, "not_found");
            return GitSummaryParser.Fail("not_found");
        }

        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync(
                new ProcessRunRequest(
                    "git",
                    path,
                    StatusArguments,
                    null,
                    TimeSpan.FromSeconds(4)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.GitSummaryRejected(path, exception.Message);
            return GitSummaryParser.Fail("git_unavailable");
        }

        if (result.TimedOut)
        {
            _logger.GitSummaryRejected(path, "timeout");
            return GitSummaryParser.Fail("timeout");
        }
        if (!result.Started)
        {
            _logger.GitSummaryRejected(path, "git_unavailable");
            return GitSummaryParser.Fail("git_unavailable");
        }
        if (result.ExitCode != 0)
        {
            _logger.GitSummaryRejected(path, "not_a_repo");
            return GitSummaryParser.Fail("not_a_repo");
        }

        return GitSummaryParser.Parse(result.Stdout);
    }
}

public static class GitSummaryParser
{
    public static GitSummaryResult Parse(string output)
    {
        var branch = "";
        var ahead = 0;
        var behind = 0;
        var dirty = 0;
        var files = new List<GitSummaryFileDto>();
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = line["# branch.head ".Length..].Trim();
            }
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                foreach (var part in line["# branch.ab ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.Length < 2 || !int.TryParse(part[1..], out var value))
                        continue;
                    if (part[0] == '+')
                        ahead = value;
                    else if (part[0] == '-')
                        behind = value;
                }
            }
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                dirty++;
                var file = ParseFile(line);
                if (file is not null)
                    files.Add(file);
            }
        }

        return new GitSummaryResult(true, branch, ahead, behind, dirty, files, null);
    }

    public static GitSummaryResult Fail(string error)
        => new(false, "", 0, 0, 0, [], error);

    private static GitSummaryFileDto? ParseFile(string line)
    {
        if (line.StartsWith("? ", StringComparison.Ordinal))
            return new GitSummaryFileDto(line[2..], "A");
        if (line.StartsWith("1 ", StringComparison.Ordinal))
        {
            var fields = line.Split(' ', 9, StringSplitOptions.None);
            return fields.Length == 9
                ? new GitSummaryFileDto(fields[8], NormalizeStatus(fields[1]))
                : null;
        }
        if (line.StartsWith("2 ", StringComparison.Ordinal))
        {
            var fields = line.Split(' ', 10, StringSplitOptions.None);
            return fields.Length == 10
                ? new GitSummaryFileDto(fields[9].Split('\t')[0], NormalizeStatus(fields[1]))
                : null;
        }
        return null;
    }

    private static string NormalizeStatus(string status)
    {
        if (status.Contains('D'))
            return "D";
        if (status.Contains('A') || status.Contains('?'))
            return "A";
        return "M";
    }
}
