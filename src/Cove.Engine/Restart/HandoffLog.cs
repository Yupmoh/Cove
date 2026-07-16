using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Restart;

internal static partial class HandoffLog
{
    [ZLoggerMessage(3450, LogLevel.Information, "handoff: no live predecessor channel={channel}")]
    public static partial void HandoffNoPredecessor(this ILogger logger, string channel);

    [ZLoggerMessage(3451, LogLevel.Warning, "handoff begin rejected channel={channel} reason={reason}")]
    public static partial void HandoffBeginRejected(this ILogger logger, string channel, string reason);

    [ZLoggerMessage(3452, LogLevel.Warning, "handoff transfer truncated channel={channel} received={received} expected={expected}")]
    public static partial void HandoffTransferTruncated(this ILogger logger, string channel, int received, int expected);

    [ZLoggerMessage(3453, LogLevel.Warning, "handoff predecessor did not exit channel={channel}")]
    public static partial void HandoffPredecessorLingered(this ILogger logger, string channel);

    [ZLoggerMessage(3454, LogLevel.Information, "handoff received channel={channel} nooks={nooks}")]
    public static partial void HandoffReceived(this ILogger logger, string channel, int nooks);

    [ZLoggerMessage(3455, LogLevel.Warning, "handoff takeover failed channel={channel} error={error}")]
    public static partial void HandoffTakeoverFailed(this ILogger logger, string channel, string error);

    [ZLoggerMessage(3456, LogLevel.Information, "handoff adopted into successor nook={nookId} adapter={adapter}")]
    public static partial void HandoffSuccessorAdopted(this ILogger logger, string nookId, string adapter);

    [ZLoggerMessage(3457, LogLevel.Warning, "handoff adoption fell back to restoration nook={nookId}")]
    public static partial void HandoffAdoptionFellBack(this ILogger logger, string nookId);
}
