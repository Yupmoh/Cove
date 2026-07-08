using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record ReviewScope(string Type, string? Id);

public sealed class ReviewScopeResolver
{
    private readonly ReviewStore _store;
    private readonly ILogger _logger;

    public ReviewScopeResolver(ReviewStore store, ILogger logger)
    {
        _store = store;
        _logger = logger;
    }

    public System.Collections.Generic.IReadOnlyList<ReviewComment> Resolve(string commitSha, ReviewScope? scope)
    {
        if (scope is null || scope.Type == "workspace")
            return _store.ListComments(commitSha);

        if (scope.Type == "session")
            return ResolveSessionScope(commitSha, scope.Id);

        return _store.ListComments(commitSha);
    }

    private System.Collections.Generic.IReadOnlyList<ReviewComment> ResolveSessionScope(string commitSha, string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogWarning("review-scope: session scope requires a session id");
            return _store.ListComments(commitSha);
        }

        var telemetry = _store.GetTelemetry(commitSha);
        var session = telemetry.FirstOrDefault(t => t.SessionId == sessionId);
        if (session is null)
        {
            _logger.LogWarning("review-scope: session {id} not found in commit {sha}", sessionId, commitSha);
            return [];
        }

        return _store.ListComments(commitSha);
    }
}
