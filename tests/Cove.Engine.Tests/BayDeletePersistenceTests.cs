using System.IO;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayDeletePersistenceTests
{
    [Fact]
    public async Task Delete_RemovesPersistedBayAndPreventsStartupResurrection()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-bay-delete-" + System.Guid.NewGuid().ToString("N"));
        var bayId = "deleted-bay";
        var bayDir = Path.Combine(root, bayId);
        try
        {
            var snapshot = new BaySnapshot
            {
                Id = bayId,
                Name = "Deleted Bay",
                ProjectDir = "/proj",
            };
            var nooks = new[] { new NookDescriptor("nook-1", "/bin/sh", new[] { "-l" }, "/tmp") };
            BayPersistence.Save(snapshot, nooks, bayDir);

            Assert.True(File.Exists(Path.Combine(bayDir, "bay.json")));
            Assert.True(File.Exists(Path.Combine(bayDir, "nooks", "nook-1", "session.json")));

            await using var manager = new BayManager(
                registry: new RegistryModel { OpenBays = [bayId], FocusedBayId = bayId },
                bays: [new BayModel { Id = bayId, Name = snapshot.Name, ProjectDir = snapshot.ProjectDir }]);
            var requestParams = JsonSerializer.SerializeToElement(
                new BayIdParams(bayId),
                BaysJsonContext.Default.BayIdParams);
            var response = await EngineCommandRouter.RouteAsync(
                new ControlRequest("1", "cove://commands/bay.delete", requestParams),
                bays: manager,
                baysDir: root);

            Assert.True(response!.Ok);

            Assert.False(Directory.Exists(bayDir));
            Assert.Empty(BayStartup.Enumerate(root, NullLogger.Instance));
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void Delete_RejectsPathsOutsideBaysRoot()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-bay-containment-" + System.Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "bays");
        var outside = Path.Combine(parent, "outside");
        try
        {
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "sentinel"), "keep");

            BayPersistence.Delete(Path.Combine("..", "outside"), root, NullLogger.Instance);

            Assert.True(File.Exists(Path.Combine(outside, "sentinel")));
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(parent);
        }
    }
}
