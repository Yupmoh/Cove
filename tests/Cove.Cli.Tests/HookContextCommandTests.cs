using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class HookContextCommandTests
{
    [Fact]
    public async Task HookContext_RendersCursorSessionContextFromManagedSkill()
    {
        var root = TestDirectory.Create("hook-context-");
        var skillPath = Path.Combine(root, "skill.md");
        File.WriteAllText(skillPath, "Cove control\nUse the CLI.");
        await using var skillEnvironment = await ProcessEnvironmentScope.SetAsync(
            "COVE_SKILL_PATH",
            skillPath);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var dataDir = CoveDataDir.ForRoot(CoveChannel.Stable, root);
            var paths = new DaemonPaths(dataDir);
            var context = new CommandContext(
                paths,
                ControlEndpointFactory.FromSocketPath(dataDir.SocketPath),
                stdout,
                stderr,
                ["--adapter", "cursor-agent"]);

            var exitCode = await ActivityCommands.HookContext(context);

            Assert.Equal(0, exitCode);
            Assert.Equal("", stderr.ToString());
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal(
                "Cove control\nUse the CLI.",
                document.RootElement.GetProperty("additional_context").GetString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public async Task HookContext_EmitsEmptyObjectOutsideManagedNook()
    {
        await using var skillEnvironment = await ProcessEnvironmentScope.SetAsync(
            "COVE_SKILL_PATH",
            null);
        var root = TestDirectory.Create("hook-context-empty-");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var dataDir = CoveDataDir.ForRoot(CoveChannel.Stable, root);
            var paths = new DaemonPaths(dataDir);
            var context = new CommandContext(
                paths,
                ControlEndpointFactory.FromSocketPath(dataDir.SocketPath),
                stdout,
                stderr,
                ["--adapter", "cursor-agent"]);

            var exitCode = await ActivityCommands.HookContext(context);

            Assert.Equal(0, exitCode);
            Assert.Equal("{}" + Environment.NewLine, stdout.ToString());
            Assert.Equal("", stderr.ToString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }
}
