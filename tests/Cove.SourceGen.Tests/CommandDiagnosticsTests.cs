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

    private const string ContractSource = """
        namespace Cove.Cli
        {
            public sealed class CommandContext;
        }

        namespace Cove.Engine
        {
            public sealed class EngineDispatchContext;
        }

        namespace Cove.Protocol
        {
            public sealed class ControlResponse;
        }
        """;

    [Theory]
    [InlineData(
        "[CoveCommand(\"test\")] public int Run(string command) => 0;",
        "COVE001",
        "Command method 'Run' must be static",
        "[CoveCommand(\"test\")] public int Run(string command) => 0;")]
    [InlineData(
        "[CoveCommand(\"test\")] public static int Run() => 0;",
        "COVE002",
        "Command method 'Run' must have exactly one parameter",
        "[CoveCommand(\"test\")] public static int Run() => 0;")]
    [InlineData(
        "[CoveCommand(\"\")] public static int Run(string command) => 0;",
        "COVE003",
        "Command method 'Run' must declare a non-empty command key",
        "\"\"")]
    [InlineData(
        "[CoveCommand(\"   \")] public static int Run(string command) => 0;",
        "COVE003",
        "Command method 'Run' must declare a non-empty command key",
        "\"   \"")]
    public void InvalidCommandSignatureProducesExactDiagnostic(
        string method,
        string expectedId,
        string expectedMessage,
        string expectedSpan)
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
            expectedSpan,
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public void DuplicateCommandKeysFailCompilationAtEachKeyArgument()
    {
        const string source = """
            using Cove.Protocol;

            namespace Cove.Cli;

            internal static class Commands
            {
                [CoveCommand("same")]
                public static System.Threading.Tasks.Task<int> First(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);
                [CoveCommand("same")]
                public static System.Threading.Tasks.Task<int> Second(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("DispatchContracts.cs", ContractSource),
            ("Commands.cs", source));

        var duplicates = diagnostics.Where(item => item.Id == "COVE004").ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.All(duplicates, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Command key 'same' is declared more than once", diagnostic.GetMessage());
            Assert.Equal("Commands.cs", diagnostic.Location.SourceTree?.FilePath);
            Assert.Equal("\"same\"", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        });
        Assert.DoesNotContain(output.SyntaxTrees, tree => tree.FilePath.EndsWith("CoveCommandRegistry.g.cs"));
    }

    [Theory]
    [InlineData(
        "[CoveCommand(\"test\")] public static int Run(ref string command) => 0;",
        "COVE005",
        "Command parameter 'command' has unsupported type or modifier",
        "ref string command")]
    [InlineData(
        "[CoveCommand(\"test\")] public static int Run(System.Span<int> command) => 0;",
        "COVE005",
        "Command parameter 'command' has unsupported type or modifier",
        "System.Span<int> command")]
    [InlineData(
        "[CoveCommand(\"test\")] public static void Run(string command) { }",
        "COVE006",
        "Command method 'Run' has unsupported return type 'void'",
        "void")]
    [InlineData(
        "[CoveCommand(\"test\")] public static System.Span<int> Run(string command) => default;",
        "COVE006",
        "Command method 'Run' has unsupported return type 'Span<int>'",
        "System.Span<int>")]
    public void UnsupportedCommandContractFailsCompilationAtExactDeclaration(
        string method,
        string expectedId,
        string expectedMessage,
        string expectedSpan)
    {
        var source = $$"""
            using Cove.Protocol;

            namespace Cove.Cli;

            internal static class Commands
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
        Assert.Equal(expectedMessage, diagnostic.GetMessage());
        Assert.Equal("Commands.cs", diagnostic.Location.SourceTree?.FilePath);
        Assert.Equal(expectedSpan, diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData(
        "test",
        "string command",
        "System.Threading.Tasks.Task<int>",
        "COVE005",
        "string command")]
    [InlineData(
        "test",
        "Cove.Cli.CommandContext command",
        "System.Threading.Tasks.Task<string>",
        "COVE006",
        "System.Threading.Tasks.Task<string>")]
    [InlineData(
        "cove://commands/test",
        "Cove.Cli.CommandContext command",
        "System.Threading.Tasks.Task<Cove.Protocol.ControlResponse>",
        "COVE005",
        "Cove.Cli.CommandContext command")]
    [InlineData(
        "cove://commands/test",
        "Cove.Engine.EngineDispatchContext command",
        "System.Threading.Tasks.Task<int>",
        "COVE006",
        "System.Threading.Tasks.Task<int>")]
    public void DispatchTierRequiresExactHandlerContract(
        string key,
        string parameter,
        string returnType,
        string expectedId,
        string expectedSpan)
    {
        var source = $$"""
            using Cove.Protocol;

            namespace Cove.Cli;

            internal static class Commands
            {
                [CoveCommand("{{key}}")]
                public static {{returnType}} Run({{parameter}}) =>
                    throw new System.NotSupportedException();
            }
            """;

        var (_, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("DispatchContracts.cs", ContractSource),
            ("Commands.cs", source));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(expectedSpan, diagnostic.Location.SourceTree!
            .GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public void PrivateCommandHandlerProducesAccessibilityDiagnostic()
    {
        const string source = """
            using Cove.Protocol;

            namespace Cove.Cli;

            internal static class Commands
            {
                [CoveCommand("test")]
                private static System.Threading.Tasks.Task<int> Run(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);
            }
            """;

        var (_, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("DispatchContracts.cs", ContractSource),
            ("Commands.cs", source));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("COVE010", diagnostic.Id);
        Assert.Equal(
            "Command method 'Run' must be accessible to generated dispatch",
            diagnostic.GetMessage());
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
                public static System.Threading.Tasks.Task<int> Run(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("DispatchContracts.cs", ContractSource),
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
