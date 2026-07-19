using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cove.SourceGen.Tests;

internal static class GeneratorTestHarness
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    public static (CSharpCompilation Output, ImmutableArray<Diagnostic> DriverDiagnostics) Run(
        IIncrementalGenerator generator,
        params (string Path, string Source)[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorTest_" + Guid.NewGuid().ToString("N"),
            sources.Select(source => CSharpSyntaxTree.ParseText(source.Source, path: source.Path)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return ((CSharpCompilation)output, diagnostics);
    }

    public static void AssertCompiles(CSharpCompilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    public static Assembly EmitAndLoad(CSharpCompilation compilation)
    {
        AssertCompiles(compilation);
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }
}
