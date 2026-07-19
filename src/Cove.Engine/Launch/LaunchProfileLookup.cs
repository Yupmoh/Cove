using Cove.Adapters;

namespace Cove.Engine.Launch;

public interface ILaunchProfileLookup
{
    LaunchProfile? Find(string adapter, string profileSlug);
}

public sealed class LaunchProfileLookup(
    LaunchProfileStore profiles) : ILaunchProfileLookup
{
    public LaunchProfile? Find(string adapter, string profileSlug)
    {
        var profile = profiles.Load(adapter, profileSlug);
        if (profile is not null || profileSlug != "default")
            return profile;

        return new LaunchProfile(
            "Default",
            "default",
            adapter,
            true,
            null,
            null,
            Array.Empty<string>(),
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            Array.Empty<string>(),
            null,
            1);
    }
}
