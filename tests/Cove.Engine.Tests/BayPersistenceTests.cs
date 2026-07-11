using System.IO;
using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayPersistenceTests
{
    [Fact]
    public void Save_Load_RoundTrips_LayoutAndSessions()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var leaf = new NookLeaf
            {
                NookId = "p1",
                Subtabs = new[] { new Subtab("p1", NookType.Terminal) },
            };
            var ws = new BaySnapshot
            {
                Id = "default",
                Name = "default",
                ProjectDir = "/proj",
                ActiveShoreId = "r1",
                Shores = new[]
                {
                    new ShoreSnapshot
                    {
                        Id = "r1",
                        Name = "shore",
                        LayoutTree = leaf,
                        ZoomedNookId = null,
                    },
                },
            };
            var descs = new[] { new NookDescriptor("p1", "/bin/sh", new[] { "-l" }, "/tmp") };

            BayPersistence.Save(ws, descs, wsDir);
            var (loaded, sessions) = BayPersistence.Load(wsDir, NullLogger.Instance);

            Assert.NotNull(loaded);
            var s1 = JsonSerializer.Serialize(ws, CoveJsonContext.Default.BaySnapshot);
            var s2 = JsonSerializer.Serialize(loaded, CoveJsonContext.Default.BaySnapshot);
            Assert.Equal(s1, s2);
            Assert.True(sessions.ContainsKey("p1"));
            Assert.Equal("/bin/sh", sessions["p1"].Command);
            Assert.Equal("/tmp", sessions["p1"].Cwd);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }
}
