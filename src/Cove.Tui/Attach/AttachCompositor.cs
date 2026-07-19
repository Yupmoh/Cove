using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Platform.Terminal;
using Cove.Protocol;
using Cove.Tui.Compositor;
using Cove.Tui.Emit;
using Cove.Tui.Vt;

namespace Cove.Tui.Attach;

public interface IAttachTerminal
{
    Stream Input { get; }
    TextWriter Output { get; }
    TextWriter Error { get; }
    int Width { get; }
    int Height { get; }
    IDisposable? EnterRawMode();
    IDisposable RegisterCancelHandler(Action cancel);
    void EnterAlternateScreen();
    void ExitAlternateScreen();
}

public static class AttachCompositor
{
    public static async Task<int> RunAsync(
        DaemonPaths paths,
        IControlEndpoint endpoint,
        string nookId,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nookId))
        {
            Console.Error.WriteLine("usage: cove attach <nookId>");
            Console.Error.WriteLine("       cove attach --raw <session>");
            return 1;
        }

        Stream stream;
        try
        {
            stream = await endpoint.ConnectAsync(
                ProtocolConstants.ReadinessTimeoutMs,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Console.Error.WriteLine("no daemon running — start one with: cove daemon");
            return 1;
        }

        var conn = new FrameConnection(stream);
        await using (conn)
        {
            return await RunConnectedAsync(
                conn,
                paths.Channel,
                nookId,
                source,
                SystemAttachTerminal.Instance,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public static AttachSession CreateSession(
        FrameConnection connection,
        string channel,
        string nookId,
        string source) =>
        new(
            connection,
            nookId,
            "tui-attach",
            CoveBuild.InformationalVersion,
            channel,
            source);

    public static async Task<int> RunConnectedAsync(
        FrameConnection connection,
        string channel,
        string nookId,
        string source,
        IAttachTerminal terminal,
        CancellationToken cancellationToken)
    {
        var width = terminal.Width;
        var height = terminal.Height;
        VtEmulator vt = new(width, height);
        AnsiDiffEmitter emitter = new();
        var session = CreateSession(connection, channel, nookId, source);

        SubscribeResult subResult;
        try
        {
            subResult = await session.SubscribeAsync(cancellationToken).ConfigureAwait(false);
            var checkpoint = DecodeOptional(subResult.TerminalCheckpointBase64);
            var modes = DecodeOptional(subResult.TerminalModePreambleBase64);
            if (subResult.CheckpointCols > 0 || subResult.CheckpointRows > 0)
            {
                var checkpointWidth = subResult.CheckpointCols > 0 ? subResult.CheckpointCols : width;
                var checkpointHeight = subResult.CheckpointRows > 0 ? subResult.CheckpointRows : height;
                vt = new VtEmulator(checkpointWidth, checkpointHeight);
                emitter = new AnsiDiffEmitter();
            }
            FeedRestore(vt, checkpoint, modes);
        }
        catch (Exception ex)
        {
            terminal.Error.WriteLine($"attach failed: {ex.Message}");
            return 1;
        }

        using var pumpCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IDisposable? rawMode = null;
        IDisposable? cancelRegistration = null;
        Stream? input = null;
        Task? stdinTask = null;
        Exception? primaryFailure = null;
        List<Exception>? cleanupFailures = null;
        var alternateScreenEntered = false;
        try
        {
            rawMode = terminal.EnterRawMode();
            alternateScreenEntered = true;
            terminal.EnterAlternateScreen();
            terminal.Output.Write(emitter.Emit(vt.Grid));
            terminal.Output.Flush();
            cancelRegistration = terminal.RegisterCancelHandler(pumpCancellation.Cancel);

            input = terminal.Input;
            stdinTask = PumpInputAsync(input, session, pumpCancellation);
            await session.PumpAsync(
                onData: (data, _) =>
                {
                    vt.Feed(System.Text.Encoding.UTF8.GetString(data.Data.Span));
                    terminal.Output.Write(emitter.Emit(vt.Grid));
                    terminal.Output.Flush();
                    return Task.CompletedTask;
                },
                onResync: (resync, _) =>
                {
                    var checkpointWidth = resync.CheckpointCols > 0 ? resync.CheckpointCols : width;
                    var checkpointHeight = resync.CheckpointRows > 0 ? resync.CheckpointRows : height;
                    vt = new VtEmulator(checkpointWidth, checkpointHeight);
                    emitter = new AnsiDiffEmitter();
                    FeedRestore(vt, resync.TerminalCheckpoint.Span, resync.TerminalModePreamble.Span);
                    terminal.Output.Write("\x1b[2J\x1b[H");
                    terminal.Output.Write(emitter.Emit(vt.Grid));
                    terminal.Output.Flush();
                    return Task.CompletedTask;
                },
                onEnd: (_, _) => Task.CompletedTask,
                ct: pumpCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (pumpCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            primaryFailure = ex;
        }
        finally
        {
            try
            {
                pumpCancellation.Cancel();
            }
            catch (Exception ex)
            {
                AddCleanupFailure(ref cleanupFailures, ex);
            }
            if (input is not null)
            {
                try
                {
                    input.Dispose();
                }
                catch (Exception ex)
                {
                    AddCleanupFailure(ref cleanupFailures, ex);
                }
            }
            if (stdinTask is not null)
            {
                try
                {
                    await stdinTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AddCleanupFailure(ref cleanupFailures, ex);
                }
            }
            try
            {
                cancelRegistration?.Dispose();
            }
            catch (Exception ex)
            {
                AddCleanupFailure(ref cleanupFailures, ex);
            }
            if (alternateScreenEntered)
            {
                try
                {
                    terminal.ExitAlternateScreen();
                }
                catch (Exception ex)
                {
                    AddCleanupFailure(ref cleanupFailures, ex);
                }
            }
            try
            {
                rawMode?.Dispose();
            }
            catch (Exception ex)
            {
                AddCleanupFailure(ref cleanupFailures, ex);
            }
        }

        if (primaryFailure is not null)
            terminal.Error.WriteLine($"stream ended: {primaryFailure.Message}");
        if (cleanupFailures is not null)
        {
            foreach (var failure in cleanupFailures)
                terminal.Error.WriteLine($"attach cleanup failed: {failure.Message}");
        }
        return primaryFailure is null && cleanupFailures is null ? 0 : 1;
    }

    private static async Task PumpInputAsync(
        Stream input,
        AttachSession session,
        CancellationTokenSource cancellation)
    {
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellation.Token).ConfigureAwait(false);
                if (read == 0)
                    return;
                await session.SendInputAsync(buffer.AsSpan(0, read).ToArray(), cancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    private static byte[] DecodeOptional(string? value) =>
        string.IsNullOrEmpty(value) ? Array.Empty<byte>() : Convert.FromBase64String(value);

    private static void AddCleanupFailure(ref List<Exception>? failures, Exception failure)
    {
        failures ??= [];
        failures.Add(failure);
    }

    private static void FeedRestore(
        VtEmulator terminal,
        ReadOnlySpan<byte> checkpoint,
        ReadOnlySpan<byte> terminalModePreamble)
    {
        if (!terminalModePreamble.IsEmpty)
            terminal.Feed(System.Text.Encoding.UTF8.GetString(terminalModePreamble));
        if (!checkpoint.IsEmpty)
            terminal.Feed(System.Text.Encoding.UTF8.GetString(checkpoint));
    }

    private sealed class SystemAttachTerminal : IAttachTerminal
    {
        public static SystemAttachTerminal Instance { get; } = new();

        public Stream Input => new InterruptibleConsoleInputStream();
        public TextWriter Output => Console.Out;
        public TextWriter Error => Console.Error;
        public int Width => Console.IsOutputRedirected ? 80 : Console.WindowWidth;
        public int Height => Console.IsOutputRedirected ? 24 : Console.WindowHeight;

        public IDisposable? EnterRawMode()
        {
            try
            {
                return RawModeScope.TryEnter();
            }
            catch
            {
                return null;
            }
        }

        public IDisposable RegisterCancelHandler(Action cancel)
        {
            ConsoleCancelEventHandler handler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancel();
            };
            Console.CancelKeyPress += handler;
            return new CallbackDisposable(() => Console.CancelKeyPress -= handler);
        }

        public void EnterAlternateScreen()
        {
            Console.Write("\x1b[?1049h\x1b[2J\x1b[H");
            Console.Out.Flush();
        }

        public void ExitAlternateScreen()
        {
            Console.Write("\x1b[0m\x1b[?1049l");
            Console.Out.Flush();
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                callback();
        }
    }
}
