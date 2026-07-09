using System.Text.Json.Nodes;
using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CanvasPatchEngineTests
{
    private static CanvasPatchEngine NewEngine()
    {
        var engine = new CanvasPatchEngine(NullLogger.Instance);
        engine.Initialize(new JsonObject { ["count"] = 0, ["name"] = "test" });
        return engine;
    }

    private static JsonNode ParsePatch(string json) => JsonNode.Parse(json)!;

    [Fact]
    public void Add_CreatesNestedPath()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"add","path":"/nested/deep/value","value":42}]"""));

        Assert.True(result.Applied);
        var state = result.Result!.AsObject();
        Assert.True(state.ContainsKey("nested"));
        Assert.Equal(42, state["nested"]!["deep"]!["value"]!.GetValue<int>());
    }

    [Fact]
    public void ForwardRef_AutoCreatesIntermediatePath()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"add","path":"/a/b/c/d","value":"deep"}]"""));

        Assert.True(result.Applied);
        var state = result.Result!.AsObject();
        Assert.Equal("deep", state["a"]!["b"]!["c"]!["d"]!.GetValue<string>());
    }

    [Fact]
    public void TestMismatch_SkipsRemainingOps()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"test","path":"/name","value":"wrong"},{"op":"add","path":"/shouldNotExist","value":true}]"""));

        Assert.False(result.Applied);
        Assert.Contains("test-mismatch", result.Error);
        var state = result.Result!.AsObject();
        Assert.False(state.ContainsKey("shouldNotExist"));
    }

    [Fact]
    public void TestMatch_ContinuesWithRemainingOps()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"test","path":"/name","value":"test"},{"op":"add","path":"/verified","value":true}]"""));

        Assert.True(result.Applied);
        Assert.True(result.Result!.AsObject()["verified"]!.GetValue<bool>());
    }

    [Fact]
    public void Replace_UpdatesExistingValue()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"replace","path":"/count","value":99}]"""));

        Assert.True(result.Applied);
        Assert.Equal(99, result.Result!.AsObject()["count"]!.GetValue<int>());
    }

    [Fact]
    public void Remove_DeletesExistingKey()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"remove","path":"/name"}]"""));

        Assert.True(result.Applied);
        Assert.False(result.Result!.AsObject().ContainsKey("name"));
    }

    [Fact]
    public void Move_TransfersValueBetweenPaths()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"move","from":"/name","path":"/label"}]"""));

        Assert.True(result.Applied);
        var state = result.Result!.AsObject();
        Assert.False(state.ContainsKey("name"));
        Assert.Equal("test", state["label"]!.GetValue<string>());
    }

    [Fact]
    public void Copy_DuplicatesValueToNewPath()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"copy","from":"/name","path":"/label"}]"""));

        Assert.True(result.Applied);
        var state = result.Result!.AsObject();
        Assert.Equal("test", state["name"]!.GetValue<string>());
        Assert.Equal("test", state["label"]!.GetValue<string>());
    }

    [Fact]
    public async Task PipedJsonl_YieldsOneWritePerFlush()
    {
        var engine = new CanvasPatchEngine(NullLogger.Instance, System.TimeSpan.FromMilliseconds(250));
        engine.Initialize(new JsonObject { ["items"] = new JsonArray() });

        int eventCount = 0;
        engine.StateChanged += (_, _) => eventCount++;

        engine.QueuePatch(ParsePatch("""[{"op":"add","path":"/items/0","value":"a"}]"""));
        engine.QueuePatch(ParsePatch("""[{"op":"add","path":"/items/1","value":"b"}]"""));
        engine.QueuePatch(ParsePatch("""[{"op":"add","path":"/items/2","value":"c"}]"""));

        await engine.FlushAsync();

        Assert.Equal(1, eventCount);
        var state = engine.GetState()!.AsObject();
        var items = state["items"]!.AsArray();
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void ArrayIndex_AutoExtendsArray()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"add","path":"/list/2","value":"third"}]"""));

        Assert.True(result.Applied);
        var list = result.Result!.AsObject()["list"]!.AsArray();
        Assert.Equal(3, list.Count);
        Assert.Equal("third", list[2]!.GetValue<string>());
    }

    [Fact]
    public void TildeEscaping_InPathTokens()
    {
        var engine = NewEngine();
        var result = engine.ApplyPatch(ParsePatch("""[{"op":"add","path":"/key~1with~0tilde","value":"escaped"}]"""));

        Assert.True(result.Applied);
        var state = result.Result!.AsObject();
        Assert.True(state.ContainsKey("key/with~tilde"));
    }
}
