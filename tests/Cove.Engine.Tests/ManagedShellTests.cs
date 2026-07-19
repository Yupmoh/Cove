using System.IO;
using Cove.Engine.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class ManagedShellTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-shell-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void GenerateEnvSh_CreatesFileWithExports()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var content = File.ReadAllText(svc.EnvShPath);
            Assert.Contains("export COVE_DATA_DIR=", content);
            Assert.Contains("export COVE_CLI_PATH=", content);
            Assert.Contains("export __COVE_MANAGED_KEYS=", content);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void GenerateEnvSh_ContainsIdempotentPathAddition()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var content = File.ReadAllText(svc.EnvShPath);
            Assert.Contains("case \":$PATH:\"", content);
            Assert.Contains("export PATH=", content);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void GenerateEnvSh_CreatesFishCompatibleFile()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            Assert.True(File.Exists(svc.EnvFishPath));
            var content = File.ReadAllText(svc.EnvFishPath);
            Assert.Contains("set -x COVE_DATA_DIR", content);
            Assert.Contains("set -x COVE_CLI_PATH", content);
            Assert.DoesNotContain("export ", content);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void ReinstallBinCove_CopiesCliWhenMissing()
    {
        var dir = NewDir();
        var fakeCli = Path.Combine(dir, "fake-cove");
        Directory.CreateDirectory(dir);
        File.WriteAllText(fakeCli, "#!/bin/sh\necho fake");
        try
        {
            var svc = new ManagedShellService(dir, fakeCli, NullLogger.Instance);
            svc.ReinstallBinCove();
            Assert.True(File.Exists(svc.BinCovePath));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void ReinstallBinCove_SkipsWhenVersionMatches()
    {
        var dir = NewDir();
        var fakeCli = Path.Combine(dir, "fake-cove");
        Directory.CreateDirectory(dir);
        File.WriteAllText(fakeCli, "first");
        try
        {
            var svc = new ManagedShellService(dir, fakeCli, NullLogger.Instance);
            svc.ReinstallBinCove();
            var firstContent = File.ReadAllText(svc.BinCovePath);
            File.WriteAllText(fakeCli, "second");
            svc.ReinstallBinCove();
            var secondContent = File.ReadAllText(svc.BinCovePath);
            Assert.Equal(firstContent, secondContent);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void ReinstallBinCove_ReinstallsOnVersionMismatch()
    {
        var dir = NewDir();
        var fakeCli = Path.Combine(dir, "fake-cove");
        Directory.CreateDirectory(dir);
        File.WriteAllText(fakeCli, "first");
        Directory.CreateDirectory(Path.Combine(dir, "bin"));
        File.WriteAllText(Path.Combine(dir, "bin", ".cove-version"), "0.0.0-old");
        try
        {
            var svc = new ManagedShellService(dir, fakeCli, NullLogger.Instance);
            svc.ReinstallBinCove();
            Assert.Contains("first", File.ReadAllText(svc.BinCovePath));
            Assert.Equal(Cove.Platform.CoveBuild.InformationalVersion, File.ReadAllText(Path.Combine(dir, "bin", ".cove-version")).Trim());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void ReinstallBinCove_LogsWhenCliMissing()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/nonexistent/cove", NullLogger.Instance);
            svc.ReinstallBinCove();
            Assert.False(File.Exists(svc.BinCovePath));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void EnvSh_SourcesInBash()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var (exit, output) = RunShell("bash", svc.EnvShPath, "echo $COVE_DATA_DIR");
            Assert.Equal(0, exit);
            Assert.Contains(dir, output);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "zsh")]
    public void EnvSh_SourcesInZsh()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var (exit, output) = RunShell("zsh", svc.EnvShPath, "echo $COVE_DATA_DIR");
            Assert.Equal(0, exit);
            Assert.Contains(dir, output);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "fish")]
    public void EnvFish_SourcesInFish()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var (exit, output) = RunShell("fish", svc.EnvFishPath, "echo $COVE_DATA_DIR");
            Assert.Equal(0, exit);
            Assert.Contains(dir, output);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    private static (int exit, string output) RunShell(string shell, string envFile, string echoCmd)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(shell) { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-c");
        if (shell == "fish")
            psi.ArgumentList.Add($"source \"{envFile}\"; {echoCmd}");
        else
            psi.ArgumentList.Add($". \"{envFile}\"; {echoCmd}");
        using var p = System.Diagnostics.Process.Start(psi)!;
        Assert.True(p.WaitForExit(5000), $"{shell} did not exit within 5 seconds");
        return (p.ExitCode, p.StandardOutput.ReadToEnd().Trim());
    }
}
