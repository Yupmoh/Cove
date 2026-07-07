using Cove.Platform;
using Cove.Protocol;

namespace Cove.Cli;

internal static class CliCommands
{
    [CoveCommand("version")]
    public static Task<int> Version(CommandContext ctx)
    {
        ctx.Stdout.WriteLine(CoveBuild.InformationalVersion);
        return Task.FromResult(0);
    }

    [CoveCommand("pane list")]
    public static Task<int> PaneList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/pane.list");

    [CoveCommand("skills list")]
    public static Task<int> SkillsList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/skills.index");

    [CoveCommand("skills resolve-prompt-sigils")]
    public static Task<int> SkillsResolvePromptSigils(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/skills.resolve-prompt-sigils");

    [CoveCommand("agent definition list")]
    public static Task<int> AgentDefinitionList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.list");

    [CoveCommand("agent definition show")]
    public static Task<int> AgentDefinitionShow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.show");

    [CoveCommand("agent definition delete")]
    public static Task<int> AgentDefinitionDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.delete");

    [CoveCommand("launch-profile list")]
    public static Task<int> LaunchProfileList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/launch-profile.list");

    [CoveCommand("launch-profile set-default")]
    public static Task<int> LaunchProfileSetDefault(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/launch-profile.set-default");

    [CoveCommand("launch-profile delete")]
    public static Task<int> LaunchProfileDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/launch-profile.delete");

    [CoveCommand("adapter-env list")]
    public static Task<int> AdapterEnvList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.list");

    [CoveCommand("adapter-env save")]
    public static Task<int> AdapterEnvSave(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.save");

    [CoveCommand("adapter-env resolve")]
    public static Task<int> AdapterEnvResolve(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.resolve");

    [CoveCommand("hook emit")]
    public static async Task<int> HookEmit(CommandContext ctx, string[] args)
    {
        var portFile = System.IO.Path.Combine(ctx.Paths.DataDir.Root, "hook-port");
        if (!System.IO.File.Exists(portFile))
        {
            await ctx.Stderr.WriteLineAsync("error: hook server not running");
            return 1;
        }
        if (!int.TryParse(System.IO.File.ReadAllText(portFile).Trim(), out var port))
        {
            await ctx.Stderr.WriteLineAsync("error: invalid hook-port file");
            return 1;
        }
        var adapter = ArgValue(args, "--adapter");
        var paneId = ArgValue(args, "--pane-id");
        var verbose = Array.IndexOf(args, "--verbose") >= 0;
        var @event = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
        if (adapter is null || @event is null)
        {
            await ctx.Stderr.WriteLineAsync("error: event and --adapter required");
            return 1;
        }
        var payload = "{}";
        if (System.Console.IsInputRedirected)
        {
            using var stdin = System.Console.In;
            var stdinPayload = stdin.ReadToEnd();
            if (!string.IsNullOrEmpty(stdinPayload))
                payload = stdinPayload;
        }
        var client = new Cove.Engine.Hooks.HookEmitClient(port);
        var result = await client.EmitAsync(adapter, @event, payload, paneId);
        if (!result.Ok)
        {
            await ctx.Stderr.WriteLineAsync($"error: hook emit failed status={result.StatusCode}");
            return 1;
        }
        if (verbose && result.Body is not null)
            await ctx.Stderr.WriteLineAsync(result.Body);
        return 0;
    }

    private static string? ArgValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return null;
    }
}
