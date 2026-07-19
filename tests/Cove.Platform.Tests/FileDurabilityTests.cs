using Cove.Testing;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class FileDurabilityTests : IDisposable
{
    private readonly string _directory;

    public FileDurabilityTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cove-durability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        TestDirectory.Delete(_directory);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void System_SetOwnerOnly_AppliesExplicitUserReadWriteMode()
    {
        var path = Path.Combine(_directory, "state");
        File.WriteAllText(path, "state");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        FileDurability.System.SetOwnerOnly(path);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(path));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void System_FlushDirectory_CompletesForExistingDirectory()
    {
        FileDurability.System.FlushDirectory(_directory);
    }
}
