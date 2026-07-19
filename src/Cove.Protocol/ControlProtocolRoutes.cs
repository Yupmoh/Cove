namespace Cove.Protocol;

public static class ControlProtocolRoutes
{
    public const string NookSubscribe =
        "cove://commands/nook.subscribe";

    public static bool IsStreamingRequest(string uri)
        => string.Equals(
            uri,
            NookSubscribe,
            StringComparison.Ordinal);
}
