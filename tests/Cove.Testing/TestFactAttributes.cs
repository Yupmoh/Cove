using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Cove.Testing;

public enum TestOperatingSystem
{
    Any,
    Windows,
    MacOS,
    Linux,
    Unix
}

[TraitDiscoverer("Cove.Testing.PlatformCategoryDiscoverer", "Cove.Testing")]
public sealed class PlatformFactAttribute : FactAttribute, ITraitAttribute
{
    public PlatformFactAttribute(TestOperatingSystem operatingSystem = TestOperatingSystem.Any)
    {
        Skip = UnsupportedReason(operatingSystem);
    }

    internal static string? UnsupportedReason(TestOperatingSystem operatingSystem) => operatingSystem switch
    {
        TestOperatingSystem.Windows when !OperatingSystem.IsWindows() => "Requires Windows",
        TestOperatingSystem.MacOS when !OperatingSystem.IsMacOS() => "Requires macOS",
        TestOperatingSystem.Linux when !OperatingSystem.IsLinux() => "Requires Linux",
        TestOperatingSystem.Unix when OperatingSystem.IsWindows() => "Requires a Unix platform",
        _ => null
    };
}

[TraitDiscoverer("Cove.Testing.PlatformCategoryDiscoverer", "Cove.Testing")]
public sealed class PlatformTheoryAttribute : TheoryAttribute, ITraitAttribute
{
    public PlatformTheoryAttribute(TestOperatingSystem operatingSystem = TestOperatingSystem.Any)
    {
        Skip = PlatformFactAttribute.UnsupportedReason(operatingSystem);
    }
}

[TraitDiscoverer("Cove.Testing.LiveCategoryDiscoverer", "Cove.Testing")]
public sealed class ExternalFactAttribute : FactAttribute, ITraitAttribute
{
    public ExternalFactAttribute(TestOperatingSystem operatingSystem, params string[] executables)
    {
        Skip = PlatformFactAttribute.UnsupportedReason(operatingSystem);
        if (Skip is null && !string.Equals(Environment.GetEnvironmentVariable("COVE_LIVE_TESTS"), "1", StringComparison.Ordinal))
            Skip = "Requires COVE_LIVE_TESTS=1";
        if (Skip is null)
        {
            var missing = executables.Where(name => TestPrerequisite.FindExecutable(name) is null).ToArray();
            if (missing.Length > 0)
                Skip = $"Requires executable: {string.Join(", ", missing)}";
        }
    }
}

[TraitDiscoverer("Cove.Testing.LiveCategoryDiscoverer", "Cove.Testing")]
public sealed class ExternalTheoryAttribute : TheoryAttribute, ITraitAttribute
{
    public ExternalTheoryAttribute(TestOperatingSystem operatingSystem, params string[] executables)
    {
        Skip = PlatformFactAttribute.UnsupportedReason(operatingSystem);
        if (Skip is null && !string.Equals(Environment.GetEnvironmentVariable("COVE_LIVE_TESTS"), "1", StringComparison.Ordinal))
            Skip = "Requires COVE_LIVE_TESTS=1";
        if (Skip is null)
        {
            var missing = executables.Where(name => TestPrerequisite.FindExecutable(name) is null).ToArray();
            if (missing.Length > 0)
                Skip = $"Requires executable: {string.Join(", ", missing)}";
        }
    }
}

[TraitDiscoverer("Cove.Testing.LiveCategoryDiscoverer", "Cove.Testing")]
public sealed class LiveFactAttribute : FactAttribute, ITraitAttribute
{
    public LiveFactAttribute(TestOperatingSystem operatingSystem = TestOperatingSystem.Any)
    {
        Skip = PlatformFactAttribute.UnsupportedReason(operatingSystem);
        if (Skip is null && !string.Equals(Environment.GetEnvironmentVariable("COVE_LIVE_TESTS"), "1", StringComparison.Ordinal))
            Skip = "Requires COVE_LIVE_TESTS=1";
    }
}

[TraitDiscoverer("Cove.Testing.LiveCategoryDiscoverer", "Cove.Testing")]
public sealed class LiveTheoryAttribute : TheoryAttribute, ITraitAttribute
{
    public LiveTheoryAttribute(TestOperatingSystem operatingSystem = TestOperatingSystem.Any)
    {
        Skip = PlatformFactAttribute.UnsupportedReason(operatingSystem);
        if (Skip is null && !string.Equals(Environment.GetEnvironmentVariable("COVE_LIVE_TESTS"), "1", StringComparison.Ordinal))
            Skip = "Requires COVE_LIVE_TESTS=1";
    }
}

public sealed class PlatformCategoryDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>(TestTraits.Category, TestTraits.Platform);
    }
}

public sealed class LiveCategoryDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>(TestTraits.Category, TestTraits.Live);
    }
}
