; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
COVE001 | Cove.SourceGen | Error | Attributed command method must be static.
COVE002 | Cove.SourceGen | Error | Attributed command method must have exactly one parameter.
COVE003 | Cove.SourceGen | Error | Attributed command must have a non-empty key.
COVE004 | Cove.SourceGen | Error | Attributed command keys must be unique.
COVE005 | Cove.SourceGen | Error | Attributed command parameters must be representable by the generated delegate registry.
COVE006 | Cove.SourceGen | Error | Attributed command return types must be representable by the generated delegate registry.
COVE007 | Cove.SourceGen | Error | Generated setting keys must be unique.
COVE008 | Cove.SourceGen | Error | Setting property types must match their declared controls.
COVE009 | Cove.SourceGen | Error | Setting options are only supported by select controls.
COVE010 | Cove.SourceGen | Error | Attributed command methods must be accessible to generated dispatch.
