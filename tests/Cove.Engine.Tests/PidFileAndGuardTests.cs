using System;
using System.IO;
using Cove.Engine.Daemon;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PidFileAndGuardTests
{
    private static string TempPidPath() =>
        Path.Combine(Path.GetTempPath(), "cove-pid-" + Guid.NewGuid().ToString("N") + ".pid");

    [Fact]
    public void Guard_WritePid_ProducesBarePidNewline()
    {
        string path = TempPidPath();
        try
        {
            using SingleInstanceGuard? guard = SingleInstanceGuard.TryAcquire(path);
            Assert.NotNull(guard);
            guard!.WritePid(43127);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
                Assert.Equal("43127\n", sr.ReadToEnd());
            Assert.Equal(43127, PidFile.Read(path));
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Guard_SecondAcquire_FailsWhileHeld_ThenSucceedsAfterDispose()
    {
        if (OperatingSystem.IsWindows())
            return;
        string path = TempPidPath();
        try
        {
            SingleInstanceGuard? first = SingleInstanceGuard.TryAcquire(path);
            Assert.NotNull(first);
            SingleInstanceGuard? second = SingleInstanceGuard.TryAcquire(path);
            Assert.Null(second);
            first!.Dispose();
            SingleInstanceGuard? third = SingleInstanceGuard.TryAcquire(path);
            Assert.NotNull(third);
            third!.Dispose();
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void PidFile_Read_MissingFile_ReturnsNull()
    {
        Assert.Null(PidFile.Read(Path.Combine(Path.GetTempPath(), "cove-missing-" + Guid.NewGuid().ToString("N"))));
    }
}
