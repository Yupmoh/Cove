using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Search;

public sealed record SearchParams(string Query, string? Path, bool? UseRegex, bool? WholeWord, bool? CaseInsensitive, string? IncludeGlob, string? ExcludeGlob);
public sealed record SearchMatch(string FilePath, int Line, int Column, string Text, string? ContextBefore, string? ContextAfter);
public sealed record SearchResult(string Query, IReadOnlyList<SearchMatch> Matches, bool UseRegex, bool WholeWord, bool CaseInsensitive);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SearchResult))]
public sealed partial class SearchJsonContext : JsonSerializerContext { }

public sealed class SearchService
{
    private readonly ILogger _logger;
    private readonly string _rgPath;

    public SearchService(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _rgPath = FindRipgrep();
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_rgPath);

    private static string FindRipgrep()
    {
        var paths = new[] { "/opt/homebrew/bin/rg", "/usr/local/bin/rg", "/usr/bin/rg" };
        foreach (var p in paths)
            if (File.Exists(p)) return p;

        var envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in envPath.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir.Trim(), "rg");
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }

    public async Task<SearchResult> SearchAsync(SearchParams parameters, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Query))
            return new SearchResult(parameters.Query, [], false, false, true);

        if (!IsAvailable)
        {
            _logger.LogWarning("search: ripgrep not found");
            return new SearchResult(parameters.Query, [], parameters.UseRegex ?? false, parameters.WholeWord ?? false, parameters.CaseInsensitive ?? true);
        }

        var searchPath = string.IsNullOrEmpty(parameters.Path) ? "." : parameters.Path;
        var args = BuildArgs(parameters, searchPath);

        var psi = new ProcessStartInfo(_rgPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = searchPath,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                _logger.LogWarning("search: failed to start ripgrep");
                return new SearchResult(parameters.Query, [], parameters.UseRegex ?? false, parameters.WholeWord ?? false, parameters.CaseInsensitive ?? true);
            }

            var matches = new List<SearchMatch>();
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(line)) continue;
                var match = ParseRipgrepLine(line);
                if (match is not null) matches.Add(match);
            }
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return new SearchResult(
                parameters.Query,
                matches,
                parameters.UseRegex ?? false,
                parameters.WholeWord ?? false,
                parameters.CaseInsensitive ?? true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "search: ripgrep execution failed");
            return new SearchResult(parameters.Query, [], parameters.UseRegex ?? false, parameters.WholeWord ?? false, parameters.CaseInsensitive ?? true);
        }
    }

    private static List<string> BuildArgs(SearchParams p, string searchPath)
    {
        var args = new List<string> { "--json", "--line-number", "--with-filename" };

        if (p.CaseInsensitive != false) args.Add("-i");
        if (p.WholeWord == true) args.Add("-w");
        if (p.UseRegex != true) args.Add("--fixed-strings");

        if (!string.IsNullOrEmpty(p.IncludeGlob))
        {
            args.Add("-g");
            args.Add(p.IncludeGlob);
        }
        if (!string.IsNullOrEmpty(p.ExcludeGlob))
        {
            args.Add("-g");
            args.Add($"!{p.ExcludeGlob}");
        }

        args.Add("--");
        args.Add(p.Query);
        return args;
    }

    private static SearchMatch? ParseRipgrepLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "match")
            {
                var data = root.GetProperty("data");
                var filePath = data.GetProperty("path").GetProperty("text").GetString() ?? "";
                var lineNum = data.GetProperty("line_number").GetInt32();
                var lineText = data.GetProperty("lines").GetProperty("text").GetString() ?? "";
                var col = 1;
                if (data.TryGetProperty("submatches", out var submatches) && submatches.GetArrayLength() > 0)
                {
                    var first = submatches[0];
                    if (first.TryGetProperty("start", out var start)) col = start.GetInt32() + 1;
                }
                return new SearchMatch(filePath, lineNum, col, lineText.TrimEnd(), null, null);
            }
        }
        catch (JsonException) { }
        return null;
    }
}
