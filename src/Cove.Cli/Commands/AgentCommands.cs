using System.Text.Json;
using Cove.Protocol;

namespace Cove.Cli;

internal static class AgentCommands
{
    [CoveCommand("agent list")]
    public static Task<int> AgentList(CommandContext ctx)
    {
        var parameters = new AgentListParams(
            ArgValue(ctx.Args, "--scope") ?? "same-tab");
        var json = JsonSerializer.Serialize(
            parameters,
            CoveJsonContext.Default.AgentListParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/agent.list",
            json,
            data => RenderAgentList(ctx, data));
    }

    [CoveCommand("agent message")]
    public static Task<int> AgentMessage(CommandContext ctx)
    {
        var positional = Positionals(
            ctx.Args,
            ["--submit-pause-ms"],
            ["--no-frame"]);
        if (positional.Count < 2)
        {
            ctx.Stderr.WriteLine(
                "usage: cove agent message <target> <body>");
            return Task.FromResult(1);
        }
        var parameters = new AgentMessageParams(
            positional[0],
            string.Join(' ', positional.Skip(1)),
            null,
            null,
            null,
            ctx.Args.Contains("--no-frame"),
            IntValue(ctx.Args, "--submit-pause-ms"));
        var json = JsonSerializer.Serialize(
            parameters,
            CoveJsonContext.Default.AgentMessageParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/agent.message",
            json,
            data => RenderMutation(
                ctx,
                data,
                $"sent to {positional[0]}"));
    }

    [CoveCommand("agent launch")]
    public static Task<int> AgentLaunch(CommandContext ctx) =>
        Launch(ctx, "new");

    [CoveCommand("agent resume")]
    public static Task<int> AgentResume(CommandContext ctx) =>
        Launch(ctx, "resume");

    [CoveCommand("skills list")]
    public static Task<int> SkillsList(CommandContext ctx) =>
        ctx.RouteCoreAsync("cove://commands/skills.index");

    [CoveCommand("skills resolve-prompt-sigils")]
    public static Task<int> SkillsResolvePromptSigils(
        CommandContext ctx) =>
        ctx.RouteCoreAsync(
            "cove://commands/skills.resolve-prompt-sigils");

    [CoveCommand("agent definition list")]
    public static Task<int> AgentDefinitionList(CommandContext ctx) =>
        ctx.RouteCoreAsync(
            "cove://commands/agent.definition.list");

    [CoveCommand("agent definition show")]
    public static Task<int> AgentDefinitionShow(CommandContext ctx) =>
        ctx.RouteCoreAsync(
            "cove://commands/agent.definition.show");

    [CoveCommand("agent definition delete")]
    public static Task<int> AgentDefinitionDelete(
        CommandContext ctx) =>
        ctx.RouteCoreAsync(
            "cove://commands/agent.definition.delete");

    private static Task<int> Launch(
        CommandContext ctx,
        string mode)
    {
        var valueFlags = new[]
        {
            "--profile",
            "--cwd",
            "--relative-to",
            "--placement",
            "--bay-id",
            "--name",
            "--cols",
            "--rows",
            "--access-scope",
        };
        var positional = Positionals(
            ctx.Args,
            valueFlags,
            ["--yolo"]);
        var required = mode == "resume" ? 2 : 1;
        if (positional.Count < required)
        {
            ctx.Stderr.WriteLine(
                mode == "resume"
                    ? "usage: cove agent resume <adapter> <session-id>"
                    : "usage: cove agent launch <adapter>");
            return Task.FromResult(1);
        }
        var parameters = new AgentLaunchParams(
            mode,
            positional[0],
            ArgValue(ctx.Args, "--profile") ?? "default",
            mode == "resume" ? positional[1] : null,
            ArgValue(ctx.Args, "--cwd"),
            ArgValue(ctx.Args, "--relative-to"),
            ArgValue(ctx.Args, "--placement") ?? "right",
            ArgValue(ctx.Args, "--bay-id"),
            ArgValue(ctx.Args, "--name"),
            ctx.Args.Contains("--yolo"),
            IntValue(ctx.Args, "--cols") ?? 80,
            IntValue(ctx.Args, "--rows") ?? 24,
            ArgValue(ctx.Args, "--access-scope") ?? "same-bay");
        var json = JsonSerializer.Serialize(
            parameters,
            CoveJsonContext.Default.AgentLaunchParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/agent.launch",
            json,
            data => RenderLaunch(ctx, data));
    }

    private static int RenderAgentList(
        CommandContext ctx,
        JsonElement? data)
    {
        if (data is not { } element)
        {
            ctx.Stderr.WriteLine("error: invalid_response");
            return 1;
        }
        if (ctx.IsJson)
        {
            ctx.Render(element);
            return 0;
        }
        var result = element.Deserialize(
            CoveJsonContext.Default.AgentListResult);
        if (result is null)
        {
            ctx.Stderr.WriteLine("error: invalid_response");
            return 1;
        }
        ctx.Stdout.WriteLine(
            "NOOK ID\tADAPTER\tNAME\tSTATUS\tBAY\tSHORE\tSCOPE");
        foreach (var agent in result.Agents)
        {
            ctx.Stdout.WriteLine(
                $"{agent.NookId}\t{agent.Adapter}\t"
                + $"{agent.Name ?? "-"}\t{agent.Status}\t"
                + $"{agent.Bay ?? "-"}\t{agent.Shore ?? "-"}\t"
                + agent.McpAccessScope);
        }
        return 0;
    }

    private static int RenderLaunch(
        CommandContext ctx,
        JsonElement? data)
    {
        if (data is not { } element)
        {
            ctx.Stderr.WriteLine("error: invalid_response");
            return 1;
        }
        if (ctx.IsJson)
        {
            ctx.Render(element);
            return 0;
        }
        var result = element.Deserialize(
            CoveJsonContext.Default.AgentLaunchResult);
        if (result is null)
        {
            ctx.Stderr.WriteLine("error: invalid_response");
            return 1;
        }
        ctx.Stdout.WriteLine(
            $"{(result.Resumed ? "resumed" : "launched")} "
            + $"{result.NookId} in "
            + $"{result.BayId}/{result.ShoreId}");
        return 0;
    }

    private static int RenderMutation(
        CommandContext ctx,
        JsonElement? data,
        string human)
    {
        if (ctx.IsJson)
        {
            if (data is { } element)
                ctx.Render(element);
            else
                ctx.Stdout.WriteLine("{}");
        }
        else
        {
            ctx.Stdout.WriteLine(human);
        }
        return 0;
    }

    private static List<string> Positionals(
        string[] args,
        IReadOnlyCollection<string> valueFlags,
        IReadOnlyCollection<string> boolFlags)
    {
        var values = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (valueFlags.Contains(args[index]))
            {
                index++;
                continue;
            }
            if (boolFlags.Contains(args[index])
                || args[index] == "--json")
            {
                continue;
            }
            values.Add(args[index]);
        }
        return values;
    }

    private static string? ArgValue(
        string[] args,
        string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static int? IntValue(
        string[] args,
        string flag) =>
        int.TryParse(
            ArgValue(args, flag),
            out var value)
            ? value
            : null;
}
