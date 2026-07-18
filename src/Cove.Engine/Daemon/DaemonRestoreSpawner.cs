using Cove.Engine.Agents;
using Cove.Engine.Hooks;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal sealed class DaemonRestoreSpawner : IRestoreSpawner
{
    private readonly NookRegistry _nooks;
    private readonly string _bayDir;
    private readonly string _bayId;
    private readonly AgentMessageRouter _agentRouter;
    private readonly SessionResumeOrchestrator _sessions;
    private readonly HookEventRouter _hookRouter;
    private readonly ILogger _logger;

    public DaemonRestoreSpawner(
        NookRegistry nooks,
        string bayDir,
        string bayId,
        AgentMessageRouter agentRouter,
        SessionResumeOrchestrator sessions,
        HookEventRouter hookRouter,
        ILogger logger)
    {
        _nooks = nooks;
        _bayDir = bayDir;
        _bayId = bayId;
        _agentRouter = agentRouter;
        _sessions = sessions;
        _hookRouter = hookRouter;
        _logger = logger;
    }

    public void Respawn(
        RestorableNook nook,
        string command,
        string[] args,
        string cwd)
    {
        try
        {
            var state = BayPersistence.LoadTerminalRestoreState(
                nook.NookId,
                _bayDir,
                _logger);
            if (state is not null)
            {
                _nooks.RespawnAs(
                    nook.NookId,
                    command,
                    args,
                    cwd,
                    nook.Cols,
                    nook.Rows,
                    state,
                    nook.Adapter,
                    nook.AgentName);
            }
            else
            {
                _nooks.RespawnAs(
                    nook.NookId,
                    command,
                    args,
                    cwd,
                    nook.Cols,
                    nook.Rows,
                    BayPersistence.LoadScrollback(nook.NookId, _bayDir),
                    nook.Adapter,
                    nook.AgentName);
            }
            if (!string.IsNullOrEmpty(nook.Title))
                _nooks.Rename(nook.NookId, nook.Title);
            if (!string.IsNullOrEmpty(nook.Adapter))
            {
                _agentRouter.Register(
                    nook.NookId,
                    nook.Adapter,
                    nook.AgentName,
                    _bayId);
                _sessions.Register(
                    nook.NookId,
                    nook.Adapter,
                    nook.SessionId);
                _hookRouter.Seed(
                    nook.NookId,
                    nook.Adapter,
                    nook.SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.NookRestorationRespawnFailed(
                nook.NookId,
                ex.Message);
        }
    }
}
