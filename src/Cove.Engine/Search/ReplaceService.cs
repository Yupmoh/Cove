using Microsoft.Extensions.Logging;

namespace Cove.Engine.Search;

public sealed record ReplaceResult(string FilePath, int Replacements, bool Saved);

public sealed class ReplaceService
{
    private readonly ILogger _logger;

    public ReplaceService(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public IReadOnlyList<ReplaceResult> ReplaceInFiles(string search, string replacement, IReadOnlyList<string> filePaths, bool useRegex = false, bool caseInsensitive = true, bool wholeWord = false)
    {
        if (string.IsNullOrEmpty(search))
        {
            _logger.LogWarning("replace: search term required");
            return [];
        }

        var results = new System.Collections.Generic.List<ReplaceResult>();
        var options = caseInsensitive ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None;
        var pattern = useRegex ? search : System.Text.RegularExpressions.Regex.Escape(search);
        if (wholeWord) pattern = $@"\b{pattern}\b";

        System.Text.RegularExpressions.Regex regex;
        try
        {
            regex = new System.Text.RegularExpressions.Regex(pattern, options, System.TimeSpan.FromSeconds(5));
        }
        catch (System.Text.RegularExpressions.RegexParseException ex)
        {
            _logger.LogWarning(ex, "replace: invalid regex pattern {pattern}", pattern);
            return [];
        }

        foreach (var filePath in filePaths)
        {
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("replace: file not found {path}", filePath);
                results.Add(new ReplaceResult(filePath, 0, false));
                continue;
            }

            try
            {
                var content = System.IO.File.ReadAllText(filePath);
                var newContent = regex.Replace(content, replacement);
                var count = regex.Matches(content).Count;

                if (count > 0)
                {
                    var tmp = filePath + ".tmp";
                    System.IO.File.WriteAllText(tmp, newContent);
                    System.IO.File.Move(tmp, filePath, true);
                    _logger.LogInformation("replace: {count} replacements in {path}", count, filePath);
                    results.Add(new ReplaceResult(filePath, count, true));
                }
                else
                {
                    results.Add(new ReplaceResult(filePath, 0, false));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "replace: failed to process {path}", filePath);
                results.Add(new ReplaceResult(filePath, 0, false));
            }
        }

        return results;
    }

    public ReplaceResult PreviewReplace(string search, string replacement, string filePath, bool useRegex = false, bool caseInsensitive = true, bool wholeWord = false)
    {
        if (!System.IO.File.Exists(filePath))
            return new ReplaceResult(filePath, 0, false);

        var content = System.IO.File.ReadAllText(filePath);
        var options = caseInsensitive ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None;
        var pattern = useRegex ? search : System.Text.RegularExpressions.Regex.Escape(search);
        if (wholeWord) pattern = $@"\b{pattern}\b";

        try
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern, options, System.TimeSpan.FromSeconds(5));
            var count = regex.Matches(content).Count;
            return new ReplaceResult(filePath, count, false);
        }
        catch (System.Text.RegularExpressions.RegexParseException)
        {
            return new ReplaceResult(filePath, 0, false);
        }
    }
}
