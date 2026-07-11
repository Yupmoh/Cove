using Cove.Engine.Nooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SkipAuditTests
{
    [Fact]
    public void NoAchievementsNookType_Registered()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("achievements"));
    }

    [Fact]
    public void NoStreakCounterNookType_Registered()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("streaks"));
        Assert.False(registry.IsRegistered("day-streak"));
    }

    [Fact]
    public void NoCalibrationGateNookType_Registered()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("calibration"));
    }

    [Fact]
    public void NoConfettiOrCelebrationNookType_Registered()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("celebrations"));
        Assert.False(registry.IsRegistered("confetti"));
    }

    [Fact]
    public void NoRenameNagNookType_Registered()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("rename-nag"));
    }

    [Fact]
    public async Task NoGamificationCommands_Registered()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/achievements.list");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.Null(response);
    }

    [Fact]
    public async Task NoStreakCommands_Registered()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/streak.get");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.Null(response);
    }

    [Fact]
    public async Task NoCalibrationCommands_Registered()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/calibration.run");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.Null(response);
    }
}
