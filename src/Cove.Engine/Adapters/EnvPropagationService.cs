using Cove.Adapters;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Adapters;

public interface ISignalableNook
{
    string NookId { get; }
    bool Signal(int signum);
}

public interface IEnvPropagationTarget
{
    IReadOnlyList<ISignalableNook> GetNooksForBinary(string binary);
}

public sealed class EnvPropagationService : IDisposable
{
    private readonly AdapterEnvStore _store;
    private readonly IEnvPropagationTarget _target;
    private readonly Func<string, string?> _binaryResolver;
    private readonly ILogger? _logger;

    public EnvPropagationService(AdapterEnvStore store, IEnvPropagationTarget target, Func<string, string?> binaryResolver, ILogger? logger)
    {
        _store = store;
        _target = target;
        _binaryResolver = binaryResolver;
        _logger = logger;
        _store.EnvSaved += OnEnvSaved;
    }

    private void OnEnvSaved(string adapter)
    {
        var binary = _binaryResolver(adapter);
        if (string.IsNullOrEmpty(binary))
        {
            _logger?.EnvPropagationBinaryUnresolved(adapter);
            return;
        }

        IReadOnlyList<ISignalableNook> nooks;
        try
        {
            nooks = _target.GetNooksForBinary(binary);
        }
        catch (Exception ex)
        {
            _logger?.EnvPropagationLookupFailed(adapter, binary, ex.Message);
            return;
        }

        if (nooks.Count == 0)
            return;

        if (OperatingSystem.IsWindows())
        {
            _logger?.EnvPropagationSkippedWindows(adapter, binary);
            return;
        }

        var signum = PtyConstants.SigUsr1;
        foreach (var nook in nooks)
        {
            try
            {
                if (!nook.Signal(signum))
                    _logger?.EnvPropagationSignalFailed(adapter, binary, nook.NookId, "signal returned false");
            }
            catch (Exception ex)
            {
                _logger?.EnvPropagationSignalFailed(adapter, binary, nook.NookId, ex.Message);
            }
        }
    }

    public void Dispose()
    {
        _store.EnvSaved -= OnEnvSaved;
    }
}
