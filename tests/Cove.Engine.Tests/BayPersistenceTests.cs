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

    [Fact]
    public void Save_Load_RoundTrips_ColsRows()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var descs = new[] { new NookDescriptor("p1", "/bin/sh", new[] { "-l" }, "/tmp", null, null, null, null, false, 213, 51) };

            BayPersistence.Save(new BaySnapshot { Id = "default", Name = "default", ProjectDir = "/proj" }, descs, wsDir);
            var (_, sessions) = BayPersistence.Load(wsDir, NullLogger.Instance);

            Assert.True(sessions.ContainsKey("p1"));
            Assert.Equal(213, sessions["p1"].Cols);
            Assert.Equal(51, sessions["p1"].Rows);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ToleratesMissingColsRows_DefaultsTo80x24()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var nookDir = Path.Combine(wsDir, "nooks", "p1");
            Directory.CreateDirectory(nookDir);
            File.WriteAllText(Path.Combine(nookDir, "session.json"), "{\"nookId\":\"p1\",\"command\":\"/bin/sh\",\"args\":[\"-l\"],\"cwd\":\"/tmp\"}");

            var (_, sessions) = BayPersistence.Load(wsDir, NullLogger.Instance);

            Assert.True(sessions.ContainsKey("p1"));
            Assert.Equal(80, sessions["p1"].Cols);
            Assert.Equal(24, sessions["p1"].Rows);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }

    [Fact]
    public void Save_Load_RoundTrips_Icon()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var ws = new BaySnapshot
            {
                Id = "default",
                Name = "default",
                ProjectDir = "/proj",
                IconKind = "emoji",
                IconValue = "🚀",
            };

            BayPersistence.Save(ws, System.Array.Empty<NookDescriptor>(), wsDir);
            var (loaded, _) = BayPersistence.Load(wsDir, NullLogger.Instance);

            Assert.NotNull(loaded);
            Assert.Equal("emoji", loaded!.IconKind);
            Assert.Equal("🚀", loaded.IconValue);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ToleratesMissingIcon()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var ws = new BaySnapshot
            {
                Id = "default",
                Name = "default",
                ProjectDir = "/proj",
            };

            BayPersistence.Save(ws, System.Array.Empty<NookDescriptor>(), wsDir);
            var (loaded, _) = BayPersistence.Load(wsDir, NullLogger.Instance);

            Assert.NotNull(loaded);
            Assert.Null(loaded!.IconKind);
            Assert.Null(loaded.IconValue);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }
}
