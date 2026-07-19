using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Tests;

internal static class LaunchTestFactory
{
    public static LaunchOrchestrator Create(
        AdapterManifestStore? manifests = null,
        MethodRunner? methods = null,
        BinaryDiscoveryService? binaries = null,
        string? loginShellPath = null,
        LaunchProfileStore? profiles = null,
        AgentResumeService? resume = null,
        LauncherOverrideStore? overrides = null,
        ILogger? logger = null)
    {
        ILaunchAdapterLookup? adapterLookup = null;
        ILaunchProcessAcquirer? processAcquirer = null;
        ILauncherOptionsResolver? launcherOptions = null;
        if (manifests is not null)
        {
            adapterLookup = new LaunchAdapterLookup(manifests);
            processAcquirer = new LaunchProcessAcquirer(
                methods ?? new MethodRunner(),
                binaries ?? new BinaryDiscoveryService(),
                loginShellPath,
                logger);
            launcherOptions = new LauncherOptionsResolver(
                adapterLookup,
                processAcquirer,
                new LauncherOptionsParser(),
                logger);
        }

        return new LaunchOrchestrator(
            new LaunchCommandComposer(),
            adapterLookup,
            processAcquirer,
            launcherOptions,
            profiles is null ? null : new LaunchProfileLookup(profiles),
            resume,
            overrides,
            logger);
    }
}
