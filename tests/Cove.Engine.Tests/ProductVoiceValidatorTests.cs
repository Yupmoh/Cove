using Cove.Engine.Ux;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ProductVoiceValidatorTests
{
    [Fact]
    public void Validate_QuietMessage_Passes()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("saving your work");
        Assert.True(result.Valid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Validate_ExclamationMark_Fails()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("done!");
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Contains("exclamation"));
    }

    [Fact]
    public void Validate_AllCaps_Fails()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("ERROR OCCURRED");
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Contains("all-caps"));
    }

    [Fact]
    public void Validate_Emoji_Fails()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("all done 🎉");
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Contains("emoji"));
    }

    [Fact]
    public void Validate_GamificationWord_Fails()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("congratulations on your achievement");
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Contains("gamification"));
    }

    [Fact]
    public void Validate_NagWord_Fails()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("you must complete this step");
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Contains("nagging"));
    }

    [Fact]
    public void Validate_EmptyMessage_Passes()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("");
        Assert.True(result.Valid);
    }

    [Fact]
    public void Validate_WhitespaceOnly_Passes()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("   ");
        Assert.True(result.Valid);
    }

    [Fact]
    public void Validate_LeadingWhitespace_Fails()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate(" saving work");
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Contains("whitespace"));
    }

    [Fact]
    public void Sanitize_RemovesExclamationMarks()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Sanitize("done!");
        Assert.Equal("done.", result);
    }

    [Fact]
    public void Sanitize_RemovesEmoji()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Sanitize("all done 🎉");
        Assert.Equal("all done", result);
    }

    [Fact]
    public void Sanitize_TrimsWhitespace()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Sanitize("  hello  ");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Sanitize_LowercasesAllCaps()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Sanitize("ERROR OCCURRED");
        Assert.Equal("error occurred", result);
    }

    [Fact]
    public void Validate_MultipleViolations_ReportsAll()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("CONGRATULATIONS! 🎉");
        Assert.False(result.Valid);
        Assert.True(result.Violations.Count >= 3);
    }

    [Fact]
    public void Validate_LowercaseMessage_Passes()
    {
        var validator = new ProductVoiceValidator(NullLogger.Instance);
        var result = validator.Validate("cove is ready");
        Assert.True(result.Valid);
    }
}
