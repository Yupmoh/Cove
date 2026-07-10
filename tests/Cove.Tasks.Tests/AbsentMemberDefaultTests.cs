using System.Text.Json;
using Cove.Tasks.LaunchConfig;
using Xunit;

namespace Cove.Tasks.Tests;

public class AbsentMemberDefaultTests
{
    [Fact]
    public void LaunchConfigModel_AbsentExecutionMode_KeepsPane()
    {
        var m = JsonSerializer.Deserialize("{\"adapter\":\"claude\"}", TaskJsonContext.Default.LaunchConfigModel)!;
        Assert.Equal("pane", m.ExecutionMode);
    }

    [Fact]
    public void LaunchConfigModel_PresentExecutionMode_Roundtrips()
    {
        var m = JsonSerializer.Deserialize("{\"executionMode\":\"worktree\"}", TaskJsonContext.Default.LaunchConfigModel)!;
        Assert.Equal("worktree", m.ExecutionMode);
    }
}
