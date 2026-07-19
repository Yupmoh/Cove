using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cove.SourceGen;

[Generator(LanguageNames.CSharp)]
public sealed class SettingSchemaGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Cove.Engine.Config.SettingAttribute";
    private static readonly DiagnosticDescriptor DuplicateSettingKey = new(
        "COVE007",
        "Setting key must be unique",
        "Setting key '{0}' is declared more than once",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedSettingType = new(
        "COVE008",
        "Setting type must match its control",
        "Setting '{0}' has unsupported type '{1}' for control '{2}'",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedSettingOptions = new(
        "COVE009",
        "Setting options must match its control",
        "Setting '{0}' has unsupported options for control '{1}'",
        "Cove.SourceGen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settings = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (ctx, _) => Extract(ctx));

        var collected = settings.Collect();
        context.RegisterSourceOutput(collected, static (spc, items) => Process(spc, items));
    }

    private static SettingModel Extract(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IPropertySymbol prop) return default;
        var syntax = (PropertyDeclarationSyntax)ctx.TargetNode;
        var attr = ctx.Attributes[0];
        if (attr.ConstructorArguments.Length < 2) return default;
        var label = attr.ConstructorArguments[0].Value as string;
        var tab = attr.ConstructorArguments[1].Value as string;
        var control = attr.ConstructorArguments.Length >= 3 ? attr.ConstructorArguments[2].Value as string ?? "text" : "text";
        string? description = null;
        if (attr.AttributeConstructor is { } constructor)
        {
            for (var i = 0; i < constructor.Parameters.Length && i < attr.ConstructorArguments.Length; i++)
            {
                if (constructor.Parameters[i].Name == "description")
                {
                    description = attr.ConstructorArguments[i].Value as string;
                    break;
                }
            }
        }
        string[]? options = null;
        Location? optionsLocation = null;
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == "Description") description = na.Value.Value as string;
            if (na.Key == "Options" && na.Value.Values is { } vals)
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (var v in vals) if (v.Value is string s) list.Add(s);
                options = list.Count > 0 ? list.ToArray() : null;
            }
        }

        var attributeSyntax = attr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
        Location? controlLocation = null;
        if (attributeSyntax?.ArgumentList is { } arguments)
        {
            optionsLocation = arguments.Arguments.FirstOrDefault(argument =>
                argument.NameEquals?.Name.Identifier.ValueText == "Options")?.GetLocation();
            controlLocation = arguments.Arguments
                .Where(argument => argument.NameEquals is null && argument.NameColon is null)
                .Skip(2)
                .FirstOrDefault()
                ?.Expression.GetLocation();
        }

        var type = ResolveSchemaType(prop.Type, control);
        var diagnostic = SettingDiagnostic.None;
        var diagnosticLocation = syntax.GetLocation();
        if (type is null)
        {
            diagnostic = SettingDiagnostic.UnsupportedType;
            diagnosticLocation = syntax.Type.GetLocation();
        }
        else if (options is { Length: > 0 } && control != "select")
        {
            diagnostic = SettingDiagnostic.UnsupportedOptions;
            diagnosticLocation = optionsLocation ?? attributeSyntax?.GetLocation() ?? syntax.GetLocation();
        }
        else if (control == "select" && options is not { Length: > 0 })
        {
            diagnostic = SettingDiagnostic.UnsupportedOptions;
            diagnosticLocation = optionsLocation ?? controlLocation ?? attributeSyntax?.GetLocation() ?? syntax.GetLocation();
        }

        return new SettingModel(
            prop.Name,
            prop.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            label!,
            tab!,
            control,
            description,
            type,
            options,
            diagnostic,
            diagnosticLocation,
            syntax.GetLocation(),
            prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    private static string? ResolveSchemaType(ITypeSymbol type, string control)
    {
        return control switch
        {
            "toggle" when type.SpecialType == SpecialType.System_Boolean => "bool",
            "number" when type.SpecialType == SpecialType.System_Int32 => "int",
            "number" when type.SpecialType == SpecialType.System_Double => "double",
            "select" when type.SpecialType == SpecialType.System_String => "string",
            "section" when type.IsReferenceType && type.SpecialType != SpecialType.System_String => "object",
            "text" when type.SpecialType == SpecialType.System_String => "string",
            "text" when Implements(type, "IDictionary", 2) => "object",
            "text" when Implements(type, "IEnumerable", 1) => "array",
            _ => null
        };
    }

    private static bool Implements(ITypeSymbol type, string interfaceName, int arity)
    {
        return type is INamedTypeSymbol named &&
               named.AllInterfaces.Any(candidate =>
                   candidate.Name == interfaceName &&
                   candidate.Arity == arity &&
                   candidate.ContainingNamespace.ToDisplayString() == "System.Collections.Generic");
    }

    private static string CamelCase(string s)
    {
        if (s.Length == 0) return s;
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    private static string SnakeCase(string value)
    {
        var sb = new StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0 &&
                (!char.IsUpper(value[i - 1]) || i + 1 < value.Length && char.IsLower(value[i + 1])))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(current));
        }
        return sb.ToString();
    }

    private static void Process(SourceProductionContext spc, ImmutableArray<SettingModel> items)
    {
        var hasErrors = false;
        foreach (var item in items.Where(item => item.Diagnostic != SettingDiagnostic.None))
        {
            var descriptor = item.Diagnostic == SettingDiagnostic.UnsupportedType
                ? UnsupportedSettingType
                : UnsupportedSettingOptions;
            var arguments = item.Diagnostic == SettingDiagnostic.UnsupportedType
                ? new object[] { item.PropertyName, item.TypeDisplay, item.Control }
                : new object[] { item.PropertyName, item.Control };
            spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(
                descriptor,
                item.DiagnosticLocation,
                arguments));
            hasErrors = true;
        }

        var valid = items.Where(item => item.Diagnostic == SettingDiagnostic.None && item.Label != null).ToArray();
        var sectionPrefixes = valid
            .Where(item => item.Control == "section")
            .GroupBy(item => item.PropertyType, System.StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => SnakeCase(group.OrderBy(item => item.PropertyName, System.StringComparer.Ordinal).First().PropertyName),
                System.StringComparer.Ordinal);
        var resolved = valid
            .Select(item => item.ResolveKey(ResolvePrefix(item, sectionPrefixes)))
            .ToArray();
        var duplicates = resolved
            .GroupBy(item => item.Key, System.StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, System.StringComparer.Ordinal)
            .ToArray();
        foreach (var duplicate in duplicates)
        {
            foreach (var item in duplicate.OrderBy(item => item.DeclarationLocation.SourceTree?.FilePath, System.StringComparer.Ordinal)
                         .ThenBy(item => item.DeclarationLocation.SourceSpan.Start))
            {
                spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(
                    DuplicateSettingKey,
                    item.DeclarationLocation,
                    duplicate.Key));
            }
            hasErrors = true;
        }

        if (!hasErrors)
            Emit(spc, resolved.ToImmutableArray());
    }

    private static string? ResolvePrefix(
        SettingModel item,
        System.Collections.Generic.IReadOnlyDictionary<string, string> sectionPrefixes)
    {
        if (sectionPrefixes.TryGetValue(item.ContainingType, out var prefix))
            return prefix;
        var simpleNameStart = item.ContainingType.LastIndexOf('.') + 1;
        var simpleName = item.ContainingType.Substring(simpleNameStart);
        const string suffix = "Section";
        if (simpleName.EndsWith(suffix, System.StringComparison.Ordinal) && simpleName.Length > suffix.Length)
            return SnakeCase(simpleName.Substring(0, simpleName.Length - suffix.Length));
        return null;
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<SettingModel> items)
    {
        var ordered = items.Where(x => x.Label != null).OrderBy(x => x.Key, System.StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Cove.Generated;");
        sb.AppendLine();
        sb.AppendLine("public sealed record SettingSchemaEntry(string Key, string Label, string Tab, string Control, string? Description, string Type, string[]? Options);");
        sb.AppendLine();
        sb.AppendLine("public static class CoveSettingSchema");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly System.Collections.Generic.IReadOnlyList<SettingSchemaEntry> Entries = new SettingSchemaEntry[]");
        sb.AppendLine("    {");
        foreach (var s in ordered)
        {
            var optionsExpr = s.Options is null || s.Options.Length == 0
                ? "null"
                : "new string[] { " + string.Join(", ", s.Options.Select(Literal)) + " }";
            sb.AppendLine($"        new SettingSchemaEntry({Literal(s.Key)}, {Literal(s.Label)}, {Literal(s.Tab)}, {Literal(s.Control)}, {(s.Description is null ? "null" : Literal(s.Description))}, {Literal(s.Type)}, {optionsExpr}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        spc.AddSource("CoveSettingSchema.g.cs", sb.ToString());
    }
    private static string Literal(string value) => SyntaxFactory.Literal(value).ToFullString();

    private readonly struct SettingModel
    {
        public readonly string PropertyName;
        public readonly string ContainingType;
        public readonly string PropertyType;
        public readonly string Key;
        public readonly string Label;
        public readonly string Tab;
        public readonly string Control;
        public readonly string? Description;
        public readonly string Type;
        public readonly string[]? Options;
        public readonly SettingDiagnostic Diagnostic;
        public readonly Location DiagnosticLocation;
        public readonly Location DeclarationLocation;
        public readonly string TypeDisplay;

        public SettingModel(
            string propertyName,
            string containingType,
            string propertyType,
            string label,
            string tab,
            string control,
            string? description,
            string? type,
            string[]? options,
            SettingDiagnostic diagnostic,
            Location diagnosticLocation,
            Location declarationLocation,
            string typeDisplay,
            string? key = null)
        {
            PropertyName = propertyName;
            ContainingType = containingType;
            PropertyType = propertyType;
            Key = key!;
            Label = label;
            Tab = tab;
            Control = control;
            Description = description;
            Type = type!;
            Options = options;
            Diagnostic = diagnostic;
            DiagnosticLocation = diagnosticLocation;
            DeclarationLocation = declarationLocation;
            TypeDisplay = typeDisplay;
        }

        public SettingModel ResolveKey(string? prefix)
            => new(
                PropertyName,
                ContainingType,
                PropertyType,
                Label,
                Tab,
                Control,
                Description,
                Type,
                Options,
                Diagnostic,
                DiagnosticLocation,
                DeclarationLocation,
                TypeDisplay,
                prefix is null ? CamelCase(PropertyName) : $"{prefix}.{CamelCase(PropertyName)}");
    }

    private enum SettingDiagnostic
    {
        None,
        UnsupportedType,
        UnsupportedOptions
    }
}
