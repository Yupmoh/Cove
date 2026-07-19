using Cove.Platform;
using Cove.Protocol;

namespace Cove.Cli;

internal static class MigrationCommands
{
    [CoveCommand("migrate")]
    public static async Task<int> Migrate(CommandContext ctx)
    {
        var connector = new Cove.Engine.Daemon.DaemonConnector(ctx.Paths, ctx.Endpoint);
        var conn = await connector.TryConnectAndHelloAsync("cli", System.Threading.CancellationToken.None);
        if (conn is not null)
        {
            await conn.DisposeAsync();
            ctx.Stderr.WriteLine("error: daemon_running, stop it first (cove stop)");
            return 1;
        }
        using var loggerFactory = Cove.Platform.CoveLog.CreateConsoleLoggerFactory();
        var runner = new Cove.Engine.Migrations.MigrationRunner(ctx.Paths.DataDir.Root, loggerFactory.CreateLogger("migrate"));
        var result = runner.Migrate();
        if (result.NoOp)
            ctx.Stdout.WriteLine($"no migrations needed (at version {result.ToVersion})");
        else
            ctx.Stdout.WriteLine($"migrated {result.FromVersion} -> {result.ToVersion}");
        return 0;
    }
}
