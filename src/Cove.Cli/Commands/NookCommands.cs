using Cove.Protocol;
using Cove.Tui.Attach;

namespace Cove.Cli;

internal static class NookCommands
{
    [CoveCommand("nook list")]
        public static Task<int> NookList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/nook.list");

    [CoveCommand("attach")]
        public static async Task<int> Attach(CommandContext ctx)
        {
            var args = ctx.Args;
            var raw = args.Length > 0 && args[0] == "--raw";
            if (raw)
            {
                if (args.Length < 2)
                {
                    ctx.Stderr.WriteLine("usage: cove attach --raw <session>");
                    return 1;
                }
                var session = args[1];
                using var attachBuf = new System.IO.MemoryStream();
                using (var attachWriter = new System.Text.Json.Utf8JsonWriter(attachBuf))
                {
                    attachWriter.WriteStartObject();
                    attachWriter.WriteString("session", session);
                    attachWriter.WriteEndObject();
                    attachWriter.Flush();
                }
                return await ctx.RouteCoreWithParamsAsync("cove://commands/attach.raw", System.Text.Encoding.UTF8.GetString(attachBuf.ToArray()));
            }
            var nookId = args.Length > 0 ? args[0] : "";
            return await AttachCompositor.RunAsync(ctx.Paths, ctx.Endpoint, nookId, ctx.Source);
        }

    [CoveCommand("nook-types list")]
        public static Task<int> NookTypesList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/nook-types.list");
}
