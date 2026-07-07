using System.Text.Json;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookSubcommandValidatorTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Validate_InstallOutput_ReturnsValid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"install","installed":"installed"}"""));
        Assert.True(result.Valid);
        Assert.Equal("installed", result.Status);
        Assert.Equal("install", result.Subcommand);
    }

    [Fact]
    public void Validate_UninstallOutput_ReturnsValid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"uninstall","installed":"uninstalled"}"""));
        Assert.True(result.Valid);
        Assert.Equal("uninstalled", result.Status);
    }

    [Fact]
    public void Validate_StatusOutput_ReturnsValid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"status","installed":"installed"}"""));
        Assert.True(result.Valid);
        Assert.Equal("status", result.Subcommand);
    }

    [Fact]
    public void Validate_StatusWithActivityHooks_Tolerated()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"status","installed":"installed","activityHooks":true}"""));
        Assert.True(result.Valid);
        Assert.True(result.ActivityHooks);
    }

    [Fact]
    public void Validate_WithReason_Extracted()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"install","installed":"uninstalled","reason":"not found"}"""));
        Assert.True(result.Valid);
        Assert.Equal("not found", result.Reason);
    }

    [Fact]
    public void Validate_UnknownSubcommand_ReturnsInvalid()
    {
        var result = HookSubcommandValidator.ValidateUnknown("bogus");
        Assert.False(result.Valid);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void ValidateOutput_MissingSubcommand_ReturnsInvalid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"installed":"installed"}"""));
        Assert.False(result.Valid);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void ValidateOutput_MissingStatus_ReturnsInvalid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"install"}"""));
        Assert.False(result.Valid);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void ValidateOutput_InvalidStatus_ReturnsInvalid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""{"subcommand":"install","installed":"bogus"}"""));
        Assert.False(result.Valid);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void ValidateOutput_NonObject_ReturnsInvalid()
    {
        var result = HookSubcommandValidator.ValidateOutput(Parse("""["install"]"""));
        Assert.False(result.Valid);
        Assert.Equal(2, result.ExitCode);
    }
}
