using System;
using System.IO;
using System.Linq;
using Cove.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(p => MetadataReference.CreateFromFile(p))
        .ToArray();

    [Theory]
    [InlineData("[CoveCommand(\"test\")] public int Run(string command) => 0;", "COVE001")]
    [InlineData("[CoveCommand(\"test\")] public static int Run() => 0;", "COVE002")]
    [InlineData("[CoveCommand(\"\")] public static int Run(string command) => 0;", "COVE003")]
    public void InvalidCommandSignatureProducesErrorAtAttributedMethod(string method, string expectedId)
    {
        var source = $$"""
            using Cove.Protocol;

            namespace Cove.Cli;

            internal sealed class Commands
            {
                {{method}}
            }
            """;
        var compilation = CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new CoveCommandGenerator());

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == expectedId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Commands.cs", diagnostic.Location.SourceTree?.FilePath);
    }

    [Fact]
    public void QuotedCommandKeyAndDescriptionGenerateCompilableOutput()
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
        var compilation = CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new CoveCommandGenerator());

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static CSharpCompilation CreateCompilation(string userSource)
    {
        return CSharpCompilation.Create(
            "TestAsm",
            new[]
            {
                CSharpSyntaxTree.ParseText(AttributeSource, path: "CoveCommandAttribute.cs"),
                CSharpSyntaxTree.ParseText(userSource, path: "Commands.cs")
            },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
