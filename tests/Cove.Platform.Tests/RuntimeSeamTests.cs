using Cove.Platform;
using Cove.Testing;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class RuntimeSeamTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task SystemProcessRunner_CapturesArgumentsEnvironmentAndOutput()
    {
        var runner = new SystemProcessRunner();
        var result = await runner.RunAsync(new ProcessRunRequest(
            "/bin/sh",
            "/",
            ["-c", "printf '%s:%s' \"$1\" \"$COVE_TEST_VALUE\"", "sh", "argument with spaces"],
            new Dictionary<string, string> { ["COVE_TEST_VALUE"] = "environment value" },
            TimeSpan.FromSeconds(3)));

        Assert.True(result.Started);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("argument with spaces:environment value", result.Stdout);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task SystemProcessRunner_TerminatesTimedOutProcess()
    {
        var runner = new SystemProcessRunner();
        var result = await runner.RunAsync(new ProcessRunRequest(
            "/bin/sh",
            "/",
            ["-c", "sleep 30"],
            null,
            TimeSpan.FromMilliseconds(50)));

        Assert.True(result.Started);
        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void SystemExecutableMode_PreservesExistingBitsAndAddsUserExecute()
    {
        var path = Path.Combine(Path.GetTempPath(), "cove-mode-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "data");
            var initial = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
            File.SetUnixFileMode(path, initial);

            new SystemExecutableMode().MakeUserExecutable(path);

            var actual = File.GetUnixFileMode(path);
            Assert.Equal(initial | UnixFileMode.UserExecute, actual);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
