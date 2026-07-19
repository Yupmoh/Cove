using System.Collections;
using Cove.SourceGen;
using Xunit;

namespace Cove.SourceGen.Tests;

public sealed class GeneratorTests
{
    private const string AttributeSource = """
        namespace Cove.Protocol;
        [System.AttributeUsage(System.AttributeTargets.Method)]
        public sealed class CoveCommandAttribute : System.Attribute
        {
            public CoveCommandAttribute(string key) => Key = key;
            public string Key { get; }
            public string? Description { get; set; }
            public string? Source { get; set; }
        }
        """;

    [Fact]
    public void EmitsExactHandlerRegistryForAttributedMethods()
    {
        const string source = """
            using Cove.Protocol;
            namespace Cove.Cli;
            internal static class CliCommands
            {
                [CoveCommand("version")] public static int Version(string command) => 0;
                [CoveCommand("nook list")] public static int NookList(string command) => 0;
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("CliCommands.cs", source));

        Assert.Empty(diagnostics);
        var generated = Assert.Single(output.SyntaxTrees, tree => tree.FilePath.EndsWith("CoveCommandRegistry.g.cs"));
        Assert.NotEmpty(generated.GetRoot().DescendantNodes());
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var registry = assembly.GetType("Cove.Generated.CoveCommandRegistry");
        Assert.NotNull(registry);
        var handlers = Assert.IsAssignableFrom<IReadOnlyDictionary<string, Delegate>>(
            registry.GetField("Handlers")!.GetValue(null));

        Assert.Equal(new[] { "nook list", "version" }, handlers.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("NookList", handlers["nook list"].Method.Name);
        Assert.Equal("Version", handlers["version"].Method.Name);
    }

    [Fact]
    public void ThirdVerbAppearsInExactCatalogueWithoutGeneratorChanges()
    {
        const string source = """
            using Cove.Protocol;
            namespace Cove.Cli;
            internal static class CliCommands
            {
                [CoveCommand("version")] public static int Version(string command) => 0;
                [CoveCommand("nook list")] public static int NookList(string command) => 0;
                [CoveCommand("theme list", Description = "Lists themes", Source = "core")] public static int ThemeList(string command) => 0;
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("CliCommands.cs", source));

        Assert.Empty(diagnostics);
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var registry = assembly.GetType("Cove.Generated.CoveCommandRegistry");
        Assert.NotNull(registry);
        var keys = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            registry.GetField("Keys")!.GetValue(null));
        Assert.Equal(new[] { "nook list", "theme list", "version" }, keys);

        var catalogue = Assert.IsAssignableFrom<IEnumerable>(registry.GetField("Catalogue")!.GetValue(null))
            .Cast<object>()
            .ToArray();
        Assert.Equal(3, catalogue.Length);
        var theme = Assert.Single(catalogue, entry =>
            (string)entry.GetType().GetProperty("Command")!.GetValue(entry)! == "theme list");
        Assert.Equal("Lists themes", theme.GetType().GetProperty("Description")!.GetValue(theme));
        Assert.Equal("core", theme.GetType().GetProperty("Source")!.GetValue(theme));
    }
}
