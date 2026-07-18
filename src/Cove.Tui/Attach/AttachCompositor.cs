using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Platform.Terminal;
using Cove.Protocol;
using Cove.Tui.Compositor;
using Cove.Tui.Emit;
using Cove.Tui.Vt;

namespace Cove.Tui.Attach;

public static class AttachCompositor
{
    public static async Task<int> RunAsync(DaemonPaths paths, IControlEndpoint endpoint, string nookId, string source)
    {
        if (string.IsNullOrEmpty(nookId))
        {
            Console.Error.WriteLine("usage: cove attach <nookId>");
            Console.Error.WriteLine("       cove attach --raw <session>");
            return 1;
        }

        var connector = new DaemonConnector(paths, endpoint);
        var conn = await connector.TryConnectAndHelloAsync("tui-attach", System.Threading.CancellationToken.None).ConfigureAwait(false);
        if (conn is null)
        {
            Console.Error.WriteLine("no daemon running — start one with: cove daemon");
            return 1;
        }

        await using (conn)
        {
            var width = Console.IsOutputRedirected ? 80 : Console.WindowWidth;
            var height = Console.IsOutputRedirected ? 24 : Console.WindowHeight;
            var vt = new VtEmulator(width, height);
            var emitter = new AnsiDiffEmitter();
            var session = new AttachSession(conn, nookId);

            SubscribeResult subResult;
            try
            {
                subResult = await session.SubscribeAsync("tui-attach", System.Threading.CancellationToken.None).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Console.Error.WriteLine($"attach failed: {ex.Message}");
                return 1;
            }

            RawModeScope? rawMode = null;
            try
            {
                try { rawMode = RawModeScope.TryEnter(); } catch { }
                EnterAlternateScreen();
                var cts = new System.Threading.CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                var stdinTask = Task.Run(async () =>
                {
                    var stdin = Console.OpenStandardInput();
                    var buf = new byte[4096];
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var read = await stdin.ReadAsync(buf, cts.Token).ConfigureAwait(false);
                            if (read == 0) break;
                            await session.SendInputAsync(buf.AsSpan(0, read).ToArray(), cts.Token).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException) { break; }
                        catch { break; }
                    }
                });

                try
                {
                    await session.PumpAsync(
                        onData: async (raw, ct) =>
                        {
                            var chars = System.Text.Encoding.UTF8.GetString(raw.Span);
                            vt.Feed(chars);
                            Console.Write(emitter.Emit(vt.Grid));
                            Console.Out.Flush();
                            await Task.CompletedTask;
                        },
                        onEnd: (finalOffset, exitCode, ct) =>
                        {
                            cts.Cancel();
                            return Task.CompletedTask;
                        },
                        ct: cts.Token).ConfigureAwait(false);
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    Console.Error.WriteLine($"stream ended: {ex.Message}");
                }

                try { await stdinTask.ConfigureAwait(false); } catch { }
            }
            finally
            {
                try
                {
                    ExitAlternateScreen();
                }
                finally
                {
                    rawMode?.Dispose();
                }
            }
            Console.Write("\x1b[0m\x1b[2J\x1b[H");
            Console.Out.Flush();
            return 0;
        }
    }

    private static void EnterAlternateScreen()
    {
        Console.Write("\x1b[?1049h\x1b[2J\x1b[H");
        Console.Out.Flush();
    }

    private static void ExitAlternateScreen()
    {
        Console.Write("\x1b[?1049l");
        Console.Out.Flush();
    }
}
