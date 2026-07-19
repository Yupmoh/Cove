using Cove.Platform;
using Cove.Protocol;

namespace Cove.Cli;

internal static class VersionCommands
{
    [CoveCommand("version")]
        public static async Task<int> Version(CommandContext ctx)
        {
            var cliVersion = CoveBuild.InformationalVersion;
            var tagline = "an open-source AI-native terminal bay";

            ctx.Stdout.WriteLine("""
                  ___
                 /   \
                |  ◈  |
                 \___/
                """);
            ctx.Stdout.WriteLine($"  Cove — {tagline}");
            ctx.Stdout.WriteLine($"  \u25C8 v{cliVersion}");

            var connector = new Cove.Engine.Daemon.DaemonConnector(ctx.Paths, ctx.Endpoint);
            var conn = await connector.TryConnectAndHelloAsync("cli", System.Threading.CancellationToken.None);
            if (conn is not null)
            {
                await conn.DisposeAsync();
                ctx.Stdout.WriteLine($"  cli: v{cliVersion} (daemon: connected)");
            }
            else
            {
                ctx.Stdout.WriteLine($"  cli: v{cliVersion} (daemon: disconnected)");
            }
            return 0;
        }

    [CoveCommand("-V")]
        public static Task<int> VersionShort(CommandContext ctx)
        {
            ctx.Stdout.WriteLine($"cove {CoveBuild.InformationalVersion}");
            return Task.FromResult(0);
        }

    [CoveCommand("--version")]
        public static Task<int> VersionLong(CommandContext ctx)
        {
            ctx.Stdout.WriteLine($"cove {CoveBuild.InformationalVersion}");
            return Task.FromResult(0);
        }
}
