using Microsoft.Extensions.Logging;

namespace Cove.Engine.Tui;

public sealed record ClientViewport(string ClientId, int Rows, int Cols, int PaneId);

public sealed class MultiClientViewportService
{
    private readonly Dictionary<string, ClientViewport> _viewports = new();
    private readonly Dictionary<int, HashSet<string>> _paneClients = new();
    private readonly ILogger _logger;
    private readonly object _lock = new();

    public MultiClientViewportService(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public void AttachClient(ClientViewport viewport)
    {
        if (string.IsNullOrWhiteSpace(viewport.ClientId))
        {
            _logger.LogWarning("viewport: client id required");
            throw new ArgumentException("client id required", nameof(viewport));
        }
        if (viewport.Rows <= 0 || viewport.Cols <= 0)
        {
            _logger.LogWarning("viewport: invalid dimensions {rows}x{cols} for client {id}", viewport.Rows, viewport.Cols, viewport.ClientId);
            throw new ArgumentException("invalid viewport dimensions");
        }

        lock (_lock)
        {
            if (_viewports.TryGetValue(viewport.ClientId, out var existing))
            {
                if (existing.PaneId != viewport.PaneId && _paneClients.TryGetValue(existing.PaneId, out var oldClients))
                {
                    oldClients.Remove(viewport.ClientId);
                    if (oldClients.Count == 0)
                        _paneClients.Remove(existing.PaneId);
                }
            }
            _viewports[viewport.ClientId] = viewport;
            if (!_paneClients.TryGetValue(viewport.PaneId, out var clients))
            {
                clients = new HashSet<string>();
                _paneClients[viewport.PaneId] = clients;
            }
            clients.Add(viewport.ClientId);
            _logger.LogInformation("viewport: client {id} attached to pane {pane} ({rows}x{cols})", viewport.ClientId, viewport.PaneId, viewport.Rows, viewport.Cols);
        }
    }

    public bool DetachClient(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("viewport: client id required for detach");
            return false;
        }

        lock (_lock)
        {
            if (!_viewports.TryGetValue(clientId, out var viewport))
                return false;

            _viewports.Remove(clientId);
            if (_paneClients.TryGetValue(viewport.PaneId, out var clients))
            {
                clients.Remove(clientId);
                if (clients.Count == 0)
                    _paneClients.Remove(viewport.PaneId);
            }
            _logger.LogInformation("viewport: client {id} detached from pane {pane}", clientId, viewport.PaneId);
            return true;
        }
    }

    public (int Rows, int Cols)? ReconcilePtySize(int paneId)
    {
        lock (_lock)
        {
            if (!_paneClients.TryGetValue(paneId, out var clients) || clients.Count == 0)
                return null;

            int minRows = int.MaxValue;
            int minCols = int.MaxValue;
            foreach (var clientId in clients)
            {
                if (_viewports.TryGetValue(clientId, out var vp))
                {
                    minRows = Math.Min(minRows, vp.Rows);
                    minCols = Math.Min(minCols, vp.Cols);
                }
            }

            if (minRows == int.MaxValue || minCols == int.MaxValue)
                return null;

            return (minRows, minCols);
        }
    }

    public IReadOnlyList<string> GetClientsForPane(int paneId)
    {
        lock (_lock)
        {
            if (!_paneClients.TryGetValue(paneId, out var clients))
                return Array.Empty<string>();
            return clients.ToList();
        }
    }

    public int GetClientCount(int paneId)
    {
        lock (_lock)
        {
            if (!_paneClients.TryGetValue(paneId, out var clients))
                return 0;
            return clients.Count;
        }
    }

    public bool UpdateViewportSize(string clientId, int rows, int cols)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("viewport: client id required for resize");
            return false;
        }
        if (rows <= 0 || cols <= 0)
        {
            _logger.LogWarning("viewport: invalid resize {rows}x{cols} for client {id}", rows, cols, clientId);
            return false;
        }

        lock (_lock)
        {
            if (!_viewports.TryGetValue(clientId, out var existing))
            {
                _logger.LogWarning("viewport: client {id} not attached, cannot resize", clientId);
                return false;
            }

            _viewports[clientId] = existing with { Rows = rows, Cols = cols };
            _logger.LogInformation("viewport: client {id} resized to {rows}x{cols}", clientId, rows, cols);
            return true;
        }
    }
}
