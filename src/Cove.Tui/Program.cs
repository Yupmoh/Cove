using Cove.Tui.Compositor;
using Cove.Tui.Emit;
using Cove.Tui.Vt;

namespace Cove.Tui;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--raw")
        {
            return RawAttach();
        }

        var width = Console.IsOutputRedirected ? 80 : Console.WindowWidth;
        var height = Console.IsOutputRedirected ? 24 : Console.WindowHeight;
        var vt = new VtEmulator(width, height);
        var emitter = new AnsiDiffEmitter();

        vt.Feed("Cove TUI — press Ctrl+C to exit\n\r");
        Console.Write(emitter.Emit(vt.Grid));
        Console.Out.Flush();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            Task.WaitAll([RunStdinLoop(vt, emitter, cts.Token)], cts.Token);
        }
        catch (OperationCanceledException) { }

        Console.Write("\x1b[0m\x1b[2J\x1b[H");
        Console.Out.Flush();
        return 0;
    }

    private static async Task RunStdinLoop(VtEmulator vt, AnsiDiffEmitter emitter, CancellationToken ct)
    {
        var stdin = Console.OpenStandardInput();
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested)
        {
            var read = await stdin.ReadAsync(buffer, ct);
            if (read == 0) break;
            var chars = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            vt.Feed(chars);
            Console.Write(emitter.Emit(vt.Grid));
            Console.Out.Flush();
        }
    }

    private static int RawAttach()
    {
        Console.Write("Cove attach --raw: raw PTY passthrough not yet implemented.\n");
        return 0;
    }
}
