using System.Text.Json.Serialization;

namespace Cove.Engine.Restart;

public sealed class ResumeFailedException(string message) : Exception(message) { }

public sealed record LauncherOverrides
{
    public bool Yolo { get; init; }
    public string? WorkingDir { get; init; }
    private readonly IReadOnlyDictionary<string, string>? _env;
    public IReadOnlyDictionary<string, string> Env { get => _env ?? new Dictionary<string, string>(); init => _env = value; }
    private readonly IReadOnlyList<string>? _extraFlags;
    public IReadOnlyList<string> ExtraFlags { get => _extraFlags ?? []; init => _extraFlags = value; }
}

public sealed record ResumeCommand(string Command, IReadOnlyList<string> Args, string Cwd);

public enum AgentResumeState { Resuming, Succeeded, Failed }

public enum NudgeKind { RetryOrStartFresh, StartFresh }

public sealed record ResumeNudge(NudgeKind Kind, string Message);

public sealed record AgentResumeResult(AgentResumeState State, ResumeCommand? Command, ResumeNudge? Nudge, string? SessionId);

public interface IAdapterResume
{
    Task<ResumeCommand> BuildResumeCommandAsync(
        string adapter,
        string sessionId,
        LauncherOverrides overrides,
        CancellationToken cancellationToken);
    Task WaitForReadiness(string sessionId, CancellationToken cancellationToken);
    bool IsSessionReaped(string sessionId);
}

public sealed class AgentResumeService
{
    private readonly IAdapterResume _adapter;

    public AgentResumeService(IAdapterResume adapter) => _adapter = adapter;

    public async Task<AgentResumeResult> ResumeAsync(
        string adapter,
        string sessionId,
        LauncherOverrides overrides,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_adapter.IsSessionReaped(sessionId))
                return new AgentResumeResult(AgentResumeState.Succeeded, FreshLaunch(overrides), null, sessionId);

            try
            {
                await _adapter.WaitForReadiness(sessionId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new AgentResumeResult(AgentResumeState.Failed, null, new ResumeNudge(NudgeKind.RetryOrStartFresh, "resume cancelled"), sessionId);
            }
            catch (ResumeFailedException ex)
            {
                return new AgentResumeResult(AgentResumeState.Failed, null, new ResumeNudge(NudgeKind.RetryOrStartFresh, ex.Message), sessionId);
            }

            if (cancellationToken.IsCancellationRequested)
                return new AgentResumeResult(AgentResumeState.Failed, null, new ResumeNudge(NudgeKind.RetryOrStartFresh, "cancelled before spawn"), sessionId);

            ResumeCommand command;
            try
            {
                command = await _adapter.BuildResumeCommandAsync(
                    adapter,
                    sessionId,
                    overrides,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ResumeFailedException ex)
            {
                return new AgentResumeResult(AgentResumeState.Failed, null, new ResumeNudge(NudgeKind.RetryOrStartFresh, ex.Message), sessionId);
            }

            return new AgentResumeResult(AgentResumeState.Succeeded, command, null, sessionId);
        }
        catch (Exception ex)
        {
            return new AgentResumeResult(AgentResumeState.Failed, null, new ResumeNudge(NudgeKind.RetryOrStartFresh, ex.Message), sessionId);
        }
    }

    private static ResumeCommand FreshLaunch(LauncherOverrides overrides)
    {
        var args = new List<string>();
        if (overrides.Yolo)
            args.Add("--dangerously-skip-permissions");
        foreach (var flag in overrides.ExtraFlags)
            args.Add(flag);
        foreach (var env in overrides.Env)
            args.Add($"--env={env.Key}={env.Value}");
        return new ResumeCommand("agent", args, overrides.WorkingDir ?? "");
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LauncherOverrides))]
[JsonSerializable(typeof(ResumeCommand))]
[JsonSerializable(typeof(AgentResumeResult))]
public sealed partial class AgentResumeJsonContext : JsonSerializerContext { }
