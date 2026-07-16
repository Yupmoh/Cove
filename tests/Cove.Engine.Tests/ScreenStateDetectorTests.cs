using System.Text;
using Cove.Adapters;
using Cove.Engine.Agents;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScreenStateDetectorTests
{
    private static ScreenStateDeclaration Decl(params (string Pattern, string Status)[] rules) =>
        new()
        {
            Rules = rules.Select(r => new ScreenStateRule { Pattern = r.Pattern, Status = r.Status }).ToArray(),
        };

    [Fact]
    public void AnsiStrip_RemovesCsiOscAndControls()
    {
        var raw = Encoding.UTF8.GetBytes("\x1b[31mred\x1b[0m \x1b]0;title\x07plain\r\n\x1b[2J\ttab");
        var text = ScreenStateDetector.AnsiStrip(raw);
        Assert.Equal("red plain\n\ttab", text);
    }

    [Fact]
    public void AnsiStrip_RemovesDcsAndSingleCharEscapes()
    {
        var raw = Encoding.UTF8.GetBytes("a\x1bP1$r0q\x1b\\b\x1b=c");
        Assert.Equal("abc", ScreenStateDetector.AnsiStrip(raw));
    }

    [Fact]
    public void Evaluate_FirstMatchWins_TopDown()
    {
        var decl = Decl(("(?i)allow", "needs-permission"), ("(?i)allow this tool", "needs-input"));
        var status = ScreenStateDetector.Evaluate(decl, "Allow this tool? [y/n]", ringAdvanced: true, quietElapsed: false, currentStatus: "idle");
        Assert.Equal("needs-permission", status);
    }

    [Fact]
    public void Evaluate_NoMatch_ActivityMeansActive()
    {
        var decl = Decl(("never-matches-anything", "needs-input"));
        var status = ScreenStateDetector.Evaluate(decl, "compiling...", ringAdvanced: true, quietElapsed: false, currentStatus: "idle");
        Assert.Equal("active", status);
    }

    [Fact]
    public void Evaluate_NoMatch_QuietMeansIdle()
    {
        var decl = Decl();
        var status = ScreenStateDetector.Evaluate(decl, "done output", ringAdvanced: false, quietElapsed: true, currentStatus: "active");
        Assert.Equal("idle", status);
    }

    [Fact]
    public void Evaluate_NoMatch_NoActivityNotQuiet_Holds()
    {
        var decl = Decl();
        var status = ScreenStateDetector.Evaluate(decl, "waiting", ringAdvanced: false, quietElapsed: false, currentStatus: "active");
        Assert.Null(status);
    }

    [Fact]
    public void Evaluate_MatchEqualToCurrent_ReturnsNull()
    {
        var decl = Decl(("ready", "idle"));
        var status = ScreenStateDetector.Evaluate(decl, "ready", ringAdvanced: true, quietElapsed: false, currentStatus: "idle");
        Assert.Null(status);
    }

    [Fact]
    public void Evaluate_Quiet_DoesNotDecayWaitingPrompt()
    {
        var decl = Decl(("(?i)allow", "needs-permission"));
        var status = ScreenStateDetector.Evaluate(decl, "", ringAdvanced: false, quietElapsed: true, currentStatus: "needs-permission");
        Assert.Null(status);
    }

    [Fact]
    public void Evaluate_AnsweredPrompt_DeltaWithoutPromptGoesActive()
    {
        var decl = Decl(("(?i)allow this tool", "needs-permission"));
        var status = ScreenStateDetector.Evaluate(decl, "y\ngranted y\nfinishing", ringAdvanced: true, quietElapsed: false, currentStatus: "needs-permission");
        Assert.Equal("active", status);
    }

    [Fact]
    public void Declaration_RejectsUnknownStatusVocabulary()
    {
        Assert.False(ScreenStateDeclaration.IsValidStatus("sleeping"));
        Assert.True(ScreenStateDeclaration.IsValidStatus("needs-permission"));
        Assert.True(ScreenStateDeclaration.IsValidStatus("active"));
    }

    [Fact]
    public void Declaration_ClampsTailBytes()
    {
        var decl = new ScreenStateDeclaration { TailBytes = 7 };
        Assert.Equal(256, decl.EffectiveTailBytes);
        var big = new ScreenStateDeclaration { TailBytes = 1 << 20 };
        Assert.Equal(65536, big.EffectiveTailBytes);
        var normal = new ScreenStateDeclaration { TailBytes = 8192 };
        Assert.Equal(8192, normal.EffectiveTailBytes);
    }
}
