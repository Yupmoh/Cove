using System.Diagnostics;
using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Gui;
using Cove.Platform;
using Cove.Protocol;
using Cove.Testing;
using Cove.Tui.Attach;

namespace Cove.ClientContract.Tests;

internal sealed record ControlObservation(
    bool Ok,
    JsonElement? Data,
    string? ErrorCode,
    string? ErrorMessage);

internal interface IClientContractDriver : IAsyncDisposable
{
    string ControlClientKind { get; }
    string StreamClientKind { get; }
    string Source { get; }
    Task<ControlObservation> RequestAsync(string uri, JsonElement? parameters, CancellationToken cancellationToken);
    Task<StreamObservation> ObserveStreamAsync(CancellationToken cancellationToken);
}

internal static class ClientContractDriver
{
    public static IClientContractDriver Create(ClientFlavor flavor, Stream stream, string root) => flavor switch
    {
        ClientFlavor.Cli => new CliClientContractDriver(stream, root),
        ClientFlavor.Gui => new GuiClientContractDriver(stream),
        ClientFlavor.Tui => new TuiClientContractDriver(stream),
        _ => throw new ArgumentOutOfRangeException(nameof(flavor), flavor, "unknown client flavor")
    };

    public static ControlObservation Observe(ControlResponse response) => new(
        response.Ok,
        response.Data,
        response.Error?.Code,
        response.Error?.Message);

    public static ResyncObservation Observe(StreamResyncMessage message) => new(
        message.BaseOffset,
        message.TerminalModePreamble.ToArray(),
        message.TerminalCheckpoint.ToArray(),
        message.CheckpointCols,
        message.CheckpointRows);
}

internal sealed class CliClientContractDriver : IClientContractDriver
{
    private readonly Stream _stream;
    private readonly string _root;
    private AttachContractSession? _attach;

    public CliClientContractDriver(Stream stream, string root)
    {
        _stream = stream;
        _root = root;
    }

    public string ControlClientKind => "cli";
    public string StreamClientKind => "tui-attach";
    public string Source => "user:cli";

    public async Task<ControlObservation> RequestAsync(
        string uri,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        if (uri != "cove://commands/nook.list")
            throw new ArgumentOutOfRangeException(nameof(uri), uri, "CLI contract driver supports nook.list");
        if (parameters is not null)
            throw new ArgumentException("nook.list does not accept parameters", nameof(parameters));
        var executableName = OperatingSystem.IsWindows() ? "cove.exe" : "cove";
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("test configuration directory is unavailable");
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var executable = Path.Combine(
            repositoryRoot,
            "src",
            "Cove.Cli",
            "bin",
            configuration,
            "net10.0",
            executableName);
        TestPrerequisite.RequireFile(executable, $"CLI contract executable was not built: {executable}");
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("nook");
        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--no-autostart");
        startInfo.ArgumentList.Add("--json");
        startInfo.ArgumentList.Add("--channel");
        startInfo.ArgumentList.Add(ContractVectors.Channel);
        startInfo.Environment["COVE_DATA_DIR"] = _root;
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("CLI contract process did not start");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        int exitCode = await TestProcess.WaitForExitAsync(
            process,
            TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (exitCode == 0)
        {
            using var document = JsonDocument.Parse(stdout);
            return new ControlObservation(true, document.RootElement.Clone(), null, null);
        }
        var errorLine = stderr
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("error: ", StringComparison.Ordinal));
        var error = errorLine?["error: ".Length..].Split(": ", 2, StringSplitOptions.None);
        return new ControlObservation(
            false,
            null,
            error?.ElementAtOrDefault(0),
            error?.ElementAtOrDefault(1));
    }

    public Task<StreamObservation> ObserveStreamAsync(CancellationToken cancellationToken)
    {
        _attach ??= new AttachContractSession(_stream, StreamClientKind, Source);
        return _attach.ObserveStreamAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_attach is not null)
            await _attach.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class GuiClientContractDriver : IClientContractDriver
{
    private readonly Stream _stream;
    private EngineLink? _link;

    public GuiClientContractDriver(Stream stream)
    {
        _stream = stream;
    }

    public string ControlClientKind => "gui";
    public string StreamClientKind => "gui-stream";
    public string Source => "user:gui";

    public async Task<ControlObservation> RequestAsync(
        string uri,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        _link ??= new EngineLink(_ => Task.FromResult(_stream), "contract-client", ContractVectors.Channel);
        var response = await _link.RequestAsync(uri, parameters, cancellationToken).ConfigureAwait(false);
        return ClientContractDriver.Observe(response);
    }

    public async Task<StreamObservation> ObserveStreamAsync(CancellationToken cancellationToken)
    {
        await using var client = await PtyStreamClient.SubscribeAsync(
            _ => Task.FromResult(_stream),
            "contract-client",
            ContractVectors.Channel,
            ContractVectors.NookId,
            0,
            cancellationToken).ConfigureAwait(false);
        var subscription = new SubscribeResult(
            client.StreamId,
            client.BaseOffset,
            ProtocolConstants.FlowWindow,
            client.ReplayUntilOffset,
            client.TerminalModePreambleBase64,
            client.TerminalCheckpointBase64,
            client.CheckpointCols,
            client.CheckpointRows);
        var data = new List<DataObservation>();
        var resyncs = new List<ResyncObservation>();
        EndObservation? end = null;
        await client.PumpAsync(
            async (chunk, token) =>
            {
                data.Add(new DataObservation(chunk.Offset, chunk.Data.ToArray()));
                await client.AckAsync(chunk.Offset + (ulong)chunk.Data.Length, token).ConfigureAwait(false);
            },
            (resync, _) =>
            {
                resyncs.Add(ClientContractDriver.Observe(resync));
                return Task.CompletedTask;
            },
            (completed, _) =>
            {
                end = new EndObservation(completed.FinalOffset, completed.ExitCode);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
        return new StreamObservation(
            subscription,
            data,
            resyncs,
            end ?? throw new InvalidOperationException("stream ended without completion"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_link is not null)
            await _link.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class TuiClientContractDriver : IClientContractDriver
{
    private readonly FrameConnection _connection;
    private readonly AttachSession _session;

    public TuiClientContractDriver(Stream stream)
    {
        _connection = new FrameConnection(stream);
        _session = AttachCompositor.CreateSession(
            _connection,
            ContractVectors.Channel,
            ContractVectors.NookId,
            Source);
    }

    public string ControlClientKind => "tui-attach";
    public string StreamClientKind => "tui-attach";
    public string Source => "user:tui";

    public async Task<ControlObservation> RequestAsync(
        string uri,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        var response = await _session.RequestAsync(uri, parameters, cancellationToken).ConfigureAwait(false);
        return ClientContractDriver.Observe(response);
    }

    public async Task<StreamObservation> ObserveStreamAsync(CancellationToken cancellationToken)
    {
        var subscription = await _session.SubscribeAsync(cancellationToken).ConfigureAwait(false);
        var data = new List<DataObservation>();
        var resyncs = new List<ResyncObservation>();
        EndObservation? end = null;
        await _session.PumpAsync(
            (chunk, _) =>
            {
                data.Add(new DataObservation(chunk.Offset, chunk.Data.ToArray()));
                return Task.CompletedTask;
            },
            (resync, _) =>
            {
                resyncs.Add(ClientContractDriver.Observe(resync));
                return Task.CompletedTask;
            },
            (completed, _) =>
            {
                end = new EndObservation(completed.FinalOffset, completed.ExitCode);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
        return new StreamObservation(
            subscription,
            data,
            resyncs,
            end ?? throw new InvalidOperationException("stream ended without completion"));
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}

internal sealed class AttachContractSession : IAsyncDisposable
{
    private readonly FrameConnection _connection;
    private readonly AttachSession _session;

    public AttachContractSession(Stream stream, string clientKind, string source)
    {
        _connection = new FrameConnection(stream);
        _session = new AttachSession(
            _connection,
            ContractVectors.NookId,
            clientKind,
            "contract-client",
            ContractVectors.Channel,
            source);
    }

    public async Task<ControlObservation> RequestAsync(
        string uri,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        var response = await _session.RequestAsync(uri, parameters, cancellationToken).ConfigureAwait(false);
        return ClientContractDriver.Observe(response);
    }

    public async Task<StreamObservation> ObserveStreamAsync(CancellationToken cancellationToken)
    {
        var subscription = await _session.SubscribeAsync(cancellationToken).ConfigureAwait(false);
        var data = new List<DataObservation>();
        var resyncs = new List<ResyncObservation>();
        EndObservation? end = null;
        await _session.PumpAsync(
            (chunk, _) =>
            {
                data.Add(new DataObservation(chunk.Offset, chunk.Data.ToArray()));
                return Task.CompletedTask;
            },
            (resync, _) =>
            {
                resyncs.Add(ClientContractDriver.Observe(resync));
                return Task.CompletedTask;
            },
            (completed, _) =>
            {
                end = new EndObservation(completed.FinalOffset, completed.ExitCode);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
        return new StreamObservation(
            subscription,
            data,
            resyncs,
            end ?? throw new InvalidOperationException("stream ended without completion"));
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
