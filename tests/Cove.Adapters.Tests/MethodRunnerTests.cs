using System.Text.Json;
using Cove.Adapters;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class MethodRunnerTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-method-" + Guid.NewGuid().ToString("N"));

    private static string WriteScript(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/usr/bin/env bash\nset -euo pipefail\n" + content);
        if (!OperatingSystem.IsWindows())
            System.IO.File.SetUnixFileMode(path, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
        return path;
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task RunAsync_ParsesJsonStdout_OnExit0()
    {
        var dir = NewDir();
        try
        {
            WriteScript(dir, "test.sh", "echo '{\"command\":[\"claude\"]}'");
            var runner = new MethodRunner();
            var result = await runner.RunAsync(dir, "test.sh", [], TimeSpan.FromSeconds(3));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(result.Json);
            Assert.True(result.Json!.Value.TryGetProperty("command", out var cmd));
            Assert.Equal("claude", cmd[0].GetString());
        }
        finally { TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task RunAsync_Exit1_IsGracefulFailure_NotCrash()
    {
        var dir = NewDir();
        try
        {
            WriteScript(dir, "fail.sh", "echo 'not found' >&2; exit 1");
            var runner = new MethodRunner();
            var result = await runner.RunAsync(dir, "fail.sh", [], TimeSpan.FromSeconds(3));

            Assert.Equal(1, result.ExitCode);
            Assert.Null(result.Json);
            Assert.Contains("not found", result.Stderr);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task RunAsync_Exit2_IsError_LoggedToStderr()
    {
        var dir = NewDir();
        try
        {
            WriteScript(dir, "err.sh", "echo 'boom' >&2; exit 2");
            var runner = new MethodRunner();
            var result = await runner.RunAsync(dir, "err.sh", [], TimeSpan.FromSeconds(3));

            Assert.Equal(2, result.ExitCode);
            Assert.Null(result.Json);
            Assert.Contains("boom", result.Stderr);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task RunAsync_NonJsonStdout_ReportsNotCrash()
    {
        var dir = NewDir();
        try
        {
            WriteScript(dir, "badjson.sh", "echo 'this is not json'");
            var runner = new MethodRunner();
            var result = await runner.RunAsync(dir, "badjson.sh", [], TimeSpan.FromSeconds(3));

            Assert.Equal(0, result.ExitCode);
            Assert.Null(result.Json);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task RunAsync_HangingScript_KilledAtTimeout()
    {
        var dir = NewDir();
        try
        {
            WriteScript(dir, "hang.sh", "sleep 30");
            var runner = new MethodRunner();
            var result = await runner.RunAsync(dir, "hang.sh", [], TimeSpan.FromSeconds(1));

            Assert.Equal(-1, result.ExitCode);
            Assert.Null(result.Json);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task RunAsync_SetsMethodEnvContract()
    {
        var dir = NewDir();
        try
        {
            WriteScript(dir, "env.sh", "echo \"{\\\"coveAdapterDir\\\":\\\"$COVE_ADAPTER_DIR\\\",\\\"sdkVersion\\\":\\\"$COVE_SDK_VERSION\\\"}\"");
            var runner = new MethodRunner();
            var env = new Dictionary<string, string>
            {
                ["COVE_ADAPTER_DIR"] = dir,
                ["COVE_DATA_DIR"] = "/tmp/.cove",
                ["COVE_HOOK_PORT"] = "12345",
                ["COVE_SDK_VERSION"] = "2",
            };
            var result = await runner.RunAsync(dir, "env.sh", [], TimeSpan.FromSeconds(3), env);

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(result.Json);
            Assert.Equal(dir, result.Json!.Value.GetProperty("coveAdapterDir").GetString());
            Assert.Equal("2", result.Json!.Value.GetProperty("sdkVersion").GetString());
        }
        finally { TestDirectory.Delete(dir); }
    }
}
