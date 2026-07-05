using System;
using System.IO;
using System.Text.Json;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class MosaicSerializationTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public MosaicSerializationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cove-mosaic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "workspace.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static WorkspaceSnapshot Sample() => new WorkspaceSnapshot
    {
        Id = "w1",
        Name = "demo",
        ProjectDir = "/tmp/demo",
        ActiveRoomId = "r1",
        Rooms = new[]
        {
            new RoomSnapshot
            {
                Id = "r1",
                Name = "main",
                LayoutTree = new SplitNode
                {
                    Orientation = SplitOrientation.Row,
                    Ratio = 0.6,
                    ChildA = new PaneLeaf
                    {
                        PaneId = "p1",
                        Subtabs = new[]
                        {
                            new Subtab("d1", PaneType.Terminal, "zsh"),
                            new Subtab("d2", PaneType.Terminal),
                        },
                        ActiveSubtab = 1,
                    },
                    ChildB = new SplitNode
                    {
                        Orientation = SplitOrientation.Column,
                        Ratio = 0.4,
                        ChildA = new PaneLeaf
                        {
                            PaneId = "p2",
                            Subtabs = new[] { new Subtab("d3", PaneType.Terminal) },
                        },
                        ChildB = new PaneLeaf
                        {
                            PaneId = "p3",
                            Subtabs = new[] { new Subtab("d4", PaneType.Empty) },
                        },
                    },
                },
            },
        },
    };

    [Fact]
    public void RoundTrip_NestedTree_IsByteIdentical()
    {
        var ws = Sample();
        AtomicJsonStore.Write(_path, ws, CoveJsonContext.Default.WorkspaceSnapshot);
        var read = AtomicJsonStore.Read<WorkspaceSnapshot>(_path, CoveJsonContext.Default.WorkspaceSnapshot, NullLogger.Instance);

        Assert.NotNull(read);
        var originalJson = JsonSerializer.Serialize(ws, CoveJsonContext.Default.WorkspaceSnapshot);
        var roundTrippedJson = JsonSerializer.Serialize(read, CoveJsonContext.Default.WorkspaceSnapshot);
        Assert.Equal(originalJson, roundTrippedJson);
    }

    [Fact]
    public void LegacyVanilla_NormalizesToTerminal()
    {
        var json = "{\"documentId\":\"d1\",\"paneType\":\"vanilla\"}";
        var subtab = JsonSerializer.Deserialize(json, CoveJsonContext.Default.Subtab);

        Assert.NotNull(subtab);
        Assert.Equal(PaneType.Terminal, subtab!.PaneType);
    }
}
