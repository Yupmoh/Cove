using System.Reflection;

namespace Cove.Platform;

public static class CoveBuild
{
    public static string InformationalVersion { get; } = Resolve();

    private static string Resolve()
    {
        var raw = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(raw))
            return "0.0.0";
        var plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }
}
