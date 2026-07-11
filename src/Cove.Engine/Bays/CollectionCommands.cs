using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class CollectionCommands
{
    [CoveCommand("cove://commands/collection.create")]
    public static async Task<ControlResponse> CollectionCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(CollectionJsonContext.Default.CollectionCreateParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Name))
            return ctx.Fail("bad_params", "name is required");
        var collection = await manager.CreateCollectionAsync(p.Name).ConfigureAwait(false);
        return ctx.Ok(new CollectionIdResult(collection.Id), CollectionJsonContext.Default.CollectionIdResult);
    }

    [CoveCommand("cove://commands/collection.rename")]
    public static async Task<ControlResponse> CollectionRename(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(CollectionJsonContext.Default.CollectionRenameParams) is not { } p)
            return ctx.Fail("bad_params", "id and name are required");
        return await manager.RenameCollectionAsync(p.Id, p.Name).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"collection {p.Id} not renamable");
    }

    [CoveCommand("cove://commands/collection.remove")]
    public static async Task<ControlResponse> CollectionRemove(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(CollectionJsonContext.Default.CollectionIdParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        return await manager.RemoveCollectionAsync(p.Id).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"collection {p.Id} not removable");
    }

    [CoveCommand("cove://commands/collection.switch")]
    public static async Task<ControlResponse> CollectionSwitch(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(CollectionJsonContext.Default.CollectionIdParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        return await manager.SwitchCollectionAsync(p.Id).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"collection {p.Id} not found");
    }

    [CoveCommand("cove://commands/collection.move-bay")]
    public static async Task<ControlResponse> CollectionMoveBay(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(CollectionJsonContext.Default.CollectionMoveParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and collectionId are required");
        return await manager.MoveBayToCollectionAsync(p.BayId, p.CollectionId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "bay or collection not found");
    }

    [CoveCommand("cove://commands/collection.list")]
    public static Task<ControlResponse> CollectionList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        return Task.FromResult(ctx.Ok(new CollectionListResult(manager.ListCollections()), CollectionJsonContext.Default.CollectionListResult));
    }
}

public sealed record CollectionCreateParams(string Name);
public sealed record CollectionIdParams(string Id);
public sealed record CollectionRenameParams(string Id, string Name);
public sealed record CollectionMoveParams(string BayId, string CollectionId);
public sealed record CollectionIdResult(string Id);
public sealed record CollectionSummary(string Id, string Name, string ProjectCount, bool Active);
public sealed record CollectionListResult(IReadOnlyList<CollectionSummary> Collections);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CollectionCreateParams))]
[JsonSerializable(typeof(CollectionIdParams))]
[JsonSerializable(typeof(CollectionRenameParams))]
[JsonSerializable(typeof(CollectionMoveParams))]
[JsonSerializable(typeof(CollectionIdResult))]
[JsonSerializable(typeof(CollectionListResult))]
public sealed partial class CollectionJsonContext : JsonSerializerContext { }
