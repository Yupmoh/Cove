using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cove.SourceGen;

[Generator(LanguageNames.CSharp)]
public sealed class CoveCommandGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Cove.Protocol.CoveCommandAttribute";
    private static readonly DiagnosticDescriptor StaticMethodRequired = new(
        "COVE001",
        "Command method must be static",
        "Command method '{0}' must be static",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor SingleParameterRequired = new(
        "COVE002",
        "Command method must have one parameter",
        "Command method '{0}' must have exactly one parameter",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor CommandKeyRequired = new(
        "COVE003",
        "Command key must not be empty",
        "Command method '{0}' must declare a non-empty command key",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor DuplicateCommandKey = new(
        "COVE004",
        "Command key must be unique",
        "Command key '{0}' is declared more than once",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedParameter = new(
        "COVE005",
        "Command parameter must be supported",
        "Command parameter '{0}' has unsupported type or modifier",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        "COVE006",
        "Command return type must be supported",
        "Command method '{0}' has unsupported return type '{1}'",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InaccessibleMethod = new(
        "COVE010",
        "Command method must be accessible",
        "Command method '{0}' must be accessible to generated dispatch",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeFullName,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) => Extract(ctx));

        context.RegisterSourceOutput(
            candidates.Where(static c => c.Diagnostic != CommandDiagnostic.None),
            static (spc, candidate) => ReportDiagnostic(spc, candidate));

        var commands = candidates.Where(static c => c.Diagnostic == CommandDiagnostic.None && c.Key != null);
        context.RegisterSourceOutput(commands.Collect(), static (spc, items) => ProcessCommands(spc, items));
    }

    private static CommandModel Extract(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return default;
        var syntax = (MethodDeclarationSyntax)ctx.TargetNode;
        var location = syntax.GetLocation();
        if (!method.IsStatic)
            return CommandModel.Invalid(CommandDiagnostic.StaticMethodRequired, method.Name, location);
        if (method.Parameters.Length != 1)
            return CommandModel.Invalid(CommandDiagnostic.SingleParameterRequired, method.Name, location);
        var attr = ctx.Attributes[0];
        var attributeSyntax = attr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
        var keyLocation = attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault()?.Expression.GetLocation() ?? location;
        if (attr.ConstructorArguments.Length < 1)
            return CommandModel.Invalid(CommandDiagnostic.CommandKeyRequired, method.Name, keyLocation);
        var key = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(key))
            return CommandModel.Invalid(CommandDiagnostic.CommandKeyRequired, method.Name, keyLocation);
        if (!IsAccessibleFromGeneratedCode(method))
            return CommandModel.Invalid(
                CommandDiagnostic.InaccessibleMethod,
                method.Name,
                syntax.Identifier.GetLocation());
        var parameter = method.Parameters[0];
        if (method.IsGenericMethod ||
            parameter.RefKind != RefKind.None ||
            !IsSupportedDelegateType(parameter.Type))
        {
            return CommandModel.Invalid(
                CommandDiagnostic.UnsupportedParameter,
                method.Name,
                syntax.ParameterList.Parameters[0].GetLocation(),
                parameter.Name);
        }
        if (method.ReturnsByRef ||
            method.ReturnsByRefReadonly ||
            method.ReturnType.SpecialType == SpecialType.System_Void ||
            !IsSupportedDelegateType(method.ReturnType))
        {
            return CommandModel.Invalid(
                CommandDiagnostic.UnsupportedReturnType,
                method.Name,
                syntax.ReturnType.GetLocation(),
                method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        var isCore = key!.StartsWith(
            "cove://",
            System.StringComparison.Ordinal);
        if (!IsExactParameterContract(parameter.Type, isCore))
        {
            return CommandModel.Invalid(
                CommandDiagnostic.UnsupportedParameter,
                method.Name,
                syntax.ParameterList.Parameters[0].GetLocation(),
                parameter.Name);
        }
        if (!IsExactReturnContract(method.ReturnType, isCore))
        {
            return CommandModel.Invalid(
                CommandDiagnostic.UnsupportedReturnType,
                method.Name,
                syntax.ReturnType.GetLocation(),
                method.ReturnType.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        var containing = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var paramType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string? description = null;
        string? source = null;
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == "Description" && na.Value.Value is string d) description = d;
            else if (na.Key == "Source" && na.Value.Value is string s) source = s;
        }
        if (source is null)
            source = key!.StartsWith("cove://", System.StringComparison.Ordinal) ? "core" : "cli";
        return new CommandModel(key!, containing, method.Name, paramType, returnType, description, source, keyLocation);
    }

    private static bool IsSupportedDelegateType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Error or TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.TypeParameter)
            return false;
        if (type.IsRefLikeType)
            return false;
        if (type is IArrayTypeSymbol array)
            return IsSupportedDelegateType(array.ElementType);
        if (type is INamedTypeSymbol named)
            return named.TypeArguments.All(IsSupportedDelegateType);
        return true;
    }

    private static bool IsAccessibleFromGeneratedCode(
        IMethodSymbol method)
    {
        if (!IsAssemblyAccessible(method.DeclaredAccessibility))
            return false;

        for (var type = method.ContainingType;
             type is not null;
             type = type.ContainingType)
        {
            if (!IsAssemblyAccessible(type.DeclaredAccessibility))
                return false;
        }

        return true;
    }

    private static bool IsAssemblyAccessible(
        Accessibility accessibility) =>
        accessibility is
            Accessibility.Public or
            Accessibility.Internal or
            Accessibility.ProtectedOrInternal;

    private static bool IsExactParameterContract(
        ITypeSymbol type,
        bool isCore) =>
        IsNamedType(
            type,
            isCore ? "Cove.Engine" : "Cove.Cli",
            isCore
                ? "EngineDispatchContext"
                : "CommandContext");

    private static bool IsExactReturnContract(
        ITypeSymbol type,
        bool isCore) =>
        type is INamedTypeSymbol
        {
            Name: "Task",
            Arity: 1
        } task &&
        task.ContainingNamespace.ToDisplayString() ==
            "System.Threading.Tasks" &&
        (isCore
            ? IsNamedType(
                task.TypeArguments[0],
                "Cove.Protocol",
                "ControlResponse")
            : task.TypeArguments[0].SpecialType ==
                SpecialType.System_Int32);

    private static bool IsNamedType(
        ITypeSymbol type,
        string namespaceName,
        string typeName) =>
        type is INamedTypeSymbol named &&
        named.Name == typeName &&
        named.ContainingNamespace.ToDisplayString() ==
            namespaceName;

    private static void ReportDiagnostic(SourceProductionContext spc, CommandModel candidate)
    {
        var descriptor = candidate.Diagnostic switch
        {
            CommandDiagnostic.StaticMethodRequired => StaticMethodRequired,
            CommandDiagnostic.SingleParameterRequired => SingleParameterRequired,
            CommandDiagnostic.CommandKeyRequired => CommandKeyRequired,
            CommandDiagnostic.UnsupportedParameter => UnsupportedParameter,
            CommandDiagnostic.UnsupportedReturnType => UnsupportedReturnType,
            CommandDiagnostic.InaccessibleMethod => InaccessibleMethod,
            _ => null
        };
        if (descriptor is not null)
            spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(
                descriptor,
                candidate.Location,
                candidate.Diagnostic == CommandDiagnostic.UnsupportedParameter
                    ? new object[] { candidate.Detail! }
                    : candidate.Diagnostic == CommandDiagnostic.UnsupportedReturnType
                        ? new object[] { candidate.MethodName, candidate.Detail! }
                        : new object[] { candidate.MethodName }));
    }

    private static void ProcessCommands(SourceProductionContext spc, ImmutableArray<CommandModel> items)
    {
        var duplicates = items
            .GroupBy(item => item.Key, System.StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, System.StringComparer.Ordinal)
            .ToArray();
        foreach (var duplicate in duplicates)
        {
            foreach (var item in duplicate.OrderBy(item => item.Location?.SourceTree?.FilePath, System.StringComparer.Ordinal)
                         .ThenBy(item => item.Location?.SourceSpan.Start))
            {
                spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(
                    DuplicateCommandKey,
                    item.Location,
                    duplicate.Key));
            }
        }
        if (duplicates.Length == 0)
            Emit(spc, items);
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<CommandModel> items)
    {
        var ordered = items.Where(x => x.Key != null).OrderBy(x => x.Key, System.StringComparer.Ordinal).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Cove.Generated;");
        sb.AppendLine("internal static class CoveCommandRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate> Handlers =");
        sb.AppendLine("        new System.Collections.Generic.Dictionary<string, System.Delegate>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var c in ordered)
            sb.AppendLine($"            [{Literal(c.Key)}] = (System.Func<{c.ParamType}, {c.ReturnType}>){c.ContainingType}.{c.MethodName},");
        sb.AppendLine("        };");
        sb.Append("    public static readonly System.Collections.Generic.IReadOnlyList<string> Keys = new string[] { ");
        foreach (var c in ordered) sb.Append($"{Literal(c.Key)}, ");
        sb.AppendLine("};");
        sb.AppendLine("    public sealed record CommandCatalogueEntry(string Command, string? Description, string Source);");
        sb.AppendLine("    public static readonly System.Collections.Generic.IReadOnlyList<CommandCatalogueEntry> Catalogue = new CommandCatalogueEntry[]");
        sb.AppendLine("    {");
        foreach (var c in ordered)
            sb.AppendLine($"        new CommandCatalogueEntry({Literal(c.Key)}, {(c.Description is null ? "null" : Literal(c.Description))}, {Literal(c.Source)}),");
        sb.AppendLine("    };");
        sb.AppendLine("}");
        spc.AddSource("CoveCommandRegistry.g.cs", sb.ToString());
    }
    private static string Literal(string value) => SyntaxFactory.Literal(value).ToFullString();

    private readonly struct CommandModel
    {
        public readonly string Key;
        public readonly string ContainingType;
        public readonly string MethodName;
        public readonly string ParamType;
        public readonly string ReturnType;
        public readonly string? Description;
        public readonly string Source;
        public readonly CommandDiagnostic Diagnostic;
        public readonly Location? Location;
        public readonly string? Detail;

        public CommandModel(string key, string containingType, string methodName, string paramType, string returnType, string? description, string source, Location keyLocation)
        {
            Key = key;
            ContainingType = containingType;
            MethodName = methodName;
            ParamType = paramType;
            ReturnType = returnType;
            Description = description;
            Source = source;
            Diagnostic = CommandDiagnostic.None;
            Location = keyLocation;
            Detail = null;
        }

        private CommandModel(CommandDiagnostic diagnostic, string methodName, Location location, string? detail)
        {
            Key = null!;
            ContainingType = null!;
            MethodName = methodName;
            ParamType = null!;
            ReturnType = null!;
            Description = null;
            Source = null!;
            Diagnostic = diagnostic;
            Location = location;
            Detail = detail;
        }

        public static CommandModel Invalid(CommandDiagnostic diagnostic, string methodName, Location location, string? detail = null) =>
            new(diagnostic, methodName, location, detail);
    }

    private enum CommandDiagnostic
    {
        None,
        StaticMethodRequired,
        SingleParameterRequired,
        CommandKeyRequired,
        UnsupportedParameter,
        UnsupportedReturnType,
        InaccessibleMethod
    }
}
