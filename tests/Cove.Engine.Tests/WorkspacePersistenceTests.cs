using System.IO;
using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public void Save_Load_RoundTrips_LayoutAndSessions()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var leaf = new PaneLeaf
            {
                PaneId = "p1",
                Subtabs = new[] { new Subtab("p1", PaneType.Terminal) },
            };
            var ws = new WorkspaceSnapshot
            {
                Id = "default",
                Name = "default",
                ProjectDir = "/proj",
                ActiveRoomId = "r1",
                Rooms = new[]
                {
                    new RoomSnapshot
                    {
                        Id = "r1",
                        Name = "room",
                        LayoutTree = leaf,
                        ZoomedPaneId = null,
                    },
                },
            };
            var descs = new[] { new PaneDescriptor("p1", "/bin/sh", new[] { "-l" }, "/tmp") };

            WorkspacePersistence.Save(ws, descs, wsDir);
            var (loaded, sessions) = WorkspacePersistence.Load(wsDir, NullLogger.Instance);

            Assert.NotNull(loaded);
            var s1 = JsonSerializer.Serialize(ws, CoveJsonContext.Default.WorkspaceSnapshot);
            var s2 = JsonSerializer.Serialize(loaded, CoveJsonContext.Default.WorkspaceSnapshot);
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
