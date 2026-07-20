using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Cove.Engine.Bays;
using Cove.Gui;
using Cove.Persistence;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ControlPlaneEndToEndLiveTests
{
    [ExternalFact(TestOperatingSystem.MacOS, "bash")]
    public Task MacOS_GuiAndAgentCli_ReconcileLaunchMessageRestartReconnectAndScope() =>
        RunAsync();

    [ExternalFact(TestOperatingSystem.Windows, "bash")]
    public Task Windows_GuiAndAgentCli_ReconcileLaunchMessageRestartReconnectAndScope() =>
        RunAsync();

    private static async Task RunAsync()
    {
        var root = TestDirectory.Create(
            "cove-control-plane-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        try
        {
            InstallAdapter(root);
            await using var daemon = await DaemonTestHarness.StartAsync(dataDir: root);
            await using var gui = new EngineLink(
                ct => daemon.Endpoint.ConnectAsync(5000, ct).AsTask(),
                "0.1.0",
                "dev");
            var events = new List<string>();
            var eventGate = new object();
            gui.SetEngineEventHandler((channel, _) =>
            {
                lock (eventGate)
                    events.Add(channel);
            });

            var bayOne = await CreateBayAsync(gui, "Bay One", root);
            var agentA = await SpawnAsync(
                gui,
                LongRunningCommand(),
                LongRunningArgs(writeToken: true),
                root,
                bayOne,
                "Agent A");
            var shoreOne = await CreateShoreAsync(gui, agentA, "Main");
            var agentAToken = await ReadTokenAsync(gui, agentA);
            lock (eventGate)
                events.Clear();

            var resumed = await RunAgentCliAsync(
                daemon.DataDir,
                agentA,
                agentAToken,
                "agent",
                "resume",
                "control-test",
                "session-previous",
                "--relative-to",
                agentA,
                "--placement",
                "right",
                "--cwd",
                root,
                "--name",
                "Agent B",
                "--access-scope",
                "same-bay",
                "--json");
            Assert.Equal(0, resumed.ExitCode);
            var agentB = JsonString(resumed.Stdout, "nookId");
            Assert.True(JsonBool(resumed.Stdout, "resumed"));
            await WaitForWorkspaceEventAsync(events, eventGate);
            await AssertPlacementAsync(gui, bayOne, shoreOne, agentA, agentB);
            var agentBToken = await ReadTokenAsync(gui, agentB);

            var message = await RunAgentCliAsync(
                daemon.DataDir,
                agentA,
                agentAToken,
                "agent",
                "message",
                agentB,
                "Please review the control plane.",
                "--submit-pause-ms",
                "0",
                "--json");
            Assert.Equal(0, message.ExitCode);
            lock (eventGate)
                events.Clear();
            var needsInput = await RunAgentCliAsync(
                daemon.DataDir,
                agentB,
                agentBToken,
                "hook",
                "emit",
                "notification",
                "--adapter",
                "control-test",
                "--nook-id",
                agentB);
            Assert.Equal(0, needsInput.ExitCode);
            await WaitForEventAsync(events, eventGate, "agent.changed");
            try
            {
                await AsyncTest.EventuallyAsync(
                    async () => await AgentStatusAsync(daemon.DataDir, agentA, agentAToken, agentB) == "needs-input",
                    TimeSpan.FromSeconds(15),
                    "Agent B did not report needs-input");
            }
            catch (TimeoutException exception)
            {
                var output = await ReadOutputAsync(gui, agentB);
                var listing = await RunAgentCliAsync(
                    daemon.DataDir,
                    agentA,
                    agentAToken,
                    "agent",
                    "list",
                    "--scope",
                    "same-tab",
                    "--json");
                throw new TimeoutException(
                    $"{exception.Message}; agentOutput={output}; listOut={listing.Stdout}; listErr={listing.Stderr}",
                    exception);
            }
            Assert.Equal("needs-input", await GuiAgentStatusAsync(gui, agentB));

            var restarted = await RunAgentCliAsync(
                daemon.DataDir,
                agentA,
                agentAToken,
                "nook",
                "restart",
                agentB,
                "--mode",
                "resume-current",
                "--resume-fallback",
                "fresh",
                "--json");
            Assert.Equal(0, restarted.ExitCode);
            Assert.Equal(agentB, JsonString(restarted.Stdout, "nookId"));
            Assert.Equal("resumed", JsonString(restarted.Stdout, "outcome"));
            await AssertPlacementAsync(gui, bayOne, shoreOne, agentA, agentB);

            var launched = await RunAgentCliAsync(
                daemon.DataDir,
                agentA,
                agentAToken,
                "agent",
                "launch",
                "control-test",
                "--relative-to",
                agentA,
                "--placement",
                "below",
                "--cwd",
                root,
                "--name",
                "Agent C",
                "--access-scope",
                "same-bay",
                "--json");
            Assert.True(launched.ExitCode == 0, launched.Stderr + launched.Stdout);
            Assert.False(JsonBool(launched.Stdout, "resumed"));

            var beforeRollback = await NookIdsAsync(gui);
            var missingTarget = await RunAgentCliAsync(
                daemon.DataDir,
                agentA,
                agentAToken,
                "agent",
                "launch",
                "control-test",
                "--relative-to",
                "nook-missing",
                "--placement",
                "right",
                "--cwd",
                root,
                "--json");
            Assert.NotEqual(0, missingTarget.ExitCode);
            Assert.Equal(beforeRollback, await NookIdsAsync(gui));

            var bayTwo = await CreateBayAsync(gui, "Bay Two", root);
            var bayTwoAnchor = await SpawnAsync(
                gui,
                LongRunningCommand(),
                LongRunningArgs(writeToken: false),
                root,
                bayTwo,
                "Bay Two Anchor");
            await CreateShoreAsync(gui, bayTwoAnchor, "Other");
            var beforeDenied = await NookIdsAsync(gui);
            var denied = await RunAgentCliAsync(
                daemon.DataDir,
                agentA,
                agentAToken,
                "agent",
                "launch",
                "control-test",
                "--relative-to",
                bayTwoAnchor,
                "--placement",
                "right",
                "--bay-id",
                bayTwo,
                "--cwd",
                root,
                "--json");
            Assert.NotEqual(0, denied.ExitCode);
            Assert.Contains("access_denied", denied.Stderr + denied.Stdout, StringComparison.Ordinal);
            Assert.Equal(beforeDenied, await NookIdsAsync(gui));

            var reconnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            gui.SetEngineEventHandler((channel, _) =>
            {
                if (channel == "engine.reconnected")
                    reconnect.TrySetResult();
            });
            await daemon.RestartAsync();
            await AsyncTest.EventuallyAsync(
                async () =>
                {
                    try
                    {
                        return (await gui.RequestAsync(
                            "cove://commands/layout.get",
                            JsonSerializer.SerializeToElement(
                                new LayoutGetParams(bayOne),
                                Cove.Protocol.CoveJsonContext.Default.LayoutGetParams),
                            CancellationToken.None)).Ok;
                    }
                    catch (IOException)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15),
                "GUI did not reconnect");
            await reconnect.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await AssertShoreContainsAsync(gui, bayOne, shoreOne, agentA, agentB);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void InstallAdapter(string root)
    {
        var directory = Path.Combine(root, "adapters", "control-test");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "adapter.json"),
            """
            {
              "sdkVersion": 2,
              "name": "control-test",
              "displayName": "Control Test",
              "description": "Control plane acceptance adapter",
              "accent": "#3b82f6",
              "binary": "control-test",
              "version": "1.0.0",
              "icon": "icon.svg",
              "binaryDiscovery": {
                "commands": ["control-test"],
                "wellKnownPaths": []
              },
              "hooks": {
                "session-start": "cove://hooks/control-test/session-start",
                "session-end": "cove://hooks/control-test/session-end",
                "pre-tool-use": "cove://hooks/control-test/pre-tool-use",
                "post-tool-use": "cove://hooks/control-test/post-tool-use",
                "stop": "cove://hooks/control-test/stop",
                "user-prompt-submit": "cove://hooks/control-test/user-prompt-submit",
                "permission-request": "cove://hooks/control-test/permission-request"
              },
              "methods": {
                "build_launch_command": { "script": "build_launch_command.sh" },
                "build_resume_command": { "script": "build_resume_command.sh" }
              }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "icon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        File.WriteAllText(
            Path.Combine(directory, "agent.sh"),
            $$"""
            #!/usr/bin/env bash
            set -euo pipefail
            printf 'TOKEN=%s\n' "$COVE_NOOK_TOKEN"
            sleep 120
            """);
        var commandBuilder =
            """
            #!/usr/bin/env bash
            set -euo pipefail
            if [ "${OS:-}" = "Windows_NT" ]; then
              printf '%s\n' '{"command":["cmd.exe","/d","/s","/c","echo TOKEN=%COVE_NOOK_TOKEN% & ping -n 120 127.0.0.1 > nul"]}'
            else
              printf '{"command":["/bin/bash","%s/agent.sh"]}\n' "$COVE_ADAPTER_DIR"
            fi
            """;
        File.WriteAllText(Path.Combine(directory, "build_launch_command.sh"), commandBuilder);
        File.WriteAllText(Path.Combine(directory, "build_resume_command.sh"), commandBuilder);
        if (!OperatingSystem.IsWindows())
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.sh"))
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static async Task<string> CreateBayAsync(EngineLink gui, string name, string projectDir)
    {
        var response = await gui.RequestAsync(
            "cove://commands/bay.create",
            JsonSerializer.SerializeToElement(
                new BayCreateParams(name, projectDir),
                BaysJsonContext.Default.BayCreateParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        return response.Data!.Value.GetProperty("id").GetString()!;
    }

    private static async Task<string> SpawnAsync(
        EngineLink gui,
        string command,
        string[] args,
        string cwd,
        string bayId,
        string name)
    {
        var response = await gui.RequestAsync(
            "cove://commands/nook.spawn",
            JsonSerializer.SerializeToElement(
                new SpawnParams(
                    command,
                    args,
                    cwd,
                    Adapter: "control-test",
                    AgentName: name,
                    Bay: bayId,
                    McpAccessScope: "same-bay"),
                Cove.Protocol.CoveJsonContext.Default.SpawnParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        return response.Data!.Value.GetProperty("nookId").GetString()!;
    }

    private static async Task<string> CreateShoreAsync(EngineLink gui, string nookId, string name)
    {
        var response = await gui.RequestAsync(
            "cove://commands/layout.mutate",
            JsonSerializer.SerializeToElement(
                new LayoutMutateParams("createShore", NewNookId: nookId, Name: name),
                Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        return response.Data!.Value.GetProperty("shoreId").GetString()!;
    }

    private static async Task<string> ReadTokenAsync(EngineLink gui, string nookId)
    {
        string? token = null;
        await AsyncTest.EventuallyAsync(
            async () =>
            {
                var response = await gui.RequestAsync(
                    "cove://commands/nook.read",
                    JsonSerializer.SerializeToElement(
                        new NookReadParams(nookId),
                        Cove.Protocol.CoveJsonContext.Default.NookReadParams),
                    CancellationToken.None);
                Assert.True(response.Ok, response.Error?.Message);
                var payload = response.Data!.Value.Deserialize(
                    Cove.Protocol.CoveJsonContext.Default.NookReadResult)!;
                var output = Encoding.UTF8.GetString(Convert.FromBase64String(payload.DataBase64));
                var marker = output.IndexOf("TOKEN=", StringComparison.Ordinal);
                if (marker < 0)
                    return false;
                var end = output.IndexOfAny(['\r', '\n'], marker);
                token = output[(marker + 6)..(end < 0 ? output.Length : end)];
                return token.Length > 0;
            },
            TimeSpan.FromSeconds(10),
            "Agent A token was not written");
        return token!;
    }

    private static async Task<string> ReadOutputAsync(EngineLink gui, string nookId)
    {
        var response = await gui.RequestAsync(
            "cove://commands/nook.read",
            JsonSerializer.SerializeToElement(
                new NookReadParams(nookId),
                Cove.Protocol.CoveJsonContext.Default.NookReadParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        var payload = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookReadResult)!;
        return Encoding.UTF8.GetString(Convert.FromBase64String(payload.DataBase64));
    }

    private static async Task AssertPlacementAsync(
        EngineLink gui,
        string bayId,
        string shoreId,
        string leftNookId,
        string rightNookId)
    {
        var response = await gui.RequestAsync(
            "cove://commands/layout.get",
            JsonSerializer.SerializeToElement(
                new LayoutGetParams(bayId),
                Cove.Protocol.CoveJsonContext.Default.LayoutGetParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        var snapshot = response.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot)!;
        var shore = Assert.Single(snapshot.Shores, item => item.Id == shoreId);
        var split = Assert.IsType<SplitNode>(shore.LayoutTree);
        Assert.Equal(leftNookId, Assert.IsType<NookLeaf>(split.ChildA).NookId);
        Assert.Equal(rightNookId, Assert.IsType<NookLeaf>(split.ChildB).NookId);
    }

    private static async Task AssertShoreContainsAsync(
        EngineLink gui,
        string bayId,
        string shoreId,
        params string[] nookIds)
    {
        var response = await gui.RequestAsync(
            "cove://commands/layout.get",
            JsonSerializer.SerializeToElement(
                new LayoutGetParams(bayId),
                Cove.Protocol.CoveJsonContext.Default.LayoutGetParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        var snapshot = response.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot)!;
        var shore = Assert.Single(snapshot.Shores, item => item.Id == shoreId);
        var actual = LeafIds(shore.LayoutTree).ToHashSet(StringComparer.Ordinal);
        Assert.All(nookIds, nookId => Assert.Contains(nookId, actual));
    }

    private static IEnumerable<string> LeafIds(MosaicNode node)
    {
        if (node is NookLeaf leaf)
        {
            yield return leaf.NookId;
            yield break;
        }
        var split = Assert.IsType<SplitNode>(node);
        foreach (var nookId in LeafIds(split.ChildA))
            yield return nookId;
        foreach (var nookId in LeafIds(split.ChildB))
            yield return nookId;
    }

    private static async Task<string[]> NookIdsAsync(EngineLink gui)
    {
        var response = await gui.RequestAsync("cove://commands/nook.list", null, CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        return response.Data!.Value
            .Deserialize(Cove.Protocol.CoveJsonContext.Default.NookListResult)!
            .Nooks.Select(nook => nook.NookId).Order().ToArray();
    }

    private static async Task<string?> AgentStatusAsync(
        string dataDir,
        string nookId,
        string nookToken,
        string targetNookId)
    {
        var result = await RunAgentCliAsync(
            dataDir,
            nookId,
            nookToken,
            "agent",
            "list",
            "--scope",
            "same-tab",
            "--json");
        if (result.ExitCode != 0)
            return null;
        using var document = JsonDocument.Parse(result.Stdout);
        return document.RootElement.GetProperty("agents")
            .EnumerateArray()
            .FirstOrDefault(agent => agent.GetProperty("nookId").GetString() == targetNookId)
            .GetProperty("status")
            .GetString();
    }

    private static async Task WaitForWorkspaceEventAsync(List<string> events, object eventGate)
        => await WaitForEventAsync(events, eventGate, "workspace.changed");

    private static async Task WaitForEventAsync(
        List<string> events,
        object eventGate,
        string expected)
    {
        await AsyncTest.EventuallyAsync(
            () =>
            {
                lock (eventGate)
                    return events.Contains(expected, StringComparer.Ordinal);
            },
            TimeSpan.FromSeconds(10),
            $"GUI did not receive {expected}");
    }

    private static async Task<string?> GuiAgentStatusAsync(EngineLink gui, string nookId)
    {
        var response = await gui.RequestAsync(
            "cove://commands/agent.list",
            JsonSerializer.SerializeToElement(
                new AgentListParams("all"),
                Cove.Protocol.CoveJsonContext.Default.AgentListParams),
            CancellationToken.None);
        Assert.True(response.Ok, response.Error?.Message);
        return response.Data!.Value
            .Deserialize(Cove.Protocol.CoveJsonContext.Default.AgentListResult)!
            .Agents.Single(agent => agent.NookId == nookId).Status;
    }

    private static async Task<ProcessResult> RunAgentCliAsync(
        string dataDir,
        string nookId,
        string nookToken,
        params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(CliAssemblyPath());
        startInfo.ArgumentList.Add("--channel");
        startInfo.ArgumentList.Add("dev");
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
        startInfo.Environment["COVE_DATA_DIR"] = dataDir;
        startInfo.Environment["COVE_NOOK_ID"] = nookId;
        startInfo.Environment["COVE_NOOK_TOKEN"] = nookToken;
        using var process = Process.Start(startInfo)!;
        await process.StandardInput.WriteAsync("{}");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string LongRunningCommand() =>
        OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";

    private static string[] LongRunningArgs(bool writeToken) =>
        OperatingSystem.IsWindows()
            ?
            [
                "/d",
                "/s",
                "/c",
                writeToken
                    ? "echo TOKEN=%COVE_NOOK_TOKEN% & ping -n 120 127.0.0.1 > nul"
                    : "ping -n 120 127.0.0.1 > nul"
            ]
            :
            [
                "-c",
                writeToken
                    ? "printf 'TOKEN=%s\\n' \"$COVE_NOOK_TOKEN\"; sleep 120"
                    : "sleep 120"
            ];

    private static string JsonString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).GetString()!;
    }

    private static bool JsonBool(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).GetBoolean();
    }

    private static string CliAssemblyPath()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new DirectoryNotFoundException("test output configuration not found");
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "Cove.Cli",
                "bin",
                configuration,
                "net10.0",
                "cove.dll");
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException("cove CLI assembly not found");
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
