using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookEnvelopeMatrixTests
{
    [Fact]
    public void Default_KindIsNone()
    {
        var matrix = new HookEnvelopeMatrix();
        var cap = matrix.GetCapability("claude-code", "sessionStartManifest");
        Assert.Equal(HookEnvelopeKind.None, cap.Kind);
    }

    [Fact]
    public void Register_SetsCapability()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "userPromptSubmit", HookEnvelopeKind.HookSpecificOutput, includeSystemMessage: true);
        var cap = matrix.GetCapability("claude-code", "userPromptSubmit");
        Assert.Equal(HookEnvelopeKind.HookSpecificOutput, cap.Kind);
        Assert.True(cap.IncludeSystemMessage);
    }

    [Fact]
    public void CanInject_None_ReturnsFalse()
    {
        var matrix = new HookEnvelopeMatrix();
        Assert.False(matrix.CanInject("claude-code", "postToolUse"));
    }

    [Fact]
    public void CanInject_Identity_ReturnsTrue()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
        Assert.True(matrix.CanInject("claude-code", "sessionStartManifest"));
    }

    [Fact]
    public void CanInject_FlatAdditionalContext_ReturnsTrue()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "preToolUse", HookEnvelopeKind.FlatAdditionalContext);
        Assert.True(matrix.CanInject("claude-code", "preToolUse"));
    }

    [Fact]
    public void CanInject_UnknownAdapter_ReturnsFalse()
    {
        var matrix = new HookEnvelopeMatrix();
        Assert.False(matrix.CanInject("unknown", "userPromptSubmit"));
    }

    [Fact]
    public void SupportedEvents_AreCanonical()
    {
        Assert.Contains("sessionStartManifest", HookEnvelopeMatrix.SupportedEvents);
        Assert.Contains("userPromptSubmit", HookEnvelopeMatrix.SupportedEvents);
        Assert.Contains("preToolUse", HookEnvelopeMatrix.SupportedEvents);
        Assert.Contains("postToolUse", HookEnvelopeMatrix.SupportedEvents);
    }

    [Fact]
    public void RegisterFromManifest_PopulatesCapabilities()
    {
        var manifest = new AdapterManifest
        {
            SdkVersion = 1,
            Name = "claude-code",
            DisplayName = "Claude Code",
            Description = "desc",
            Accent = "#fff",
            Binary = "claude",
            Version = "1.0",
            Methods = new Dictionary<string, AdapterMethod>(),
            HookEnvelopes = new Dictionary<string, HookEnvelopeDeclaration>
            {
                ["sessionStartManifest"] = new() { Kind = HookEnvelopeKind.Identity },
                ["userPromptSubmit"] = new() { Kind = HookEnvelopeKind.HookSpecificOutput, IncludeSystemMessage = true },
            },
        };

        var matrix = new HookEnvelopeMatrix();
        matrix.RegisterFromManifest(manifest);

        Assert.Equal(HookEnvelopeKind.Identity, matrix.GetCapability("claude-code", "sessionStartManifest").Kind);
        Assert.Equal(HookEnvelopeKind.HookSpecificOutput, matrix.GetCapability("claude-code", "userPromptSubmit").Kind);
        Assert.True(matrix.GetCapability("claude-code", "userPromptSubmit").IncludeSystemMessage);
        Assert.Equal(HookEnvelopeKind.None, matrix.GetCapability("claude-code", "postToolUse").Kind);
    }
}

public sealed class ContextInjectorTests
{
    [Fact]
    public void Render_Identity_StringUnwrappedToRawText()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("\"my context\"").RootElement.Clone();
        var result = injector.Render("claude-code", "sessionStartManifest", context);
        Assert.Equal("my context", result);
    }

    [Fact]
    public void Render_Identity_Object_RawJson()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("""{"workspace":"ws1"}""").RootElement.Clone();
        var result = injector.Render("claude-code", "sessionStartManifest", context);
        Assert.Equal(context.GetRawText(), result);
    }

    [Fact]
    public void Render_HookSpecificOutput_AdditionalContext()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "userPromptSubmit", HookEnvelopeKind.HookSpecificOutput);
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("\"some context\"").RootElement.Clone();
        var result = injector.Render("claude-code", "userPromptSubmit", context);
        Assert.Contains("hookSpecificOutput", result);
        Assert.Contains("additionalContext", result);
        Assert.Contains("some context", result);
    }

    [Fact]
    public void Render_HookSpecificOutput_WithSystemMessage()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "userPromptSubmit", HookEnvelopeKind.HookSpecificOutput, includeSystemMessage: true);
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("\"ctx\"").RootElement.Clone();
        var result = injector.Render("claude-code", "userPromptSubmit", context);
        Assert.Contains("systemMessage", result);
    }

    [Fact]
    public void Render_FlatAdditionalContext_DirectValue()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "preToolUse", HookEnvelopeKind.FlatAdditionalContext);
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("\"ctx\"").RootElement.Clone();
        var result = injector.Render("claude-code", "preToolUse", context);
        Assert.Contains("additionalContext", result);
        Assert.DoesNotContain("[", result);
    }

    [Fact]
    public void Render_None_EmptyObject()
    {
        var matrix = new HookEnvelopeMatrix();
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("\"ctx\"").RootElement.Clone();
        var result = injector.Render("claude-code", "postToolUse", context);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void Render_UndefinedContext_EmptyObject()
    {
        var matrix = new HookEnvelopeMatrix();
        matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
        var injector = new ContextInjector(matrix);
        var result = injector.Render("claude-code", "sessionStartManifest", default);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void Render_NoCapability_ReturnsEmpty()
    {
        var matrix = new HookEnvelopeMatrix();
        var injector = new ContextInjector(matrix);
        var context = JsonDocument.Parse("\"ctx\"").RootElement.Clone();
        var result = injector.Render("claude-code", "userPromptSubmit", context);
        Assert.Equal("{}", result);
    }
}
