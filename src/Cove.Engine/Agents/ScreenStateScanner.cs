using Cove.Adapters;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Agents;

public sealed class ScreenStateScanner : IDisposable
{
    private sealed class NookMemo
    {
        public long LastHead = -1;
        public long LastAdvanceTicks;
    }

    private readonly NookRegistry _nooks;
    private readonly HookEventRouter _router;
    private readonly Func<string, AdapterManifest?> _manifests;
    private readonly ILogger _logger;
    private readonly Dictionary<string, NookMemo> _memos = new();
    private Timer? _timer;
    private int _ticking;

    public ScreenStateScanner(NookRegistry nooks, HookEventRouter router, Func<string, AdapterManifest?> manifests, ILogger logger)
    {
        _nooks = nooks;
        _router = router;
        _manifests = manifests;
        _logger = logger;
    }

    public void Start()
    {
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    internal void Tick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) != 0)
            return;
        try
        {
            var seen = new HashSet<string>();
            foreach (var (nookId, adapter) in _nooks.ListAdapterNooks())
            {
                seen.Add(nookId);
                var manifest = _manifests(adapter);
                var declaration = manifest?.ScreenState;
                if (declaration is null || manifest!.Hooks.Count > 0)
                    continue;
                var priorHead = _memos.TryGetValue(nookId, out var memo) ? memo.LastHead : -1L;
                var since = priorHead < 0 ? long.MaxValue : priorHead;
                if (_nooks.TryGetScreenSample(nookId, since, declaration.EffectiveTailBytes) is not { } sample)
                {
                    _memos.Remove(nookId);
                    continue;
                }
                var now = Environment.TickCount64;
                if (memo is null)
                {
                    _memos[nookId] = new NookMemo { LastHead = sample.Head, LastAdvanceTicks = now };
                    continue;
                }
                var advanced = sample.Head != memo.LastHead;
                if (advanced)
                {
                    memo.LastHead = sample.Head;
                    memo.LastAdvanceTicks = now;
                }
                var quiet = !advanced && now - memo.LastAdvanceTicks >= declaration.EffectiveQuietMs;
                var current = _router.GetNookState(nookId)?.Status ?? "idle";
                var text = advanced ? ScreenStateDetector.AnsiStrip(sample.Delta) : "";
                if (ScreenStateDetector.Evaluate(declaration, text, advanced, quiet, current) is { } next)
                    _router.ScreenTransition(nookId, adapter, next);
            }
            var stale = _memos.Keys.Where(id => !seen.Contains(id)).ToList();
            foreach (var id in stale)
                _memos.Remove(id);
        }
        catch (Exception ex)
        {
            _logger.ScreenScanFailed(ex.Message);
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
