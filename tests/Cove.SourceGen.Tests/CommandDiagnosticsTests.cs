using System.Collections;
using Cove.SourceGen;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Cove.SourceGen.Tests;

public sealed class CommandDiagnosticsTests
{
    private const string AttributeSource = """
        namespace Cove.Protocol;

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public sealed class CoveCommandAttribute(string key) : System.Attribute
        {
            public string Key { get; } = key;
            public string? Description { get; set; }
            public string? Source { get; set; }
        }
        """;

    [Theory]
    [InlineData(
        "[CoveCommand(\"test\")] public int Run(string command) => 0;",
        "COVE001",
        "Command method 'Run' must be static")]
    [InlineData(
        "[CoveCommand(\"test\")] public static int Run() => 0;",
        "COVE002",
        "Command method 'Run' must have exactly one parameter")]
    [InlineData(
        "[CoveCommand(\"\")] public static int Run(string command) => 0;",
        "COVE003",
        "Command method 'Run' must declare a non-empty command key")]
    public void InvalidCommandSignatureProducesExactDiagnostic(
        string method,
        string expectedId,
        string expectedMessage)
    {
        var source = $$"""
            using Cove.Protocol;

            namespace Cove.Cli;

            internal sealed class Commands
            {
                {{method}}
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("Commands.cs", source));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(expectedMessage, diagnostic.GetMessage());
        Assert.Equal("Commands.cs", diagnostic.Location.SourceTree?.FilePath);
        Assert.Equal(
            method,
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        GeneratorTestHarness.AssertCompiles(output);
    }

    [Fact]
    public void QuotedCommandMetadataGeneratesExactCompilableCatalogue()
    {
        const string source = """
            using Cove.Protocol;

            namespace Cove.Cli;

            internal static class Commands
            {
                [CoveCommand("say \"hi\"", Description = "A \"quoted\" command")]
                public static int Run(string command) => 0;
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("Commands.cs", source));

        Assert.Empty(diagnostics);
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var registry = assembly.GetType("Cove.Generated.CoveCommandRegistry");
        Assert.NotNull(registry);
        var catalogue = Assert.IsAssignableFrom<IEnumerable>(registry.GetField("Catalogue")!.GetValue(null))
            .Cast<object>()
            .ToArray();
        var entry = Assert.Single(catalogue);
        var entryType = entry.GetType();
        Assert.Equal("say \"hi\"", entryType.GetProperty("Command")!.GetValue(entry));
        Assert.Equal("A \"quoted\" command", entryType.GetProperty("Description")!.GetValue(entry));
        Assert.Equal("cli", entryType.GetProperty("Source")!.GetValue(entry));
    }
}
