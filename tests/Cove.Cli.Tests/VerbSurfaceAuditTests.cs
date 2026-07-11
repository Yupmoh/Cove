using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class VerbSurfaceAuditTests
{
    private static readonly string[] M4Namespaces =
    {
        "nook", "bay", "worktree", "shore", "wing", "collection", "hook", "config",
        "note", "extension", "exec", "theme", "launcher", "task", "run", "adapter",
        "browser", "agent", "review", "context", "commands", "version", "skills",
        "launch", "launch-profile", "migrate", "edits", "capture", "timeline",
        "bay-command", "memory", "blackboard",
    };

    private static string GoldenDir
    {
        get
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            for (var i = 0; i < 6; i++)
            {
                var candidate = Path.Combine(dir, "goldens");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir)!;
            }
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "goldens");
        }
    }

    private static HashSet<string> RegisteredRoutes()
    {
        var set = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var k in CoveCommandRegistry.Keys)
            set.Add(k);
        foreach (var r in Cove.Engine.EngineCommandCatalogue.RegisteredRoutes)
            set.Add(r);
        return set;
    }

    private static HashSet<string> GoldenCommands()
    {
        var goldenPath = Path.Combine(GoldenDir, "commands-catalogue.json");
        var set = new HashSet<string>(System.StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(File.ReadAllText(goldenPath));
        foreach (var entry in doc.RootElement.EnumerateArray())
            set.Add(entry.GetProperty("command").GetString()!);
        return set;
    }

    private static string NamespaceOf(string command)
    {
        const string prefix = "cove://commands/";
        if (command.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            var rest = command.Substring(prefix.Length);
            var dot = rest.IndexOf('.');
            return dot >= 0 ? rest.Substring(0, dot) : rest;
        }
        var space = command.IndexOf(' ');
        return space >= 0 ? command.Substring(0, space) : command;
    }

    [Fact]
    public void EveryM4Namespace_HasAtLeastOneRegisteredRoute()
    {
        var namespaces = RegisteredRoutes().Select(NamespaceOf).ToHashSet(System.StringComparer.Ordinal);
        var missing = M4Namespaces.Where(ns => !namespaces.Contains(ns)).ToList();
        Assert.True(missing.Count == 0, $"M4 namespaces with no registered route: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryCatalogueRoute_ResolvesToRegisteredHandler()
    {
        var golden = GoldenCommands();
        var registered = RegisteredRoutes();
        var unroutable = golden.Where(c => !registered.Contains(c)).OrderBy(c => c, System.StringComparer.Ordinal).ToList();
        Assert.True(unroutable.Count == 0, $"catalogue commands with no registered handler: {string.Join(", ", unroutable)}");
    }

    [Fact]
    public void EveryRegisteredHandler_AppearsInCatalogue()
    {
        var golden = GoldenCommands();
        var registered = RegisteredRoutes();
        var absent = registered.Where(c => !golden.Contains(c)).OrderBy(c => c, System.StringComparer.Ordinal).ToList();
        Assert.True(absent.Count == 0, $"registered handlers absent from catalogue: {string.Join(", ", absent)}");
    }

    [Fact]
    public void RegisteredRouteSurface_IsNotEmpty()
    {
        Assert.True(RegisteredRoutes().Count > 100);
    }
}
