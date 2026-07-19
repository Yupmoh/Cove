using Cove.Protocol;

namespace Cove.Cli;

internal static class BrowserCommands
{
    [CoveCommand("browser open")]
        public static Task<int> BrowserOpen(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/browser.open");

    [CoveCommand("browser navigate")]
        public static Task<int> BrowserNavigate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/browser.navigate");

    [CoveCommand("browser back")]
        public static Task<int> BrowserBack(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/browser.back");

    [CoveCommand("browser forward")]
        public static Task<int> BrowserForward(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/browser.forward");

    [CoveCommand("browser reload")]
        public static Task<int> BrowserReload(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/browser.reload");

    [CoveCommand("browser close")]
        public static Task<int> BrowserClose(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/browser.close");
}
