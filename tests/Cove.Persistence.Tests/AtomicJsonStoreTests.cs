using System;
using System.IO;
using Cove.Persistence;
using Cove.Platform;
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
    public void WriteRawText_SecondTime_CreatesBackupWithPreviousContent()
    {
        AtomicJsonStore.WriteRawText(_path, "first");
        AtomicJsonStore.WriteRawText(_path, "second");

        Assert.Equal("second", File.ReadAllText(_path));
        Assert.Equal("first", File.ReadAllText(_path + ".bak"));
    }

    [Fact]
    public void WriteBytes_UsesInjectedDurabilityForFileAndDirectory()
    {
        var durability = new RecordingFileDurability();

        AtomicJsonStore.WriteBytes(_path, "content"u8, durability: durability);

        Assert.Single(durability.OwnerOnlyPaths);
        Assert.StartsWith(Path.Combine(_dir, ".state.json.tmp-"), durability.OwnerOnlyPaths[0], StringComparison.Ordinal);
        Assert.Equal(new[] { _dir }, durability.FlushedDirectories);
    }

    [Fact]
    public void WriteRawText_WhenReplaceFails_RemovesTemporaryFile()
    {
        Directory.CreateDirectory(_path);

        Assert.ThrowsAny<IOException>(() => AtomicJsonStore.WriteRawText(_path, "content"));

        Assert.Empty(Directory.EnumerateFiles(_dir, ".state.json.tmp-*"));
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

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void WriteRawText_OnPosix_FileAndBackupAreOwnerOnly()
    {
        AtomicJsonStore.WriteRawText(_path, "first");
        AtomicJsonStore.WriteRawText(_path, "second");

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(_path));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(_path + ".bak"));
    }

    private sealed class RecordingFileDurability : IFileDurability
    {
        public List<string> OwnerOnlyPaths { get; } = [];
        public List<string> FlushedDirectories { get; } = [];

        public void SetOwnerOnly(string path, Microsoft.Extensions.Logging.ILogger? logger = null)
            => OwnerOnlyPaths.Add(path);

        public void FlushDirectory(string path, Microsoft.Extensions.Logging.ILogger? logger = null)
            => FlushedDirectories.Add(path);
    }
}
