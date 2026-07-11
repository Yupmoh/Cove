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
        _path = Path.Combine(_dir, "bay.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static BaySnapshot Sample() => new BaySnapshot
    {
        Id = "w1",
        Name = "demo",
        ProjectDir = "/tmp/demo",
        ActiveShoreId = "r1",
        Shores = new[]
        {
            new ShoreSnapshot
            {
                Id = "r1",
                Name = "main",
                LayoutTree = new SplitNode
                {
                    Orientation = SplitOrientation.Row,
                    Ratio = 0.6,
                    ChildA = new NookLeaf
                    {
                        NookId = "p1",
                        Subtabs = new[]
                        {
                            new Subtab("d1", NookType.Terminal, "zsh"),
                            new Subtab("d2", NookType.Terminal),
                        },
                        ActiveSubtab = 1,
                    },
                    ChildB = new SplitNode
                    {
                        Orientation = SplitOrientation.Column,
                        Ratio = 0.4,
                        ChildA = new NookLeaf
                        {
                            NookId = "p2",
                            Subtabs = new[] { new Subtab("d3", NookType.Terminal) },
                        },
                        ChildB = new NookLeaf
                        {
                            NookId = "p3",
                            Subtabs = new[] { new Subtab("d4", NookType.Empty) },
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
        AtomicJsonStore.Write(_path, ws, CoveJsonContext.Default.BaySnapshot);
        var read = AtomicJsonStore.Read<BaySnapshot>(_path, CoveJsonContext.Default.BaySnapshot, NullLogger.Instance);

        Assert.NotNull(read);
        var originalJson = JsonSerializer.Serialize(ws, CoveJsonContext.Default.BaySnapshot);
        var roundTrippedJson = JsonSerializer.Serialize(read, CoveJsonContext.Default.BaySnapshot);
        Assert.Equal(originalJson, roundTrippedJson);
    }

    [Fact]
    public void LegacyVanilla_NormalizesToTerminal()
    {
        var json = "{\"documentId\":\"d1\",\"nookType\":\"vanilla\"}";
        var subtab = JsonSerializer.Deserialize(json, CoveJsonContext.Default.Subtab);

        Assert.NotNull(subtab);
        Assert.Equal(NookType.Terminal, subtab!.NookType);
    }
}
