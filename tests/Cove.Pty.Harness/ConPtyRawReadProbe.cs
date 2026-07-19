using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Windows;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class ConPtyRawReadProbe
{
    [Trait("Suite", "PtyInteractive")]
    [PlatformFact(TestOperatingSystem.Windows)]
    public async Task ConPtyRawReadSurfacesChildStdout()
    {
        var logger = NullLogger.Instance;
        var request = new PtySpawnRequest
        {
            Command = "cmd.exe",
            Args = new[] { "/c", "echo hello" },
            Cols = 80,
            Rows = 24,
            Environment = new System.Collections.Generic.Dictionary<string, string>
            {
                ["COVE_NOOK_ID"] = "conpty-rawprobe",
            },
        };

        var spawn = WindowsPtyHost.SpawnWithOptions(request, ConPtyDiagnosticOptions.Production, logger);
        var session = spawn.Session;
        var report = new StringBuilder();
        report.Append("ConPtyRawReadProbe: direct session.Read() loop.\n");
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        bool sawHello = false;
        var buffer = new byte[8192];
        Task<int>? pendingRead = null;
        Exception? failure = null;
        try
        {
            while (sw.Elapsed.TotalSeconds < 8 && !sawHello)
            {
                var remaining = TimeSpan.FromSeconds(8) - sw.Elapsed;
                pendingRead = Task.Run(() => session.Read(buffer));
                int n;
                try
                {
                    n = await pendingRead.WaitAsync(remaining);
                }
                catch (TimeoutException exception)
                {
                    report.Append($"RESULT: read timed out after {sw.ElapsedMilliseconds}ms totalBytes={totalBytes} hasExited={session.HasExited} exitCode={session.ExitCode}");
                    throw new TimeoutException(
                        $"ConPTY session {session.SessionId} read did not complete within 8 seconds.\n{report}",
                        exception);
                }
                catch
                {
                    pendingRead = null;
                    throw;
                }
                pendingRead = null;
                if (n == 0)
                {
                    report.Append($"  [t={sw.ElapsedMilliseconds}ms] READ EOF (n=0) hasExited={session.HasExited} exitCode={session.ExitCode}\n");
                    break;
                }
                totalBytes += n;
                var chunk = Encoding.UTF8.GetString(buffer, 0, n);
                report.Append($"  [t={sw.ElapsedMilliseconds}ms] READ n={n} total={totalBytes} hasExited={session.HasExited}\n");
                report.Append("        chunk=\"").Append(Escape(buffer, n)).Append("\"\n");
                if (chunk.Contains("hello"))
                    sawHello = true;
            }
            report.Append($"RESULT: sawHello={sawHello} totalBytes={totalBytes} hasExited={session.HasExited} exitCode={session.ExitCode}");
            Assert.True(sawHello, report.ToString());
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            var cleanupFailures = new System.Collections.Generic.List<Exception>();
            try
            {
                session.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(new InvalidOperationException(
                    $"ConPTY session {session.SessionId} disposal failed.",
                    exception));
            }

            if (pendingRead is not null)
            {
                try
                {
                    await pendingRead.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException exception)
                {
                    cleanupFailures.Add(new InvalidOperationException(
                        $"ConPTY session {session.SessionId} read remained blocked for 5 seconds after disposal.",
                        exception));
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(new InvalidOperationException(
                        $"ConPTY session {session.SessionId} read failed while completing after disposal.",
                        exception));
                }
            }

            if (failure is not null && cleanupFailures.Count > 0)
            {
                cleanupFailures.Insert(0, failure);
                throw new AggregateException(cleanupFailures);
            }
            if (cleanupFailures.Count > 0)
                throw new AggregateException(cleanupFailures);
        }

        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static string Escape(byte[] raw, int length)
    {
        var text = Encoding.UTF8.GetString(raw, 0, length);
        var escaped = new StringBuilder(text.Length + 16);
        foreach (char c in text)
        {
            if (c == '\\') escaped.Append("\\\\");
            else if (c == '\x1b') escaped.Append("\\e");
            else if (c == '\r') escaped.Append("\\r");
            else if (c == '\n') escaped.Append("\\n");
            else if (c < 0x20 || c == 0x7f) escaped.Append("\\x").Append(((int)c).ToString("x2"));
            else escaped.Append(c);
        }
        return escaped.ToString();
    }
}
