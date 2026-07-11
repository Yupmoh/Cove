using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
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

            EnterRawMode();
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
            ExitRawMode();
            Console.Write("\x1b[0m\x1b[2J\x1b[H");
            Console.Out.Flush();
            return 0;
        }
    }

    private static void EnterRawMode()
    {
        if (System.OperatingSystem.IsMacOS() || System.OperatingSystem.IsLinux())
        {
            try { rawTermios(); } catch { }
        }
        Console.Write("\x1b[?1049h\x1b[2J\x1b[H");
        Console.Out.Flush();
    }

    private static void ExitRawMode()
    {
        Console.Write("\x1b[?1049l");
        Console.Out.Flush();
        if (System.OperatingSystem.IsMacOS() || System.OperatingSystem.IsLinux())
        {
            try { restoreTermios(); } catch { }
        }
    }

    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern int tcgetattr(int fd, ref Termios termios);

    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern int tcsetattr(int fd, int actions, ref Termios termios);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Termios
    {
        public uint c_iflag;
        public uint c_oflag;
        public uint c_cflag;
        public uint c_lflag;
        public byte c_line;
        public System.IntPtr c_cc;
    }

    private static Termios? savedTermios;

    private static void rawTermios()
    {
        var t = new Termios();
        if (tcgetattr(0, ref t) != 0) return;
        savedTermios = t;
        t.c_lflag &= ~(0x00000001u | 0x00000002u | 0x00000008u);
        t.c_iflag &= ~(0x00000002u | 0x00000004u | 0x00000040u);
        tcsetattr(0, 0, ref t);
    }

    private static void restoreTermios()
    {
        if (savedTermios is { } t)
        {
            tcsetattr(0, 0, ref t);
            savedTermios = null;
        }
    }
}
