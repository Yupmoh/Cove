using Cove.Persistence;

namespace Cove.Engine.Bays;

public static class BayInvariants
{
    public static BayModel CloseShore(BayModel model, string shoreId, Func<string> newId)
    {
        var closed = model.Shores.FirstOrDefault(r => r.Id == shoreId);
        if (closed is null)
            return model;

        var shores = model.Shores.Where(r => r.Id != shoreId).ToList();
        var nooks = new Dictionary<string, NookRecord>(model.Nooks);
        foreach (var nookId in CollectNookIds(closed.LayoutTree))
            nooks.Remove(nookId);

        string? activeShoreId = model.ActiveShoreId;
        if (shores.Count == 0)
        {
            var (shore, nook) = Mint(BayModel.MainWingId, newId);
            shores.Add(shore);
            nooks[nook.NookId] = nook;
            activeShoreId = shore.Id;
        }
        else if (activeShoreId == shoreId)
        {
            activeShoreId = shores[0].Id;
        }

        return model with { Shores = shores, Nooks = nooks, ActiveShoreId = activeShoreId };
    }

    public static BayModel RemoveWing(BayModel model, string wingId, Func<string> newId)
    {
        if (wingId == BayModel.MainWingId)
            return model;
        if (!model.Wings.Any(w => w.Id == wingId))
            return model;

        var shores = model.Shores
            .Select(r => r.WingId == wingId ? r with { WingId = BayModel.MainWingId } : r)
            .ToList();
        var wings = model.Wings.Where(w => w.Id != wingId).ToList();

        return model with { Wings = wings, Shores = shores };
    }

    public static BayModel SwitchWing(BayModel model, string wingId, Func<string> newId)
    {
        if (!model.Wings.Any(w => w.Id == wingId))
            return model;

        var wingShores = model.Shores.Where(r => r.WingId == wingId).ToList();
        if (wingShores.Count == 0)
        {
            var (shore, nook) = Mint(wingId, newId);
            var shores = new List<Shore>(model.Shores) { shore };
            var nooks = new Dictionary<string, NookRecord>(model.Nooks) { [nook.NookId] = nook };
            return model with { Shores = shores, Nooks = nooks, ActiveShoreId = shore.Id };
        }

        return model with { ActiveShoreId = wingShores[0].Id };
    }

    public static BayModel AddShore(BayModel model, string wingId, string shoreId, string nookId, string name)
    {
        var wing = model.Wings.Any(w => w.Id == wingId) ? wingId : BayModel.MainWingId;
        var shore = new Shore
        {
            Id = shoreId,
            Name = name,
            WingId = wing,
            ActiveNookId = nookId,
            LayoutTree = new NookLeaf { NookId = nookId },
        };
        var shores = new List<Shore>(model.Shores) { shore };
        var nooks = new Dictionary<string, NookRecord>(model.Nooks) { [nookId] = new NookRecord { NookId = nookId } };
        return model with { Shores = shores, Nooks = nooks, ActiveShoreId = shoreId };
    }

    public static BayModel RenameShore(BayModel model, string shoreId, string name)
        => model with { Shores = model.Shores.Select(r => r.Id == shoreId ? r with { Name = name } : r).ToList() };

    public static BayModel SetShorePinned(BayModel model, string shoreId, bool pinned)
        => model with { Shores = model.Shores.Select(r => r.Id == shoreId ? r with { Pinned = pinned } : r).ToList() };

    public static BayModel MoveShoreToWing(BayModel model, string shoreId, string wingId)
    {
        if (!model.Wings.Any(w => w.Id == wingId))
            return model;
        return model with { Shores = model.Shores.Select(r => r.Id == shoreId ? r with { WingId = wingId } : r).ToList() };
    }

    public static BayModel SwitchShore(BayModel model, string shoreId)
        => model.Shores.Any(r => r.Id == shoreId) ? model with { ActiveShoreId = shoreId } : model;

    public static BayModel AddWing(BayModel model, string wingId, string name)
    {
        if (model.Wings.Any(w => w.Id == wingId))
            return model;
        return model with { Wings = new List<Wing>(model.Wings) { new Wing { Id = wingId, Name = name } } };
    }

    public static BayModel RenameWing(BayModel model, string wingId, string name)
        => model with { Wings = model.Wings.Select(w => w.Id == wingId ? w with { Name = name } : w).ToList() };

    public static BayModel ReorderWings(BayModel model, IReadOnlyList<string> orderedIds)
    {
        var known = new HashSet<string>(model.Wings.Select(w => w.Id), System.StringComparer.Ordinal);
        var next = new List<Wing>();
        foreach (var id in orderedIds)
            if (known.Contains(id))
                next.Add(model.Wings.First(w => w.Id == id));
        foreach (var w in model.Wings)
            if (!next.Exists(x => x.Id == w.Id))
                next.Add(w);
        return model with { Wings = next };
    }

    public static BayModel SetWingIcon(BayModel model, string wingId, BayIcon? icon)
        => model with { Wings = model.Wings.Select(w => w.Id == wingId ? w with { Icon = icon } : w).ToList() };

    private static (Shore Shore, NookRecord Nook) Mint(string wingId, Func<string> newId)
    {
        var nookId = newId();
        var shoreId = newId();
        var shore = new Shore
        {
            Id = shoreId,
            Name = "shell",
            WingId = wingId,
            ActiveNookId = nookId,
            LayoutTree = new NookLeaf { NookId = nookId },
        };
        return (shore, new NookRecord { NookId = nookId });
    }

    public static IEnumerable<string> CollectNookIds(MosaicNode node)
    {
        switch (node)
        {
            case NookLeaf leaf:
                yield return leaf.NookId;
                break;
            case SplitNode split:
                foreach (var id in CollectNookIds(split.ChildA))
                    yield return id;
                foreach (var id in CollectNookIds(split.ChildB))
                    yield return id;
                break;
        }
    }
}
