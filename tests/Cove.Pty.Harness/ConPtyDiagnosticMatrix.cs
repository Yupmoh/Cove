using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class ConPtyDiagnosticMatrix
{
    private const int CaptureMilliseconds = 2000;

    [Trait("Category", "PtyInteractive")]
    [Fact]
    public void ConPtyVariantMatrixReportsWhichSpawnShapeSurfacesChildOutput()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var variants = new (string Id, string Description, ConPtyDiagnosticOptions Options)[]
        {
            ("A", "production baseline", new ConPtyDiagnosticOptions()),
            ("B", "inherit parent env, no CREATE_UNICODE_ENVIRONMENT", new ConPtyDiagnosticOptions { InheritParentEnvironment = true, IncludeUnicodeEnvironmentFlag = false }),
            ("C", "inherit parent env, keep CREATE_UNICODE_ENVIRONMENT", new ConPtyDiagnosticOptions { InheritParentEnvironment = true, IncludeUnicodeEnvironmentFlag = true }),
            ("D", "production env, literal command line (no quoting)", new ConPtyDiagnosticOptions { CommandLineOverride = "cmd.exe /c echo hello" }),
            ("E", "production path, explicitly zeroed stdio fields", new ConPtyDiagnosticOptions { ExplicitZeroStdHandles = true }),
            ("F", "explicit non-inheritable pipe security attributes", new ConPtyDiagnosticOptions { ExplicitNonInheritablePipes = true }),
        };

        var report = new StringBuilder();
        report.Append("ConPtyDiagnosticMatrix: spawned `cmd.exe /c echo hello` under ").Append(variants.Length).Append(" variants (~2s capture each).\n");

        foreach (var variant in variants)
        {
            report.Append('\n').Append(RunVariant(variant.Id, variant.Description, variant.Options));
        }

        Assert.Fail(report.ToString());
    }

    private static string RunVariant(string id, string description, ConPtyDiagnosticOptions options)
    {
        var logger = NullLogger.Instance;
        var request = new PtySpawnRequest
        {
            Command = "cmd.exe",
            Args = new[] { "/c", "echo hello" },
            Cols = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["COVE_NOOK_ID"] = "conpty-matrix" },
        };

        ConPtyDiagnosticSpawn spawn;
        try
        {
            spawn = WindowsPtyHost.SpawnWithOptions(request, options, logger);
        }
        catch (Exception ex)
        {
            return $"[{id}] {description}: SPAWN THREW {ex.GetType().Name}: {ex.Message}";
        }

        var session = spawn.Session;
        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, logger);
        reader.Start();
        try
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < CaptureMilliseconds)
                Thread.Sleep(10);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
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
            line.Append("      startupInfo.dwFlags=").Append(spawn.StartupFlags);
            line.Append(" hStdIn=").Append(spawn.StdInput);
            line.Append(" hStdOut=").Append(spawn.StdOutput);
            line.Append(" hStdErr=").Append(spawn.StdError).Append('\n');
            line.Append("      commandLine=\"").Append(spawn.CommandLine).Append("\"\n");
            line.Append("      capture=\"").Append(DescribeCapture(text)).Append('"');
            return line.ToString();
        }
        finally
        {
            reader.Dispose();
            session.Dispose();
        }
    }

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
