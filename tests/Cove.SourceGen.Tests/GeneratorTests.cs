using System.Linq;
using Cove.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class GeneratorTests
{
    private const string AttrSource = """
        namespace Cove.Protocol;
        [System.AttributeUsage(System.AttributeTargets.Method)]
        public sealed class CoveCommandAttribute : System.Attribute
        {
            public CoveCommandAttribute(string key) => Key = key;
            public string Key { get; }
        }
        """;

    private static string RunGenerator(string userSource)
    {
        var compilation = CSharpCompilation.Create(
            "TestAsm",
            new[] { CSharpSyntaxTree.ParseText(AttrSource), CSharpSyntaxTree.ParseText(userSource) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new CoveCommandGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.Empty(diagnostics);
        var generated = output.SyntaxTrees.Single(t => t.FilePath.EndsWith("CoveCommandRegistry.g.cs"));
        return generated.ToString();
    }

    [Fact]
    public void EmitsEntryPerAttributedMethod()
    {
        var src = """
            using Cove.Protocol;
            namespace Cove.Cli;
            internal static class CliCommands
            {
                [CoveCommand("version")] public static int Version(string c) => 0;
                [CoveCommand("pane list")] public static int PaneList(string c) => 0;
            }
            """;
        var g = RunGenerator(src);
        Assert.Contains("[\"version\"] = (System.Func<string, int>)global::Cove.Cli.CliCommands.Version", g);
        Assert.Contains("[\"pane list\"] = (System.Func<string, int>)global::Cove.Cli.CliCommands.PaneList", g);
    }

    [Fact]
    public void ThirdVerbNeedsOnlyTheAttribute()
    {
        var src = """
            using Cove.Protocol;
            namespace Cove.Cli;
            internal static class CliCommands
            {
                [CoveCommand("version")] public static int Version(string c) => 0;
                [CoveCommand("pane list")] public static int PaneList(string c) => 0;
                [CoveCommand("theme list")] public static int ThemeList(string c) => 0;
            }
            """;
        var g = RunGenerator(src);
        Assert.Contains("\"theme list\"", g);
    }
}
