using Cove.Cli;
using Cove.Generated;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class CommandsCatalogueTests
{
    [Fact]
    public void Catalogue_IsNotEmpty()
    {
        Assert.NotEmpty(CoveCommandRegistry.Catalogue);
    }

    [Fact]
    public void Catalogue_EveryEntryHasCommandAndSource()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
        {
            Assert.False(string.IsNullOrEmpty(entry.Command));
            Assert.False(string.IsNullOrEmpty(entry.Source));
        }
    }

    [Fact]
    public void Catalogue_SourceIsCliOrCore()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
            Assert.Contains(entry.Source, new[] { "cli", "core", "extension" });
    }

    [Fact]
    public void Catalogue_CoreEntriesAreCoveUri()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
            if (entry.Source == "core")
                Assert.StartsWith("cove://", entry.Command);
    }

    [Fact]
    public void Catalogue_CliEntriesAreSpaceSeparated()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
            if (entry.Source == "cli")
                Assert.False(entry.Command.StartsWith("cove://"));
    }

    [Fact]
    public void Catalogue_CountMatchesKeys()
    {
        Assert.Equal(CoveCommandRegistry.Keys.Count, CoveCommandRegistry.Catalogue.Count);
    }

    [Fact]
    public void Catalogue_HasNoDuplicateCommandKeys()
    {
        var duplicateKeys = CoveCommandRegistry.Catalogue
            .GroupBy(entry => entry.Command, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicateKeys);
    }

    [Fact]
    public void CombinedCliAndEngineCatalogues_HaveUniqueKeys()
    {
        var duplicateKeys = CoveCommandRegistry.Keys
            .Concat(Cove.Engine.EngineCommandCatalogue.RegisteredRoutes)
            .GroupBy(command => command, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicateKeys);
    }

    [Fact]
    public void CliHandlers_AreOwnedByFeatureSlices()
    {
        Assert.Equal("VersionCommands", HandlerOwner("version"));
        Assert.Equal("NookCommands", HandlerOwner("attach"));
        Assert.Equal("LaunchProfileCommands", HandlerOwner("launch-profile list"));
        Assert.Equal("TaskCommands", HandlerOwner("task list"));
        Assert.Equal("NoteCommands", HandlerOwner("note list"));
        Assert.Equal("DiagnosticsCommands", HandlerOwner("diagnostics status"));

        var largestOwner = CoveCommandRegistry.Handlers
            .Where(entry => !entry.Key.StartsWith("cove://", StringComparison.Ordinal))
            .GroupBy(entry => entry.Value.Method.DeclaringType)
            .Max(group => group.Count());

        Assert.InRange(largestOwner, 1, 32);
    }

    [Fact]
    public void Catalogue_AttachCliVerbPresent()
    {
        Assert.Contains(CoveCommandRegistry.Catalogue, e => e.Command == "attach" && e.Source == "cli");
    }

    [Fact]
    public void Catalogue_WorkspaceContextCliVerbPresent()
    {
        Assert.Contains(
            CoveCommandRegistry.Catalogue,
            entry => entry.Command == "workspace context"
                && entry.Source == "cli");
        Assert.Equal(
            "WorkspaceCommands",
            HandlerOwner("workspace context"));
    }

    [Fact]
    public async Task Commands_UsesDaemonExtensionCatalogueAndPreservesJsonOrdering()
    {
        var root = TestDirectory.Create("cc-");
        try
        {
            var result = await InvokeAgainstDaemonAsync(
                root,
                ["--json"],
                request =>
                {
                    using var document = System.Text.Json.JsonDocument.Parse(
                        """
                        [{"command":"extension.runtime-only.execute","description":"Runtime only","source":"extension","adapter":"runtime-only","method":"execute"}]
                        """);
                    return new ControlResponse(
                        request.Id,
                        true,
                        document.RootElement.Clone());
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/extension.list", result.Request.Uri);
            Assert.Equal("", result.Stderr);
            using var output = System.Text.Json.JsonDocument.Parse(result.Stdout);
            var entries = output.RootElement.EnumerateArray().ToArray();
            var runtimeEntry = Assert.Single(
                entries,
                entry => entry.GetProperty("command").GetString()
                    == "extension.runtime-only.execute");
            Assert.Equal("Runtime only", runtimeEntry.GetProperty("description").GetString());
            Assert.Equal("extension", runtimeEntry.GetProperty("source").GetString());
            Assert.Equal(
                "extension.runtime-only.execute",
                entries[^1].GetProperty("command").GetString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public async Task Commands_TextOutputIncludesDaemonExtensionCatalogue()
    {
        var root = TestDirectory.Create("cc-");
        try
        {
            var result = await InvokeAgainstDaemonAsync(
                root,
                [],
                request =>
                {
                    using var document = System.Text.Json.JsonDocument.Parse(
                        """
                        [{"command":"extension.runtime-only.execute","source":"extension","adapter":"runtime-only","method":"execute"}]
                        """);
                    return new ControlResponse(
                        request.Id,
                        true,
                        document.RootElement.Clone());
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/extension.list", result.Request.Uri);
            Assert.Contains(
                "  [extension] extension.runtime-only.execute" + Environment.NewLine,
                result.Stdout);
            Assert.StartsWith("Commands:" + Environment.NewLine, result.Stdout);
            Assert.Contains("Total: ", result.Stdout);
            Assert.Equal("", result.Stderr);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public async Task Commands_NoAutostartWhenDisconnectedReturnsNotConnected()
    {
        var root = TestDirectory.Create("cc-");
        try
        {
            var paths = new Cove.Engine.Daemon.DaemonPaths(
                Cove.Platform.CoveDataDir.ForRoot(
                    Cove.Platform.CoveChannel.Stable,
                    root));
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(
                paths.DataDir.SocketPath);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var context = new CommandContext(
                paths,
                endpoint,
                stdout,
                stderr,
                ["--no-autostart"]);

            var exitCode = await CliDocumentationCommands.Commands(context);

            Assert.Equal(1, exitCode);
            Assert.Equal("", stdout.ToString());
            Assert.Equal("[not_connected]" + Environment.NewLine, stderr.ToString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    private static async Task<(
        int ExitCode,
        ControlRequest Request,
        string Stdout,
        string Stderr)> InvokeAgainstDaemonAsync(
        string root,
        string[] args,
        Func<ControlRequest, ControlResponse> respond)
    {
        var paths = new Cove.Engine.Daemon.DaemonPaths(
            Cove.Platform.CoveDataDir.ForRoot(
                Cove.Platform.CoveChannel.Stable,
                root));
        var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(
            paths.DataDir.SocketPath);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DataDir.SocketPath)!);
        File.WriteAllText(
            paths.ControlTokenPath,
            "catalogue-test-control-token");
        await using var listener = endpoint.Bind();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            await using var stream = await listener.AcceptAsync(cts.Token);
            await using var connection = new FrameConnection(stream);
            var hello = (await connection.ReadFrameAsync(cts.Token))!.Value;
            var helloRequest = ControlCodec.DecodeRequest(hello.Payload);
            await connection.WriteFrameAsync(
                FrameType.Response,
                0,
                ControlCodec.Encode(new ControlResponse(helloRequest.Id, true)),
                cts.Token);
            var frame = (await connection.ReadFrameAsync(cts.Token))!.Value;
            var request = ControlCodec.DecodeRequest(frame.Payload);
            await connection.WriteFrameAsync(
                FrameType.Response,
                0,
                ControlCodec.Encode(respond(request)),
                cts.Token);
            return request;
        }, cts.Token);
        using var process = new System.Diagnostics.Process
        {
            StartInfo = CreateCommandsStartInfo(root, args)
        };
        Assert.True(process.Start());
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (await Task.WhenAny(server, Task.Delay(TimeSpan.FromSeconds(2))) != server)
        {
            cts.Cancel();
            try
            {
                await server;
            }
            catch (OperationCanceledException)
            {
            }
            throw new Xunit.Sdk.XunitException(
                "commands did not request the daemon extension catalogue");
        }
        var request = await server;

        return (process.ExitCode, request, stdout, stderr);
    }

    private static System.Diagnostics.ProcessStartInfo CreateCommandsStartInfo(
        string root,
        string[] args)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(typeof(CommandContext).Assembly.Location);
        startInfo.ArgumentList.Add("commands");
        foreach (var argument in args)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["COVE_DATA_DIR"] = root;
        return startInfo;
    }

    private static string? HandlerOwner(string command)
        => CoveCommandRegistry.Handlers[command].Method.DeclaringType?.Name;
}
