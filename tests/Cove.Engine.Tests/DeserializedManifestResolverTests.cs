using Cove.Adapters;
using Cove.Engine.Adapters;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DeserializedManifestResolverTests
{
    [Fact]
    public void ResolversRunAgainstDeserializedFixtureManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-nreprobe-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "test-v2");
        Directory.CreateDirectory(dir);
        var src = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", "test-v2");
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);

        var store = new AdapterManifestStore(root);
        foreach (var manifest in store.LoadAll())
        {
            var update = AdapterListCommands.ResolveUpdateCommand(manifest, null);
            var uninstall = AdapterListCommands.ResolveUninstallCommand(manifest, null);
            Assert.Null(update);
            Assert.Null(uninstall);
        }
    }
}
