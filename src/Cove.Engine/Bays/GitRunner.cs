using System.Diagnostics;

namespace Cove.Engine.Bays;

public sealed record GitResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Ok => ExitCode == 0;
}

public interface IGitRunner
{
    Task<GitResult> RunAsync(string workingDir, IReadOnlyList<string> args, CancellationToken cancellationToken = default);
}

public sealed class ProcessGitRunner : IGitRunner
{
    public async Task<GitResult> RunAsync(string workingDir, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new GitResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }
}
