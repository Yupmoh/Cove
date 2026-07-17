using System.Linq;
using Cove.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cove.SourceGen.Tests;

public class SettingDescriptionGenTests
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
    public void EmitsConstructorSuppliedDescription()
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

        var generated = RunGenerator(source);

        Assert.Contains(
            "new SettingSchemaEntry(\"appearance.fontSize\", \"Font size\", \"Appearance\", \"number\", \"Size of terminal text\", \"int\", null)",
            generated);
    }

    [Fact]
    public void NamedDescriptionOverridesConstructorDescription()
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

        var generated = RunGenerator(source, SettableDescriptionAttributeSource);

        Assert.Contains(
            "new SettingSchemaEntry(\"appearance.fontSize\", \"Font size\", \"Appearance\", \"number\", \"Named description\", \"int\", null)",
            generated);
    }

    private static string RunGenerator(string userSource, string attributeSource = AttributeSource)
    {
        var compilation = CSharpCompilation.Create(
            "TestAsm",
            new[] { CSharpSyntaxTree.ParseText(attributeSource), CSharpSyntaxTree.ParseText(userSource) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new SettingSchemaGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.Empty(diagnostics);
        var generated = output.SyntaxTrees.Single(t => t.FilePath.EndsWith("CoveSettingSchema.g.cs"));
        return generated.ToString();
    }
}
