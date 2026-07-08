using System.Collections.Generic;
using Cove.Adapters;

namespace Cove.Engine.Protocol;

public sealed class ExtensionCommand(string command, string? description, string source, string adapter, string method)
{
    public string Command { get; } = command;
    public string? Description { get; } = description;
    public string Source { get; } = source;
    public string Adapter { get; } = adapter;
    public string Method { get; } = method;
}

public sealed class ExtensionResolved(string adapter, string method)
{
    public string Adapter { get; } = adapter;
    public string Method { get; } = method;
}

public sealed class ExtensionRegistry
{
    private readonly AdapterManifestStore _manifests;

    public ExtensionRegistry(AdapterManifestStore manifests)
    {
        _manifests = manifests;
    }

    private List<ExtensionCommand> BuildCurrent()
    {
        var commands = new List<ExtensionCommand>();
        foreach (var manifest in _manifests.LoadAll())
        {
            var adapter = manifest.Name;
            foreach (var (method, _) in manifest.Methods)
            {
                var command = $"extension.{adapter}.{method}";
                commands.Add(new ExtensionCommand(command, null, "extension", adapter, method));
            }
        }
        return commands;
    }

    public void Index()
    {
    }

    public IReadOnlyList<ExtensionCommand> List()
        => BuildCurrent();

    public ExtensionResolved? Resolve(string command)
    {
        var current = BuildCurrent();
        ExtensionResolved? exact = null;
        foreach (var cmd in current)
        {
            if (cmd.Command == command)
                return new ExtensionResolved(cmd.Adapter, cmd.Method);
            if (cmd.Command.StartsWith(command + ".", System.StringComparison.Ordinal))
            {
                if (exact is not null)
                    return null;
                exact = new ExtensionResolved(cmd.Adapter, cmd.Method);
            }
        }
        return exact;
    }
}
