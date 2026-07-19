using System.Collections;
using Cove.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cove.SourceGen.Tests;

public sealed class SettingDiagnosticsTests
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

    [Fact]
    public void DuplicateSettingKeysFailCompilationAtEachProperty()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class FirstRoot
            {
                [Setting("Editor", "editor", "section")]
                public FirstEditor Editor { get; set; } = new();
            }

            public sealed class SecondRoot
            {
                [Setting("Editor", "editor", "section")]
                public SecondEditor Editor { get; set; } = new();
            }

            public sealed class FirstEditor
            {
                [Setting("Font", "editor")]
                public string Font { get; set; } = "";
            }

            public sealed class SecondEditor
            {
                [Setting("Font", "editor")]
                public string Font { get; set; } = "";
            }
            """;

        var (output, diagnostics) = Run(source);

        var duplicates = diagnostics.Where(item => item.Id == "COVE007").ToArray();
        Assert.Equal(4, duplicates.Length);
        var fontDuplicates = duplicates
            .Where(diagnostic => diagnostic.GetMessage().Contains("'editor.font'", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, fontDuplicates.Length);
        Assert.All(fontDuplicates, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Setting key 'editor.font' is declared more than once", diagnostic.GetMessage());
            Assert.Equal("Settings.cs", diagnostic.Location.SourceTree?.FilePath);
            Assert.Equal(
                "[Setting(\"Font\", \"editor\")]\n    public string Font { get; set; } = \"\";",
                diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        });
        Assert.DoesNotContain(output.SyntaxTrees, tree => tree.FilePath.EndsWith("CoveSettingSchema.g.cs"));
    }

    [Fact]
    public void UnsupportedSettingTypeFailsCompilationAtType()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class EditorSection
            {
                [Setting("Changed", "editor", "text")]
                public System.DateTime Changed { get; set; }
            }
            """;

        var (output, diagnostics) = Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("COVE008", diagnostic.Id);
        Assert.Equal("Setting 'Changed' has unsupported type 'DateTime' for control 'text'", diagnostic.GetMessage());
        Assert.Equal("Settings.cs", diagnostic.Location.SourceTree?.FilePath);
        Assert.Equal("System.DateTime", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void SelectWithoutOptionsFailsCompilationAtControlArgument()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class EditorSection
            {
                [Setting("Font", "editor", "select")]
                public string Font { get; set; } = "";
            }
            """;

        var (output, diagnostics) = Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("COVE009", diagnostic.Id);
        Assert.Equal("Setting 'Font' has unsupported options for control 'select'", diagnostic.GetMessage());
        Assert.Equal("Settings.cs", diagnostic.Location.SourceTree?.FilePath);
        Assert.Equal("\"select\"", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void OptionsOnUnsupportedControlFailCompilationAtOptionsArgument()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class EditorSection
            {
                [Setting("Font", "editor", "text", Options = new[] { "mono" })]
                public string Font { get; set; } = "";
            }
            """;

        var (output, diagnostics) = Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("COVE009", diagnostic.Id);
        Assert.Equal("Setting 'Font' has unsupported options for control 'text'", diagnostic.GetMessage());
        Assert.Equal("Settings.cs", diagnostic.Location.SourceTree?.FilePath);
        Assert.Equal(
            "Options = new[] { \"mono\" }",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void ArbitrarySectionTypesGenerateDeterministicSchemaFromSectionAttributes()
    {
        const string source = """
            using Cove.Engine.Config;

            namespace Cove.Engine;

            public sealed class Root
            {
                [Setting("Editor", "editor", "section")]
                public BespokePreferences MarkdownTools { get; set; } = new();
            }

            public sealed class BespokePreferences
            {
                [Setting("Enabled", "editor", "toggle")]
                public bool Enabled { get; set; }

                [Setting("Font size", "editor", "number")]
                public double FontSize { get; set; }
            }
            """;

        var (output, diagnostics) = Run(source);

        Assert.Empty(diagnostics);
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var schema = assembly.GetType("Cove.Generated.CoveSettingSchema");
        Assert.NotNull(schema);
        var entries = Assert.IsAssignableFrom<IEnumerable>(schema.GetField("Entries")!.GetValue(null))
            .Cast<object>()
            .ToArray();
        var keys = entries
            .Select(entry => (string)entry.GetType().GetProperty("Key")!.GetValue(entry)!)
            .ToArray();
        Assert.Equal(new[] { "markdownTools", "markdown_tools.enabled", "markdown_tools.fontSize" }, keys);
    }

    private static (CSharpCompilation Output, System.Collections.Immutable.ImmutableArray<Diagnostic> Diagnostics) Run(
        string source)
        => GeneratorTestHarness.Run(
            new SettingSchemaGenerator(),
            ("SettingAttribute.cs", AttributeSource),
            ("Settings.cs", source));
}
