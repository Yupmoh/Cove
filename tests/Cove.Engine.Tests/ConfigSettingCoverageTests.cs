using System.IO;
using Cove.Engine.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ConfigSettingCoverageTests
{
    private static readonly string[] SimpleTypes = { "string", "int", "double", "bool" };

    [Fact]
    public void EverySimpleSchemaKey_HasAWiredGetter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-cfg-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            var missing = new List<string>();
            foreach (var entry in ConfigSchemaGenerator.Generate())
            {
                if (entry.Control == "section" || !SimpleTypes.Contains(entry.Type))
                    continue;
                if (entry.Key == "keybindings.bindings")
                    continue;
                if (cfg.Get(entry.Key) is null)
                    missing.Add(entry.Key);
            }
            Assert.True(missing.Count == 0, "schema keys without a Get arm: " + string.Join(", ", missing));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void EverySimpleSchemaKey_RoundTripsThroughSet()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-cfg-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            var broken = new List<string>();
            foreach (var entry in ConfigSchemaGenerator.Generate())
            {
                if (entry.Control == "section" || !SimpleTypes.Contains(entry.Type))
                    continue;
                if (entry.Key == "keybindings.bindings")
                    continue;
                var probe = entry.Type switch
                {
                    "bool" => "true",
                    "int" => "7",
                    "double" => "1.5",
                    _ => entry.Options is { Length: > 0 } opts ? opts[0] : "probe-value",
                };
                cfg.Set(entry.Key, probe);
                if (cfg.Get(entry.Key) != probe)
                    broken.Add(entry.Key);
            }
            Assert.True(broken.Count == 0, "schema keys that do not round-trip: " + string.Join(", ", broken));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void SchemaKeys_UseSectionKeyPrefixes_NotTypeNames()
    {
        foreach (var entry in ConfigSchemaGenerator.Generate())
            Assert.DoesNotContain("Section.", entry.Key, System.StringComparison.Ordinal);
    }
}
