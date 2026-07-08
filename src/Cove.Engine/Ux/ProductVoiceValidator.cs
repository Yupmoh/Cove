using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Ux;

public sealed record VoiceValidationResult(bool Valid, IReadOnlyList<string> Violations);

public sealed class ProductVoiceValidator
{
    private static readonly Regex ExclamationPattern = new(@"!+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex AllCapsPattern = new(@"\b[A-Z]{4,}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex EmojiPattern = new(@"[\u2600-\u27BF]|[\uD83C-\uDBFF][\uDC00-\uDFFF]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly string[] GamificationWords =
    [
        "congratulations", "amazing", "awesome", "spectacular", "incredible",
        "streak", "achievement", "unlocked", "level up", "badge",
        "celebrate", "celebration", "confetti", "reward", "bonus",
        "epic", "legendary", "mind-blowing", "game-changer"
    ];

    private static readonly string[] NagWords =
    [
        "must", "required", "mandatory", "need to", "have to",
        "don't forget", "make sure", "remember to", "you should"
    ];

    private readonly ILogger _logger;

    public ProductVoiceValidator(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public VoiceValidationResult Validate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new VoiceValidationResult(true, []);

        var violations = new System.Collections.Generic.List<string>();
        var lower = message.ToLowerInvariant();

        if (ExclamationPattern.IsMatch(message))
            violations.Add("no exclamation marks");

        if (AllCapsPattern.IsMatch(message))
            violations.Add("no all-caps shouting");

        if (EmojiPattern.IsMatch(message))
            violations.Add("no emoji");

        foreach (var word in GamificationWords)
        {
            if (lower.Contains(word))
            {
                violations.Add($"no gamification language: '{word}'");
                break;
            }
        }

        foreach (var word in NagWords)
        {
            if (lower.Contains(word))
            {
                violations.Add($"no nagging language: '{word}'");
                break;
            }
        }

        if (message.StartsWith(" ") || message.EndsWith("  "))
            violations.Add("no leading/trailing whitespace");

        if (violations.Count > 0)
            _logger.LogWarning("voice: message '{msg}' has {count} violations: {violations}", message, violations.Count, string.Join(", ", violations));

        return new VoiceValidationResult(violations.Count == 0, violations);
    }

    public string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var result = message.Trim();
        result = ExclamationPattern.Replace(result, ".");
        result = AllCapsPattern.Replace(result, m => m.Value.ToLowerInvariant());
        result = EmojiPattern.Replace(result, "").Trim();
        foreach (var word in GamificationWords)
        {
            var pattern = new Regex(Regex.Escape(word), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            result = pattern.Replace(result, word);
        }

        return result;
    }
}
