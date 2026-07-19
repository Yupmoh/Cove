using Cove.Protocol;

namespace Cove.Cli;

internal static class EnvironmentCommands
{
    [CoveCommand("context")]
    public static Task<int> Context(CommandContext ctx)
    {
        var nookId = System.Environment.GetEnvironmentVariable("COVE_NOOK_ID") ?? "(unset)";
        var cwd = System.Environment.CurrentDirectory;
        var bay = System.Environment.GetEnvironmentVariable("COVE_BAY_ID") ?? "(unset)";
        var shore = System.Environment.GetEnvironmentVariable("COVE_SHORE_ID") ?? "(unset)";
        ctx.Stdout.WriteLine($"nook: {nookId}");
        ctx.Stdout.WriteLine($"bay: {bay}");
        ctx.Stdout.WriteLine($"shore: {shore}");
        ctx.Stdout.WriteLine($"cwd: {cwd}");
        return Task.FromResult(0);
    }
}
