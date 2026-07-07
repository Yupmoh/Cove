namespace Cove.Engine.Agents;

public interface IPaneWriter
{
    bool Write(string paneId, ReadOnlySpan<byte> data);
}

public sealed class AgentMessageDelivery
{
    public const int DefaultSubmitPauseMs = 250;
    private const string BracketedPasteStart = "\x1b[200~";
    private const string BracketedPasteEnd = "\x1b[201~";
    private readonly IPaneWriter _panes;

    public AgentMessageDelivery(IPaneWriter panes) => _panes = panes;

    public async Task<bool> DeliverAsync(string paneId, string body, int? submitPauseMs = null, CancellationToken cancellationToken = default)
    {
        var pause = submitPauseMs ?? DefaultSubmitPauseMs;
        var wrapped = BracketedPasteStart + body + BracketedPasteEnd;
        var wrappedBytes = System.Text.Encoding.UTF8.GetBytes(wrapped);
        if (!_panes.Write(paneId, wrappedBytes))
            return false;

        if (pause > 0)
        {
            try { await Task.Delay(pause, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }

        return _panes.Write(paneId, System.Text.Encoding.UTF8.GetBytes("\r"));
    }
}
