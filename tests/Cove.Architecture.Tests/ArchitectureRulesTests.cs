using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Cove.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    private static readonly string[] NativeInteropProjects =
    [
        "Cove.Engine",
        "Cove.Gui",
        "Cove.Tui"
    ];

    private static readonly MetadataReference[] PlatformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    [Fact]
    public void PackageReferences_DoNotSpecifyInlineVersions()
    {
        var violations = EnumerateProductionFiles("*.csproj")
            .Where(path => XDocument.Parse(File.ReadAllText(path))
                .Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .Any(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Version")))
            .Select(RelativePath)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSources_DoNotUseReflection()
    {
        var compilation = CreateCompilation(EnumerateProductionFiles("*.cs"));
        var violations = FindProductionSymbolViolations(
            compilation,
            ClassifyReflectionViolation);

        Assert.Empty(violations);
    }

    [Fact]
    public void ReflectionAnalyzer_DetectsForbiddenSymbolFamilies()
    {
        const string source = """
            using ReflectionNamespace = System.Reflection;
            [assembly: System.Reflection.AssemblyMetadata("fixture", "allowed")]

            internal sealed class ReflectionFixture
            {
                private static void Exercise(System.Type type, System.Delegate callback)
                {
                    _ = typeof(System.Reflection.Assembly);
                    _ = nameof(System.Reflection.Assembly.FullName);
                    _ = System.Reflection.Assembly.GetExecutingAssembly();
                    _ = new System.Reflection.AssemblyName();
                    _ = System.Reflection.BindingFlags.Public;
                    _ = System.Activator.CreateInstance(typeof(ReflectionFixture));
                    _ = System.Type.GetType("System.String");
                    _ = System.Type.GetTypeFromHandle(typeof(string).TypeHandle);
                    _ = type.GetMethod("Exercise");
                    _ = type.GetMethods();
                    _ = type.GetProperty("Value");
                    _ = type.GetProperties();
                    _ = type.GetField("Value");
                    _ = type.GetFields();
                    _ = type.GetMember("Exercise");
                    _ = type.GetMembers();
                    _ = type.GetConstructor(System.Type.EmptyTypes);
                    _ = type.GetConstructors();
                    _ = type.GetEvent("Changed");
                    _ = type.GetEvents();
                    _ = type.GetNestedType("Nested");
                    _ = type.GetNestedTypes();
                    _ = type.GetInterface("System.IDisposable");
                    _ = type.GetInterfaces();
                    _ = type.InvokeMember("Exercise", default, null, null, null);
                    _ = callback.DynamicInvoke();
                    _ = new ReflectionLookalike().GetType();
                    _ = ReflectionLookalike.GetType("allowed");
                    _ = ReflectionLookalike.GetMethod();
                    _ = ReflectionLookalike.GetMethods();
                    _ = ReflectionLookalike.CreateInstance();
                }
            }

            internal sealed class ReflectionLookalike
            {
                internal static object GetType(string name) => new();
                internal static object GetMethod() => new();
                internal static object GetMethods() => new();
                internal static object CreateInstance() => new();
            }
            """;
        var path = Path.Combine(RepositoryRoot, "fixtures", "ReflectionViolations.cs");
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path);
        var compilation = CSharpCompilation.Create(
            "ReflectionAnalyzerFixture",
            [tree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var violations = FindSymbolViolations(compilation, ClassifyReflectionViolation);

        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Equal(
            [
                "fixtures/ReflectionViolations.cs:10:40",
                "fixtures/ReflectionViolations.cs:11:13",
                "fixtures/ReflectionViolations.cs:13:30",
                "fixtures/ReflectionViolations.cs:14:25",
                "fixtures/ReflectionViolations.cs:15:25",
                "fixtures/ReflectionViolations.cs:16:18",
                "fixtures/ReflectionViolations.cs:17:18",
                "fixtures/ReflectionViolations.cs:18:18",
                "fixtures/ReflectionViolations.cs:19:18",
                "fixtures/ReflectionViolations.cs:1:1",
                "fixtures/ReflectionViolations.cs:20:18",
                "fixtures/ReflectionViolations.cs:21:18",
                "fixtures/ReflectionViolations.cs:22:18",
                "fixtures/ReflectionViolations.cs:23:18",
                "fixtures/ReflectionViolations.cs:24:18",
                "fixtures/ReflectionViolations.cs:25:18",
                "fixtures/ReflectionViolations.cs:26:18",
                "fixtures/ReflectionViolations.cs:27:18",
                "fixtures/ReflectionViolations.cs:28:18",
                "fixtures/ReflectionViolations.cs:29:18",
                "fixtures/ReflectionViolations.cs:30:18",
                "fixtures/ReflectionViolations.cs:31:18",
                "fixtures/ReflectionViolations.cs:32:18",
                "fixtures/ReflectionViolations.cs:33:22"
            ],
            violations.Select(ViolationLocation));
        Assert.DoesNotContain(violations, violation =>
            violation.StartsWith("fixtures/ReflectionViolations.cs:2:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:8:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:9:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:12:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:34:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:35:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:36:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:37:", StringComparison.Ordinal) ||
            violation.StartsWith("fixtures/ReflectionViolations.cs:38:", StringComparison.Ordinal));
    }

    [Fact]
    public void ReflectionAnalyzer_DetectsDynamicTypeAndInvocation()
    {
        const string source = """
            internal static class DynamicFixture
            {
                internal static object Exercise(dynamic target)
                {
                    return target.Acquire();
                }
            }
            """;
        var path = Path.Combine(RepositoryRoot, "fixtures", "DynamicViolations.cs");
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path);
        var compilation = CSharpCompilation.Create(
            "DynamicAnalyzerFixture",
            [tree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var violations = FindSymbolViolations(compilation, ClassifyReflectionViolation);

        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Equal(
            [
                "fixtures/DynamicViolations.cs:3:37",
                "fixtures/DynamicViolations.cs:5:16"
            ],
            violations.Select(ViolationLocation));
    }

    [Fact]
    public void ProductionSemanticScan_FailsClosedWhenTypeResolutionBreaks()
    {
        const string source = """
            internal static class BrokenFixture
            {
                internal static void Exercise()
                {
                    _ = Missing.Reference.ReflectionGateway.GetMethod();
                }
            }
            """;
        var path = Path.Combine(RepositoryRoot, "fixtures", "BrokenSemanticResolution.cs");
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path);
        var compilation = CSharpCompilation.Create(
            "BrokenSemanticResolutionFixture",
            [tree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var violations = FindProductionSymbolViolations(compilation, ClassifyReflectionViolation);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, violation =>
            violation.StartsWith("fixtures/BrokenSemanticResolution.cs:5:13: CS0103", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionSemanticScan_RejectsEmptySourceSet()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateCompilation(Array.Empty<string>()));

        Assert.Equal("Production semantic scan discovered zero source files.", exception.Message);
    }

    [Fact]
    public void ProductionSources_DoNotWriteDirectlyToConsole()
    {
        var files = EnumerateProductionFiles("*.cs");
        var compilation = CreateCompilation(files);
        var violations = FindProductionSymbolViolations(
            compilation,
            static (model, node) =>
            {
                if (!IsProjectSource(node.SyntaxTree.FilePath, "Cove.Engine", "Cove.Gui"))
                    return null;
                if (node is not SimpleNameSyntax name)
                    return null;
                var symbol = ResolveSymbol(model, name);
                return symbol switch
                {
                    IMethodSymbol method when IsSystemConsole(method.ContainingType) && method.Name == "WriteLine"
                        => method.ToDisplayString(),
                    IPropertySymbol property when IsSystemConsole(property.ContainingType) && property.Name is "Error" or "Out"
                        => property.ToDisplayString(),
                    _ => null
                };
            });

        Assert.Empty(violations);
    }

    [Fact]
    public void EngineGuiAndTui_DoNotDeclareNativeImports()
    {
        var files = EnumerateProductionFiles("*.cs");
        var compilation = CreateCompilation(files);
        var violations = FindProductionSymbolViolations(
            compilation,
            static (model, node) =>
            {
                if (!IsProjectSource(node.SyntaxTree.FilePath, NativeInteropProjects))
                    return null;
                if (node is not AttributeSyntax attribute)
                    return null;
                var symbol = ResolveSymbol(model, attribute);
                var attributeType = symbol switch
                {
                    IMethodSymbol constructor => constructor.ContainingType,
                    INamedTypeSymbol type => type,
                    _ => null
                };
                return attributeType?.ToDisplayString() is
                    "System.Runtime.InteropServices.DllImportAttribute" or
                    "System.Runtime.InteropServices.LibraryImportAttribute"
                    ? attributeType.ToDisplayString()
                    : null;
            });

        Assert.Empty(violations);
    }

    [Fact]
    public void ProjectReferences_FollowLayeringRules()
    {
        var protocolReferences = GetProjectReferences("Cove.Protocol");
        var forbiddenProtocolReferences = new[]
        {
            "Cove.Engine",
            "Cove.Gui",
            "Cove.Cli",
            "Cove.Tui",
            "Cove.Adapters"
        };
        var violations = protocolReferences
            .Intersect(forbiddenProtocolReferences, StringComparer.Ordinal)
            .Select(reference => $"src/Cove.Protocol/Cove.Protocol.csproj -> {reference}")
            .Concat(GetProjectReferences("Cove.Platform")
                .Where(reference => reference == "Cove.Engine")
                .Select(reference => $"src/Cove.Platform/Cove.Platform.csproj -> {reference}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void TestMethods_DoNotSilentlyReturnForMissingPrerequisites()
    {
        var trees = EnumerateTestFiles("*.cs")
            .Select(path => CSharpSyntaxTree.ParseText(
                    File.ReadAllText(path),
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path));
        var violations = FindSilentPrerequisiteReturns(trees);

        Assert.Empty(violations);
    }

    [Fact]
    public void SilentPrerequisiteAnalyzer_DetectsAliasedProbeReturns()
    {
        const string source = """
            using System;
            using System.Diagnostics;
            using System.IO;

            internal sealed class FactAttribute : Attribute;

            internal static class TestPrerequisite
            {
                internal static string? FindExecutable(string name) => null;
            }

            internal sealed class PrerequisiteFixture
            {
                [Fact]
                internal void AliasedPrerequisite()
                {
                    var find = TestPrerequisite.FindExecutable;
                    var executable = find("git");
                    if (executable is null)
                        return;
                }

                [Fact]
                internal void AliasedEnvironment()
                {
                    Func<string, string?> read = Environment.GetEnvironmentVariable;
                    var configured = read("COVE_TEST");
                    if (configured is null)
                    {
                        return;
                    }
                }

                [Fact]
                internal void AliasedFile()
                {
                    var exists = File.Exists;
                    var present = exists("fixture");
                    if (!present)
                        return;
                }

                [Fact]
                internal void ProcessProbe()
                {
                    using var process = Process.GetCurrentProcess();
                    if (!process.HasExited)
                        return;
                }

                internal void NonTest()
                {
                    return;
                }

                [Fact]
                internal void NestedFunctionsRemainAllowed()
                {
                    Action lambda = () => { return; };
                    static void Local()
                    {
                        return;
                    }
                    lambda();
                    Local();
                }
            }
            """;
        var path = Path.Combine(RepositoryRoot, "fixtures", "SilentPrerequisiteReturns.cs");
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path);
        var compilation = CSharpCompilation.Create(
            "SilentPrerequisiteFixture",
            [tree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var violations = FindSilentPrerequisiteReturns([tree]);

        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Equal(
            [
                "fixtures/SilentPrerequisiteReturns.cs:20:13",
                "fixtures/SilentPrerequisiteReturns.cs:30:13",
                "fixtures/SilentPrerequisiteReturns.cs:40:13",
                "fixtures/SilentPrerequisiteReturns.cs:48:13"
            ],
            violations.Select(ViolationLocation));
    }

    [Fact]
    public void TestSources_DoNotUseBlindSleepsOrDirectEnvironmentMutation()
    {
        var violations = EnumerateTestFiles("*.cs")
            .SelectMany(path =>
            {
                var root = CSharpSyntaxTree.ParseText(
                    File.ReadAllText(path),
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path).GetRoot();
                return root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation =>
                        IsBlindThreadSleep(invocation) ||
                        IsDirectEnvironmentMutation(path, invocation))
                    .Select(invocation => FormatViolation(path, invocation));
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void TestMethods_DoNotContainArithmeticPlaceholders()
    {
        var violations = EnumerateTestFiles("*.cs")
            .SelectMany(path =>
            {
                var root = CSharpSyntaxTree.ParseText(
                    File.ReadAllText(path),
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path).GetRoot();
                return root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(IsTestMethod)
                    .Where(method => method.Identifier.ValueText == "TestHost_Runs" ||
                        method.DescendantNodes()
                            .OfType<BinaryExpressionSyntax>()
                            .Any(expression =>
                                expression.IsKind(SyntaxKind.AddExpression) &&
                                expression.Left is LiteralExpressionSyntax { Token.ValueText: "2" } &&
                                expression.Right is LiteralExpressionSyntax { Token.ValueText: "2" }))
                    .Select(method => FormatViolation(path, method));
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<string> files)
    {
        var paths = files.ToArray();
        if (paths.Length == 0)
            throw new InvalidOperationException("Production semantic scan discovered zero source files.");
        var trees = paths
            .Select(path => CSharpSyntaxTree.ParseText(
                File.ReadAllText(path),
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path))
            .Append(CSharpSyntaxTree.ParseText(
                """
                global using System;
                global using System.Collections.Generic;
                global using System.IO;
                global using System.Linq;
                global using System.Net.Http;
                global using System.Threading;
                global using System.Threading.Tasks;
                """,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                Path.Combine(RepositoryRoot, "generated", "ImplicitUsings.g.cs")))
            .ToArray();
        var references = PlatformReferences
            .Concat(EnumeratePackageReferences(paths))
            .GroupBy(reference => Path.GetFileName(reference.Display), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        return CSharpCompilation.Create(
            "CoveArchitectureAnalysis",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static IEnumerable<MetadataReference> EnumeratePackageReferences(IEnumerable<string> files)
    {
        var projectDirectories = files
            .Select(path => Path.GetRelativePath(SourceRoot, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0])
            .Distinct(StringComparer.Ordinal)
            .Select(project => Path.Combine(SourceRoot, project));

        foreach (var projectDirectory in projectDirectories)
        {
            var assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
            if (!File.Exists(assetsPath))
                continue;

            using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var root = document.RootElement;
            var packageFolders = root.GetProperty("packageFolders")
                .EnumerateObject()
                .Select(folder => folder.Name)
                .ToArray();
            var targets = root.GetProperty("targets").EnumerateObject().ToArray();
            var target = targets
                .FirstOrDefault(candidate => !candidate.Name.Contains('/'))
                .Value;

            foreach (var library in target.EnumerateObject())
            {
                if (!library.Value.TryGetProperty("type", out var type) ||
                    type.GetString() != "package" ||
                    !library.Value.TryGetProperty("compile", out var compileAssets))
                {
                    continue;
                }

                var packagePath = library.Name.ToLowerInvariant()
                    .Replace('/', Path.DirectorySeparatorChar);
                foreach (var asset in compileAssets.EnumerateObject())
                {
                    if (!asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var referencePath = packageFolders
                        .Select(folder => Path.Combine(
                            folder,
                            packagePath,
                            asset.Name.Replace('/', Path.DirectorySeparatorChar)))
                        .FirstOrDefault(File.Exists);
                    if (referencePath is not null)
                        yield return MetadataReference.CreateFromFile(referencePath);
                }
            }
        }
    }

    private static string[] FindSymbolViolations(
        CSharpCompilation compilation,
        Func<SemanticModel, SyntaxNode, string?> classify)
    {
        return compilation.SyntaxTrees
            .SelectMany(tree =>
            {
                var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
                return tree.GetRoot()
                    .DescendantNodes()
                    .Select(node => (Node: node, Symbol: classify(model, node)))
                    .Where(result => result.Symbol is not null)
                    .Select(result =>
                    {
                        var position = result.Node.GetLocation().GetLineSpan().StartLinePosition;
                        return $"{RelativePath(tree.FilePath)}:{position.Line + 1}:{position.Character + 1}: {result.Symbol}";
                    });
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] FindProductionSymbolViolations(
        CSharpCompilation compilation,
        Func<SemanticModel, SyntaxNode, string?> classify)
    {
        return FindSymbolViolations(compilation, classify)
            .Concat(compilation.GetDiagnostics()
                .Where(diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error &&
                    !IsExpectedProductionDiagnostic(diagnostic))
                .Select(FormatDiagnostic))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExpectedProductionDiagnostic(Diagnostic diagnostic)
    {
        if (IsExpectedGeneratorDiagnostic(diagnostic))
            return true;

        var message = diagnostic.GetMessage();
        var path = diagnostic.Location.GetLineSpan().Path;
        return diagnostic.Id switch
        {
            "CS0117" => message.Contains("JsonContext", StringComparison.Ordinal) &&
                message.Contains("'Default'", StringComparison.Ordinal),
            "CS0534" or "CS7036" => message.Contains("JsonSerializerContext", StringComparison.Ordinal),
            "CS0234" => message.Contains("'Generated'", StringComparison.Ordinal) &&
                message.Contains("'Cove'", StringComparison.Ordinal),
            "CS0103" => message.Contains("'CoveCommandRegistry'", StringComparison.Ordinal) ||
                message.Contains("'CoveVersionConstant'", StringComparison.Ordinal),
            "CS1061" => message.Contains("'AddCoveGuiCommands'", StringComparison.Ordinal) ||
                message.Contains("'AddPerfResultsCommand'", StringComparison.Ordinal) ||
                message.Contains("'BayId'", StringComparison.Ordinal) ||
                message.Contains("'DataDirSchemaVersion'", StringComparison.Ordinal) ||
                message.Contains("'CoveVersionAtCreate'", StringComparison.Ordinal) ||
                message.Contains("'CreatedAtUnixMs'", StringComparison.Ordinal),
            "CS0019" => RelativePath(path) == "src/Cove.Engine/Restart/RestorationService.cs" &&
                message == "Operator '??' cannot be applied to operands of type 'T' and 'CoveState'",
            "CS1503" => message == "Argument 1: cannot convert from 'T' to 'Cove.Engine.Bays.RunCommandDefinition'",
            "CS0165" => RelativePath(path).StartsWith("src/Cove.Platform/Pty/", StringComparison.Ordinal) ||
                RelativePath(path) == "src/Cove.Engine/Adapters/AdapterToolsCommands.cs" &&
                message.Contains("'sessions'", StringComparison.Ordinal),
            "CS0170" or "CS0177" => RelativePath(path)
                .StartsWith("src/Cove.Platform/Pty/", StringComparison.Ordinal),
            "CS0260" => RelativePath(path) == "src/Cove.Gui/Program.cs",
            "CS0579" => RelativePath(path) == "src/Cove.Tasks/DapperAotOptIn.cs" &&
                message.Contains("'DapperAot'", StringComparison.Ordinal),
            "CS8805" => RelativePath(path) == "src/Cove.Cli/Program.cs",
            _ => false
        };
    }

    private static bool IsExpectedGeneratorDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Id is not ("CS8795" or "CS8796" or "CS8797" or "CS8798") ||
            !diagnostic.Location.IsInSource ||
            diagnostic.Location.SourceTree is not { } tree)
        {
            return false;
        }

        var method = tree.GetRoot()
            .FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        return method?.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name switch
            {
                IdentifierNameSyntax identifier => IsGeneratorAttribute(identifier.Identifier.ValueText),
                QualifiedNameSyntax qualified => IsGeneratorAttribute(qualified.Right.Identifier.ValueText),
                AliasQualifiedNameSyntax alias => IsGeneratorAttribute(alias.Name.Identifier.ValueText),
                _ => false
            }) == true;
    }

    private static bool IsGeneratorAttribute(string name) =>
        name is "GeneratedRegex" or "GeneratedRegexAttribute" or
            "LibraryImport" or "LibraryImportAttribute" or
            "ZLoggerMessage" or "ZLoggerMessageAttribute";

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var position = span.StartLinePosition;
        return $"{RelativePath(span.Path)}:{position.Line + 1}:{position.Character + 1}: {diagnostic.Id}: {diagnostic.GetMessage()}";
    }

    private static string ViolationLocation(string violation)
    {
        var symbolSeparator = violation.IndexOf(": ", StringComparison.Ordinal);
        return symbolSeparator < 0 ? violation : violation[..symbolSeparator];
    }

    private static ISymbol? ResolveSymbol(SemanticModel model, SyntaxNode node)
    {
        var symbol = model.GetSymbolInfo(node).Symbol;
        return symbol is IAliasSymbol alias ? alias.Target : symbol;
    }

    private static string? ClassifyReflectionViolation(SemanticModel model, SyntaxNode node)
    {
        if (IsCompileTimeMetadata(model, node))
            return null;

        if (node is IdentifierNameSyntax { Identifier.ValueText: "dynamic" } dynamicType &&
            model.GetTypeInfo(dynamicType).Type is IDynamicTypeSymbol)
        {
            return "dynamic";
        }

        if (node is InvocationExpressionSyntax dynamicInvocation &&
            model.GetOperation(dynamicInvocation) is Microsoft.CodeAnalysis.Operations.IDynamicInvocationOperation)
        {
            return "dynamic invocation";
        }

        if (node is UsingDirectiveSyntax { Name: { } importedName })
        {
            var imported = ResolveSymbol(model, importedName);
            return IsInNamespace(imported, "System.Reflection")
                ? imported?.ToDisplayString()
                : null;
        }

        var symbol = node switch
        {
            SimpleNameSyntax name => ResolveSymbol(model, name),
            ObjectCreationExpressionSyntax creation => ResolveSymbol(model, creation),
            _ => null
        };
        var method = symbol as IMethodSymbol;
        var resolvedMethod = method?.ReducedFrom ?? method;
        return symbol switch
        {
            IMethodSymbol when IsInNamespace(resolvedMethod, "System.Reflection")
                => symbol.ToDisplayString(),
            IMethodSymbol when
                resolvedMethod?.Name == "CreateInstance" &&
                resolvedMethod.ContainingType.ToDisplayString() == "System.Activator"
                => symbol.ToDisplayString(),
            IMethodSymbol when
                IsForbiddenTypeMethod(resolvedMethod) &&
                resolvedMethod?.ContainingType.ToDisplayString() == "System.Type"
                => symbol.ToDisplayString(),
            IMethodSymbol when
                resolvedMethod?.Name == "DynamicInvoke" &&
                resolvedMethod.ContainingType.ToDisplayString() == "System.Delegate"
                => symbol.ToDisplayString(),
            _ => null
        };
    }

    private static bool IsForbiddenTypeMethod(IMethodSymbol? method) =>
        method?.Name is
            "GetType" or
            "GetTypeFromHandle" or
            "GetMethod" or
            "GetMethods" or
            "GetProperty" or
            "GetProperties" or
            "GetField" or
            "GetFields" or
            "GetMember" or
            "GetMembers" or
            "GetConstructor" or
            "GetConstructors" or
            "GetEvent" or
            "GetEvents" or
            "GetNestedType" or
            "GetNestedTypes" or
            "GetInterface" or
            "GetInterfaces" or
            "InvokeMember";

    private static bool IsCompileTimeMetadata(SemanticModel model, SyntaxNode node) =>
        node.AncestorsAndSelf().Any(candidate =>
            candidate is AttributeSyntax or TypeOfExpressionSyntax ||
            candidate is InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax
                {
                    Identifier.ValueText: "nameof"
                }
            } invocation &&
            model.GetOperation(invocation) is Microsoft.CodeAnalysis.Operations.INameOfOperation);

    private static bool IsInNamespace(ISymbol? symbol, string namespaceName)
    {
        var candidate = symbol switch
        {
            INamespaceSymbol namespaceSymbol => namespaceSymbol.ToDisplayString(),
            null => null,
            _ => symbol.ContainingNamespace?.ToDisplayString()
        };
        return candidate == namespaceName ||
               candidate?.StartsWith(namespaceName + ".", StringComparison.Ordinal) == true;
    }

    private static bool IsSystemConsole(INamedTypeSymbol? type) =>
        type?.ToDisplayString() == "System.Console";

    private static bool IsProjectSource(string path, params string[] projects)
    {
        var relative = RelativePath(path);
        return projects.Any(project =>
            relative.StartsWith($"src/{project}/", StringComparison.Ordinal));
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Any(name =>
                name.EndsWith("Fact", StringComparison.Ordinal) ||
                name.EndsWith("FactAttribute", StringComparison.Ordinal) ||
                name.EndsWith("Theory", StringComparison.Ordinal) ||
                name.EndsWith("TheoryAttribute", StringComparison.Ordinal));

    private static string[] FindSilentPrerequisiteReturns(IEnumerable<SyntaxTree> trees) =>
        trees.SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(IsTestMethod)
                .SelectMany(method => method.DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .Where(statement => statement.Expression is null)
                    .Where(statement => !statement.Ancestors()
                        .TakeWhile(ancestor => ancestor != method)
                        .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                    .Select(statement => FormatViolation(tree.FilePath, statement))))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool IsThreadSleep(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Sleep"
        } member &&
        member.Expression.ToString().EndsWith("Thread", StringComparison.Ordinal);

    private static bool IsBlindThreadSleep(InvocationExpressionSyntax invocation) =>
        IsThreadSleep(invocation) &&
        !invocation.Ancestors()
            .OfType<WhileStatementSyntax>()
            .Any(loop => loop.Condition.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.ValueText is "Elapsed" or "ElapsedMilliseconds"));

    private static bool IsDirectEnvironmentMutation(string path, InvocationExpressionSyntax invocation) =>
        !RelativePath(path).Equals("tests/Cove.Testing/TestResources.cs", StringComparison.Ordinal) &&
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "SetEnvironmentVariable"
        } member &&
        member.Expression.ToString().EndsWith("Environment", StringComparison.Ordinal);

    private static string FormatViolation(string path, SyntaxNode node)
    {
        var position = node.GetLocation().GetLineSpan().StartLinePosition;
        return $"{RelativePath(path)}:{position.Line + 1}:{position.Character + 1}";
    }

    private static string[] GetProjectReferences(string projectName)
    {
        var projectPath = Path.Combine(SourceRoot, projectName, $"{projectName}.csproj");
        return XDocument.Parse(File.ReadAllText(projectPath))
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateProductionFiles(string pattern)
    {
        return Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateProjectFiles(string projectName, string pattern)
    {
        var projectRoot = Path.Combine(SourceRoot, projectName);
        return Directory.EnumerateFiles(projectRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateTestFiles(string pattern)
    {
        var testRoot = Path.Combine(RepositoryRoot, "tests");
        return Directory.EnumerateFiles(testRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string path)
    {
        var relativePath = Path.GetRelativePath(SourceRoot, path);
        return relativePath.Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");
    }

    private static string RelativePath(string path)
    {
        return Path.GetRelativePath(RepositoryRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string SourceRoot => Path.Combine(RepositoryRoot, "src");

    private static string RepositoryRoot
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            Assert.True(Directory.Exists(Path.Combine(root, "src")), $"Repository src directory not found from {AppContext.BaseDirectory}");
            return root;
        }
    }
}
