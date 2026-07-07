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

    [CoveCommand("config get")]
    public static Task<int> ConfigGet(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove config get <key>");
            return Task.FromResult(1);
        }
        var key = args[0];
        var configPath = System.IO.Path.Combine(ctx.Paths.DataDir.Root, "config.json");
        if (!System.IO.File.Exists(configPath))
        {
            ctx.Stderr.WriteLine($"error: config key '{key}' not found");
            return Task.FromResult(1);
        }
        var json = System.IO.File.ReadAllText(configPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(key.Replace(".", ":"), out var val))
            ctx.Stdout.WriteLine(val.ToString());
        else
            ctx.Stderr.WriteLine($"error: config key '{key}' not found");
        return Task.FromResult(0);
    }

    [CoveCommand("config set")]
    public static Task<int> ConfigSet(CommandContext ctx, string[] args)
    {
        if (args.Length < 2)
        {
            ctx.Stderr.WriteLine("usage: cove config set <key> <value>");
            return Task.FromResult(1);
        }
        var key = args[0];
        var value = args[1];
        var configPath = System.IO.Path.Combine(ctx.Paths.DataDir.Root, "config.json");
        var config = new Dictionary<string, string>();
        if (System.IO.File.Exists(configPath))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(configPath));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    config[prop.Name] = prop.Value.GetRawText();
            }
            catch { }
        }
        config[key.Replace(".", ":")] = value;
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var kv in config)
                writer.WriteString(kv.Key, kv.Value);
            writer.WriteEndObject();
            writer.Flush();
        }
        System.IO.File.WriteAllText(configPath, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
        ctx.Stdout.WriteLine($"set {key} = {value}");
        return Task.FromResult(0);
    }

    [CoveCommand("commands")]
    public static async Task<int> Commands(CommandContext ctx)
    {
        var registry = Cove.Generated.CoveCommandRegistry.Handlers;
        ctx.Stdout.WriteLine("CLI:");
        foreach (var cmd in registry.Keys.OrderBy(k => k))
            ctx.Stdout.WriteLine($"  {cmd}");
        ctx.Stdout.WriteLine($"Total: {registry.Count}");
        await Task.CompletedTask;
        return 0;
    }

    [CoveCommand("context")]
    public static Task<int> Context(CommandContext ctx)
    {
        var paneId = System.Environment.GetEnvironmentVariable("COVE_PANE_ID") ?? "(unset)";
        ctx.Stdout.WriteLine($"pane: {paneId}");
        return Task.FromResult(0);
    }

    [CoveCommand("exec")]
    public static Task<int> Exec(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove exec <dot.name> [--params '<json>']");
            return Task.FromResult(1);
        }
        var uri = "cove://commands/" + args[0].Replace(".", "/");
        return ctx.RouteCoreAsync(uri);
    }
}
