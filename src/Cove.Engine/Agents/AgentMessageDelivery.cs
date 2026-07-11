namespace Cove.Engine.Agents;

public interface INookWriter
{
    bool Write(string nookId, ReadOnlySpan<byte> data);
}

public sealed class AgentMessageDelivery
{
    public const int DefaultSubmitPauseMs = 250;
    private const string BracketedPasteStart = "\x1b[200~";
    private const string BracketedPasteEnd = "\x1b[201~";
    private readonly INookWriter _nooks;

    public AgentMessageDelivery(INookWriter nooks) => _nooks = nooks;

    public async Task<bool> DeliverAsync(string nookId, string body, int? submitPauseMs = null, CancellationToken cancellationToken = default)
    {
        var pause = submitPauseMs ?? DefaultSubmitPauseMs;
        var wrapped = BracketedPasteStart + body + BracketedPasteEnd;
        var wrappedBytes = System.Text.Encoding.UTF8.GetBytes(wrapped);
        if (!_nooks.Write(nookId, wrappedBytes))
            return false;

        if (pause > 0)
        {
            try { await Task.Delay(pause, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }

        return _nooks.Write(nookId, System.Text.Encoding.UTF8.GetBytes("\r"));
    }
}
