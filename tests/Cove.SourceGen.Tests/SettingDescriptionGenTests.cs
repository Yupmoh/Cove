using System.Collections;
using Cove.SourceGen;
using Xunit;

namespace Cove.SourceGen.Tests;

public sealed class SettingDescriptionGenTests
{
    private const string AttributeSource = """
        namespace Cove.Engine.Config;

        [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
        public sealed class SettingAttribute(string label, string tab, string control = "text", string? description = null) : System.Attribute
        {
            public string Label { get; } = label;
            public string Tab { get; } = tab;
            public string Control { get; } = control;
            public string? Description { get; } = description;
            public string[]? Options { get; set; }
        }
        """;

    private const string SettableDescriptionAttributeSource = """
        namespace Cove.Engine.Config;

        [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
        public sealed class SettingAttribute(string label, string tab, string control = "text", string? description = null) : System.Attribute
        {
            public string Label { get; } = label;
            public string Tab { get; } = tab;
            public string Control { get; } = control;
            public string? Description { get; set; } = description;
            public string[]? Options { get; set; }
        }
        """;

    [Fact]
    public void EmitsConstructorSuppliedDescriptionInCompilableSchema()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class AppearanceSection
            {
                [Setting("Font size", "Appearance", "number", "Size of terminal text")]
                public int FontSize { get; set; }
            }
            """;

        var entry = GenerateSingleEntry(source, AttributeSource);

        AssertSchemaEntry(
            entry,
            "appearance.fontSize",
            "Font size",
            "Appearance",
            "number",
            "Size of terminal text",
            "int");
    }

    [Fact]
    public void NamedDescriptionOverridesConstructorDescriptionInCompilableSchema()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class AppearanceSection
            {
                [Setting("Font size", "Appearance", "number", "Constructor description", Description = "Named description")]
                public int FontSize { get; set; }
            }
            """;

        var entry = GenerateSingleEntry(source, SettableDescriptionAttributeSource);

        AssertSchemaEntry(
            entry,
            "appearance.fontSize",
            "Font size",
            "Appearance",
            "number",
            "Named description",
            "int");
    }

    private static object GenerateSingleEntry(string source, string attributeSource)
    {
        var (output, diagnostics) = GeneratorTestHarness.Run(
            new SettingSchemaGenerator(),
            ("SettingAttribute.cs", attributeSource),
            ("AppearanceSection.cs", source));

        Assert.Empty(diagnostics);
        var generated = Assert.Single(output.SyntaxTrees, tree => tree.FilePath.EndsWith("CoveSettingSchema.g.cs"));
        Assert.NotEmpty(generated.GetRoot().DescendantNodes());
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var schema = assembly.GetType("Cove.Generated.CoveSettingSchema");
        Assert.NotNull(schema);
        var entries = Assert.IsAssignableFrom<IEnumerable>(schema.GetField("Entries")!.GetValue(null))
            .Cast<object>()
            .ToArray();
        return Assert.Single(entries);
    }

    private static void AssertSchemaEntry(
        object entry,
        string key,
        string label,
        string tab,
        string control,
        string description,
        string type)
    {
        var entryType = entry.GetType();
        Assert.Equal(key, entryType.GetProperty("Key")!.GetValue(entry));
        Assert.Equal(label, entryType.GetProperty("Label")!.GetValue(entry));
        Assert.Equal(tab, entryType.GetProperty("Tab")!.GetValue(entry));
        Assert.Equal(control, entryType.GetProperty("Control")!.GetValue(entry));
        Assert.Equal(description, entryType.GetProperty("Description")!.GetValue(entry));
        Assert.Equal(type, entryType.GetProperty("Type")!.GetValue(entry));
        Assert.Null(entryType.GetProperty("Options")!.GetValue(entry));
    }
}
