using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ContextInjectionTests
{
    private static HookEnvelopeMatrix MatrixWith(params (string adapter, string Event, HookEnvelopeKind kind)[] entries)
    {
        var matrix = new HookEnvelopeMatrix();
        foreach (var (adapter, evt, kind) in entries)
            matrix.Register(adapter, evt, kind);
        return matrix;
    }

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Render_NoneKind_ReturnsEmpty_WhenAdapterHasNoCapability()
    {
        var matrix = MatrixWith(("claude-code", "userPromptSubmit", HookEnvelopeKind.None));
        var injector = new ContextInjector(matrix);

        var result = injector.Render("claude-code", "postToolUse", Payload("""{"tool":"bash"}"""));

        Assert.Equal("{}", result);
    }

    [Fact]
    public void Render_FailOpen_MalformedPayload_DoesNotThrow()
    {
        var matrix = MatrixWith(("claude-code", "userPromptSubmit", HookEnvelopeKind.HookSpecificOutput));
        var injector = new ContextInjector(matrix);
        var empty = JsonDocument.Parse("{}").RootElement.Clone();

        var ex = Record.Exception(() => injector.Render("claude-code", "userPromptSubmit", empty));

        Assert.Null(ex);
    }

    [Fact]
    public void Render_AwarenessOff_YieldsExplicitSigilsOnly()
    {
        var matrix = MatrixWith(("claude-code", "sessionStartManifest", HookEnvelopeKind.FlatAdditionalContext));
        var injector = new ContextInjector(matrix, AwarenessLevel.Off);

        var result = injector.Render("claude-code", "sessionStartManifest", Payload("""{"context":"ambient data"}"""));

        Assert.Equal("{}", result);
    }

    [Fact]
    public void Render_AwarenessMinimal_IncludesSessionStartButNotAmbient()
    {
        var matrix = MatrixWith(
            ("claude-code", "sessionStartManifest", HookEnvelopeKind.FlatAdditionalContext),
            ("claude-code", "preToolUse", HookEnvelopeKind.FlatAdditionalContext));
        var injector = new ContextInjector(matrix, AwarenessLevel.Minimal);

        var sessionResult = injector.Render("claude-code", "sessionStartManifest", Payload("""{"context":"manifest"}"""));
        var ambientResult = injector.Render("claude-code", "preToolUse", Payload("""{"context":"ambient"}"""));

        Assert.NotEqual("{}", sessionResult);
        Assert.Equal("{}", ambientResult);
    }

    [Fact]
    public void Render_AwarenessFull_IncludesAllCapabilities()
    {
        var matrix = MatrixWith(
            ("claude-code", "sessionStartManifest", HookEnvelopeKind.FlatAdditionalContext),
            ("claude-code", "preToolUse", HookEnvelopeKind.FlatAdditionalContext));
        var injector = new ContextInjector(matrix, AwarenessLevel.Full);

        var sessionResult = injector.Render("claude-code", "sessionStartManifest", Payload("""{"context":"manifest"}"""));
        var ambientResult = injector.Render("claude-code", "preToolUse", Payload("""{"context":"ambient"}"""));

        Assert.NotEqual("{}", sessionResult);
        Assert.NotEqual("{}", ambientResult);
    }

    [Fact]
    public void ContextChip_TracksInjectedPieces()
    {
        var matrix = MatrixWith(
            ("claude-code", "sessionStartManifest", HookEnvelopeKind.FlatAdditionalContext),
            ("claude-code", "userPromptSubmit", HookEnvelopeKind.Identity));
        var injector = new ContextInjector(matrix, AwarenessLevel.Full);

        injector.Render("claude-code", "sessionStartManifest", Payload("""{"context":"manifest"}"""));
        injector.Render("claude-code", "userPromptSubmit", Payload("\"prompt\""));

        var chip = injector.GetContextChip("claude-code");
        Assert.Contains(chip, c => c.Source == "sessionStartManifest" && c.Injected);
        Assert.Contains(chip, c => c.Source == "userPromptSubmit" && c.Injected);
    }

    [Fact]
    public void ContextChip_NotInjected_WhenAwarenessOff()
    {
        var matrix = MatrixWith(("claude-code", "sessionStartManifest", HookEnvelopeKind.FlatAdditionalContext));
        var injector = new ContextInjector(matrix, AwarenessLevel.Off);

        injector.Render("claude-code", "sessionStartManifest", Payload("""{"context":"manifest"}"""));

        var chip = injector.GetContextChip("claude-code");
        Assert.Contains(chip, c => c.Source == "sessionStartManifest" && !c.Injected);
    }
}
