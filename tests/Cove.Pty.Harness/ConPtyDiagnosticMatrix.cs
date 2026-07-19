using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Windows;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class ConPtyDiagnosticMatrix
{
    private const int CaptureMilliseconds = 2000;
    private const int LongCaptureMilliseconds = 4000;

    [Trait("Suite", "PtyInteractive")]
    [PlatformFact(TestOperatingSystem.Windows)]
    public async Task ConPtyVariantMatrixReportsWhichSpawnShapeSurfacesChildOutput()
    {
        var cmdArgs = new[] { "/c", "echo hello" };
        var variants = new (string Id, string Description, string Command, string[] Args, int CaptureMs, ConPtyDiagnosticOptions Options)[]
        {
            ("A", "production baseline", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions()),
            ("B", "inherit parent env, no CREATE_UNICODE_ENVIRONMENT", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { InheritParentEnvironment = true, IncludeUnicodeEnvironmentFlag = false }),
            ("C", "inherit parent env, keep CREATE_UNICODE_ENVIRONMENT", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { InheritParentEnvironment = true, IncludeUnicodeEnvironmentFlag = true }),
            ("D", "production env, literal command line (no quoting)", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { CommandLineOverride = "cmd.exe /c echo hello" }),
            ("E", "production path, explicitly zeroed stdio fields", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { ExplicitZeroStdHandles = true }),
            ("F", "explicit non-inheritable pipe security attributes", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { ExplicitNonInheritablePipes = true }),
            ("G", "EchoCon-faithful: keep conpty-side pipe ends alive until close", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { KeepConptySideHandles = true }),
            ("H", "no watcher close: only dispose closes the pseudoconsole", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { SuppressWatcherClose = true }),
            ("I", "long-lived child: ping.exe -n 3 127.0.0.1, production options", "ping.exe", new[] { "-n", "3", "127.0.0.1" }, LongCaptureMilliseconds, new ConPtyDiagnosticOptions()),
            ("J", "cmd via full path, production options", @"C:\Windows\System32\cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions()),
            ("K", "G+H: keep conpty handles AND never watcher-close", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { KeepConptySideHandles = true, SuppressWatcherClose = true }),
            ("L", "powershell.exe -NoProfile -Command Write-Output hello", "powershell.exe", new[] { "-NoProfile", "-Command", "Write-Output hello" }, LongCaptureMilliseconds, new ConPtyDiagnosticOptions()),
            ("M", "CreatePseudoConsole with PSEUDOCONSOLE_INHERIT_CURSOR", "cmd.exe", cmdArgs, CaptureMilliseconds, new ConPtyDiagnosticOptions { InheritCursor = true }),
        };

        var report = new StringBuilder();
        report.Append("ConPtyDiagnosticMatrix: spawned per-variant children under ").Append(variants.Length).Append(" variants.\n");
        var results = new List<VariantResult>(variants.Length);

        foreach (var variant in variants)
        {
            var result = await RunVariant(variant.Id, variant.Description, variant.Command, variant.Args, variant.CaptureMs, variant.Options);
            results.Add(result);
            report.Append('\n').Append(result.Report);
        }

        Assert.True(results.Single(result => result.Id == "A").SawHello, report.ToString());
    }

    private static async Task<VariantResult> RunVariant(string id, string description, string command, string[] args, int captureMs, ConPtyDiagnosticOptions options)
    {
        var logger = NullLogger.Instance;
        var request = new PtySpawnRequest
        {
            Command = command,
            Args = args,
            Cols = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["COVE_NOOK_ID"] = "conpty-matrix" },
        };

        var spawn = WindowsPtyHost.SpawnWithOptions(request, options, logger);

        var session = spawn.Session;
        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, logger);
        reader.Start();
        try
        {
            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
            try
            {
                await AsyncTest.EventuallyAsync(
                    () =>
                    {
                        delivery.PumpAvailable();
                        return reader.HasCompleted || sink.Contains("hello"u8);
                    },
                    TimeSpan.FromMilliseconds(captureMs),
                    $"variant {id} produced neither output nor completion");
            }
            catch (TimeoutException)
            {
            }
            delivery.PumpAvailable();
            byte[] raw = sink.Delivered;
            string text = Encoding.UTF8.GetString(raw);
            bool sawHello = text.Contains("hello");

            var line = new StringBuilder();
            line.Append('[').Append(id).Append("] ").Append(description).Append('\n');
            line.Append("      hello=").Append(sawHello ? "YES" : "no");
            line.Append(" bytes=").Append(raw.Length);
            line.Append(" completed=").Append(reader.HasCompleted);
            line.Append(" exitCode=").Append(reader.ExitCode).Append('\n');
            line.Append("      creationFlags=0x").Append(spawn.CreationFlags.ToString("x8"));
            line.Append(" envInherited=").Append(spawn.EnvironmentInherited).Append('\n');
            line.Append("      hpconSize=").Append(spawn.PseudoConsoleCols).Append('x').Append(spawn.PseudoConsoleRows);
            line.Append(" hpconValid=").Append(spawn.PseudoConsoleValid);
            line.Append(" updateAttrLastError=").Append(spawn.UpdateAttributeLastError).Append('\n');
            line.Append("      startupInfo.dwFlags=").Append(spawn.StartupFlags);
            line.Append(" hStdIn=").Append(spawn.StdInput);
            line.Append(" hStdOut=").Append(spawn.StdOutput);
            line.Append(" hStdErr=").Append(spawn.StdError).Append('\n');
            line.Append("      commandLine=\"").Append(spawn.CommandLine).Append("\"\n");
            line.Append("      capture=\"").Append(DescribeCapture(text)).Append('"');
            return new VariantResult(id, sawHello, line.ToString());
        }
        finally
        {
            try
            {
                reader.Dispose();
            }
            finally
            {
                session.Dispose();
            }
        }
    }

    private sealed record VariantResult(string Id, bool SawHello, string Report);

    private static string DescribeCapture(string text)
    {
        const int limit = 300;
        var escaped = new StringBuilder(text.Length + 16);
        foreach (char c in text)
        {
            if (escaped.Length >= limit)
            {
                escaped.Append("...");
                break;
            }
            if (c == '\\')
                escaped.Append("\\\\");
            else if (c == '\x1b')
                escaped.Append("\\e");
            else if (c == '\r')
                escaped.Append("\\r");
            else if (c == '\n')
                escaped.Append("\\n");
            else if (c < 0x20 || c == 0x7f)
                escaped.Append("\\x").Append(((int)c).ToString("x2"));
            else
                escaped.Append(c);
        }
        return escaped.ToString();
    }
}
