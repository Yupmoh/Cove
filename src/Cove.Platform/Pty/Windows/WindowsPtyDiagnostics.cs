using System.Runtime.CompilerServices;
using Cove.Platform.Pty;

[assembly: InternalsVisibleTo("Cove.Pty.Harness")]

namespace Cove.Platform.Pty.Windows;

internal sealed class ConPtyDiagnosticOptions
{
    public bool InheritParentEnvironment { get; init; }
    public bool IncludeUnicodeEnvironmentFlag { get; init; } = true;
    public string? CommandLineOverride { get; init; }
    public bool ExplicitZeroStdHandles { get; init; }
    public bool ExplicitNonInheritablePipes { get; init; }
    public bool KeepConptySideHandles { get; init; }
    public bool SuppressWatcherClose { get; init; }
    public bool InheritCursor { get; init; }

    public static readonly ConPtyDiagnosticOptions Production = new();
}

internal readonly struct ConPtyDiagnosticSpawn
{
    public required IPtySession Session { get; init; }
    public int StartupFlags { get; init; }
    public long StdInput { get; init; }
    public long StdOutput { get; init; }
    public long StdError { get; init; }
    public uint CreationFlags { get; init; }
    public bool EnvironmentInherited { get; init; }
    public string CommandLine { get; init; }
    public int PseudoConsoleCols { get; init; }
    public int PseudoConsoleRows { get; init; }
    public bool PseudoConsoleValid { get; init; }
    public int UpdateAttributeLastError { get; init; }
}
