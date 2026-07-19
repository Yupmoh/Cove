using System;
using System.IO;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Cove.Testing;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class AtomicJsonStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public AtomicJsonStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cove-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        TestDirectory.Delete(_dir);
    }

    private static CoveState SampleA() => new CoveState
    {
        FocusedBay = "0192f0a0-7b2c-7e10-9a3b-2b7c4d5e6f70",
        OpenBays = new[] { "0192f0a0-7b2c-7e10-9a3b-2b7c4d5e6f70" },
        WindowGeometry = new WindowGeometry(120, 80, 1440, 900),
    };

    [Fact]
    public void Write_ThenRead_RoundTripsFields()
    {
        var state = SampleA();
        AtomicJsonStore.Write(_path, state, CoveJsonContext.Default.CoveState);
        var read = AtomicJsonStore.Read(_path, CoveJsonContext.Default.CoveState, NullLogger.Instance);
        Assert.NotNull(read);
        Assert.Equal(state.SchemaVersion, read!.SchemaVersion);
        Assert.Equal(state.FocusedBay, read.FocusedBay);
        Assert.Equal(state.OpenBays, read.OpenBays);
        Assert.Equal(state.WindowGeometry, read.WindowGeometry);
    }

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        var read = AtomicJsonStore.Read(_path, CoveJsonContext.Default.CoveState, NullLogger.Instance);
        Assert.Null(read);
    }

    [Fact]
    public void Write_SecondTime_CreatesBak()
    {
        AtomicJsonStore.Write(_path, SampleA(), CoveJsonContext.Default.CoveState);
        Assert.False(File.Exists(_path + ".bak"));
        AtomicJsonStore.Write(_path, SampleA() with { FocusedBay = "second" }, CoveJsonContext.Default.CoveState);
        Assert.True(File.Exists(_path + ".bak"));
    }

    [Fact]
    public void Read_CorruptMainFile_FallsBackToBak()
    {
        AtomicJsonStore.Write(_path, SampleA(), CoveJsonContext.Default.CoveState);
        AtomicJsonStore.Write(_path, SampleA() with { FocusedBay = "newer" }, CoveJsonContext.Default.CoveState);
        File.WriteAllText(_path, "{ not valid json ");
        var read = AtomicJsonStore.Read(_path, CoveJsonContext.Default.CoveState, NullLogger.Instance);
        Assert.NotNull(read);
        Assert.Equal("0192f0a0-7b2c-7e10-9a3b-2b7c4d5e6f70", read!.FocusedBay);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void Write_OnPosix_FileIsOwnerOnly()
    {
        AtomicJsonStore.Write(_path, SampleA(), CoveJsonContext.Default.CoveState);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(_path));
    }
}
