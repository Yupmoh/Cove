using System.Diagnostics;
using System.Security;
using Cove.Testing;
using Xunit;

namespace Cove.Architecture.Tests;

public sealed class TestingInfrastructureTests
{
    public static TheoryData<string, string, string[]> WorkflowAssemblyMaps => new()
    {
        {
            "unit",
            "macOS",
            [
                "Cove.Adapters.Tests",
                "Cove.Architecture.Tests",
                "Cove.ClientContract.Tests",
                "Cove.Cli.Tests",
                "Cove.Dictation.Tests",
                "Cove.Engine.Tests",
                "Cove.Gui.Tests",
                "Cove.Persistence.Tests",
                "Cove.Platform.Tests",
                "Cove.Protocol.Tests",
                "Cove.Pty.Harness",
                "Cove.SourceGen.Tests",
                "Cove.Tasks.Tests",
                "Cove.Tui.Tests"
            ]
        },
        {
            "unit",
            "Linux",
            [
                "Cove.Adapters.Tests",
                "Cove.Architecture.Tests",
                "Cove.ClientContract.Tests",
                "Cove.Cli.Tests",
                "Cove.Dictation.Tests",
                "Cove.Engine.Tests",
                "Cove.Gui.Tests",
                "Cove.Persistence.Tests",
                "Cove.Platform.Tests",
                "Cove.Protocol.Tests",
                "Cove.Pty.Harness",
                "Cove.SourceGen.Tests",
                "Cove.Tasks.Tests",
                "Cove.Tui.Tests"
            ]
        },
        {
            "unit",
            "Windows",
            [
                "Cove.Adapters.Tests",
                "Cove.Architecture.Tests",
                "Cove.ClientContract.Tests",
                "Cove.Cli.Tests",
                "Cove.Dictation.Tests",
                "Cove.Engine.Tests",
                "Cove.Gui.Tests",
                "Cove.Persistence.Tests",
                "Cove.Platform.Tests",
                "Cove.Protocol.Tests",
                "Cove.Pty.Harness",
                "Cove.SourceGen.Tests",
                "Cove.Tasks.Tests",
                "Cove.Tui.Tests"
            ]
        },
        {
            "platform",
            "macOS",
            [
                "Cove.Adapters.Tests",
                "Cove.Engine.Tests",
                "Cove.Gui.Tests",
                "Cove.Persistence.Tests",
                "Cove.Platform.Tests",
                "Cove.Protocol.Tests",
                "Cove.Pty.Harness"
            ]
        },
        {
            "platform",
            "Linux",
            [
                "Cove.Adapters.Tests",
                "Cove.Engine.Tests",
                "Cove.Gui.Tests",
                "Cove.Persistence.Tests",
                "Cove.Platform.Tests",
                "Cove.Protocol.Tests",
                "Cove.Pty.Harness"
            ]
        },
        {
            "platform",
            "Windows",
            [
                "Cove.Architecture.Tests",
                "Cove.Platform.Tests",
                "Cove.Pty.Harness"
            ]
        },
        {
            "live",
            "macOS",
            [
                "Cove.Adapters.Tests",
                "Cove.Dictation.Tests",
                "Cove.Engine.Tests"
            ]
        },
        {
            "live",
            "Linux",
            [
                "Cove.Adapters.Tests",
                "Cove.Engine.Tests"
            ]
        },
        {
            "live",
            "Windows",
            [
                "Cove.Engine.Tests"
            ]
        }
    };

    [PlatformFact(TestOperatingSystem.Windows)]
    public void PlatformFact_RunsOnlyOnSupportedOperatingSystem()
    {
        Assert.True(OperatingSystem.IsWindows());
    }

    [Fact]
    public void TestDirectory_CreateAndDelete_OwnsCompleteLifecycle()
    {
        var path = TestDirectory.Create("cove-testing-");
        File.WriteAllText(Path.Combine(path, "owned.txt"), "owned");

        TestDirectory.Delete(path);

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task ProcessEnvironmentScope_RestoresOriginalValue()
    {
        var variable = "COVE_TEST_SCOPE_" + Guid.NewGuid().ToString("N");
        Assert.Null(Environment.GetEnvironmentVariable(variable));

        await using (await ProcessEnvironmentScope.SetAsync(variable, "scoped"))
            Assert.Equal("scoped", Environment.GetEnvironmentVariable(variable));

        Assert.Null(Environment.GetEnvironmentVariable(variable));
    }

    [Fact]
    public async Task EventuallyAsync_ObservesAsynchronousCompletion()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producer = Task.Run(async () =>
        {
            await Task.Yield();
            completion.SetResult();
        });

        await AsyncTest.EventuallyAsync(
            () => completion.Task.IsCompletedSuccessfully,
            TimeSpan.FromSeconds(1),
            "completion was not observed");
        await producer;
    }

    [Fact]
    public async Task CompletesWithinAsync_ReturnsCompletedValue()
    {
        var value = await AsyncTest.CompletesWithinAsync(
            Task.FromResult(42),
            TimeSpan.FromSeconds(1),
            "completed task timed out");

        Assert.Equal(42, value);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("platform")]
    [InlineData("live")]
    public async Task WorkflowDiscoveryVerification_RejectsZeroDiscovered(string lane)
    {
        using var fixture = new WorkflowVerificationFixture(lane);
        fixture.WriteTrx("zero", null, 0, 0);

        var result = await fixture.RunAsync("macOS");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("zero tests", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("platform")]
    [InlineData("live")]
    public async Task WorkflowDiscoveryVerification_RejectsAllSkipped(string lane)
    {
        var assemblies = WorkflowAssemblyMaps
            .Single(row => (string)row[0] == lane && (string)row[1] == "macOS")[2] as string[];
        Assert.NotNull(assemblies);
        using var fixture = new WorkflowVerificationFixture(lane);
        foreach (var assembly in assemblies)
            fixture.WriteTrx(assembly, assembly, 1, 0);

        var result = await fixture.RunAsync("macOS");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("executed zero tests", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("platform")]
    [InlineData("live")]
    public async Task WorkflowDiscoveryVerification_RejectsMissingExpectedAssembly(string lane)
    {
        var assemblies = WorkflowAssemblyMaps
            .Single(row => (string)row[0] == lane && (string)row[1] == "macOS")[2] as string[];
        Assert.NotNull(assemblies);
        using var fixture = new WorkflowVerificationFixture(lane);
        foreach (var assembly in assemblies.Skip(1))
            fixture.WriteTrx(assembly, assembly, 1, 1);

        var result = await fixture.RunAsync("macOS");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(assemblies[0], result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("platform")]
    [InlineData("live")]
    public async Task WorkflowDiscoveryVerification_RejectsSkippedExpectedAssembly(string lane)
    {
        var assemblies = WorkflowAssemblyMaps
            .Single(row => (string)row[0] == lane && (string)row[1] == "macOS")[2] as string[];
        Assert.NotNull(assemblies);
        using var fixture = new WorkflowVerificationFixture(lane);
        fixture.WriteTrx(assemblies[0], assemblies[0], 1, 0);
        foreach (var assembly in assemblies.Skip(1))
            fixture.WriteTrx(assembly, assembly, 1, 1);

        var result = await fixture.RunAsync("macOS");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            $"assembly '{assemblies[0]}' executed zero tests",
            result.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(WorkflowAssemblyMaps))]
    public async Task WorkflowDiscoveryVerification_AcceptsExpectedAssemblyMap(
        string lane,
        string runnerOs,
        string[] assemblies)
    {
        using var fixture = new WorkflowVerificationFixture(lane);
        foreach (var assembly in assemblies)
            fixture.WriteTrx(assembly, assembly, 1, 1);

        var result = await fixture.RunAsync(runnerOs);

        Assert.True(result.ExitCode == 0, result.Error);
    }

    private sealed class WorkflowVerificationFixture : IDisposable
    {
        private readonly string _lane;
        private readonly string _root;

        public WorkflowVerificationFixture(string lane)
        {
            _lane = lane;
            _root = TestDirectory.Create("cove-workflow-");
            Directory.CreateDirectory(Path.Combine(_root, "artifacts", "test-results", lane));
        }

        public void WriteTrx(string name, string? assembly, int total, int executed)
        {
            var definition = assembly is null
                ? string.Empty
                : $"""
                  <TestDefinitions>
                    <UnitTest name="fixture">
                      <TestMethod codeBase="{SecurityElement.Escape(Path.Combine(_root, assembly + ".dll"))}" />
                    </UnitTest>
                  </TestDefinitions>
                  """;
            var document = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  {definition}
                  <ResultSummary outcome="Completed">
                    <Counters total="{total}" executed="{executed}" passed="{executed}" failed="0" />
                  </ResultSummary>
                </TestRun>
                """;
            File.WriteAllText(
                Path.Combine(_root, "artifacts", "test-results", _lane, name + ".trx"),
                document);
        }

        public async Task<WorkflowResult> RunAsync(string runnerOs)
        {
            var script = ExtractWorkflowScript(_lane);
            var scriptPath = Path.Combine(_root, "verify.ps1");
            File.WriteAllText(scriptPath, script);
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["COVE_CI_RUNNER_OS"] = runnerOs;

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                await process.WaitForExitAsync(deadline.Token);
            }
            catch (OperationCanceledException timeout)
            {
                Exception? cleanupFailure = null;
                try
                {
                    process.Kill(entireProcessTree: true);
                    using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(cleanupDeadline.Token);
                }
                catch (Exception cleanup)
                {
                    cleanupFailure = cleanup;
                }

                if (cleanupFailure is not null)
                    throw new AggregateException("Workflow verification timed out and PowerShell cleanup failed", timeout, cleanupFailure);
                throw new TimeoutException("Workflow verification did not complete within 20 seconds", timeout);
            }
            return new WorkflowResult(
                process.ExitCode,
                await outputTask,
                await errorTask);
        }

        public void Dispose()
        {
            TestDirectory.Delete(_root);
        }
    }

    private sealed record WorkflowResult(int ExitCode, string Output, string Error);

    private static string ExtractWorkflowScript(string lane)
    {
        var workflow = File.ReadAllLines(Path.Combine(RepositoryRoot, ".github", "workflows", "ci.yml"));
        var step = Array.FindIndex(
            workflow,
            line => line == $"      - name: Verify {lane} discovery");
        Assert.True(step >= 0, $"Workflow verification step not found for {lane}");
        var run = Array.FindIndex(workflow, step + 1, line => line == "        run: |");
        Assert.True(run >= 0, $"Workflow verification script not found for {lane}");

        return string.Join(
            Environment.NewLine,
            workflow
                .Skip(run + 1)
                .TakeWhile(line => line.Length == 0 || line.StartsWith("          ", StringComparison.Ordinal))
                .Select(line => line.Length >= 10 ? line[10..] : string.Empty));
    }

    private static string RepositoryRoot
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            Assert.True(Directory.Exists(Path.Combine(root, ".github", "workflows")));
            return root;
        }
    }
}
