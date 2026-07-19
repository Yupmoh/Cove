using System.Text.Json;
using Cove.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Store;

public sealed class CommentRow
{
    public string Id { get; set; } = "";
    public string CardId { get; set; } = "";
    public string Kind { get; set; } = "discussion";
    public string Body { get; set; } = "";
    public string Source { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class CommentRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;
    private readonly CardRepository? _cards;

    private static readonly HashSet<string> AllowedKinds = new(System.StringComparer.Ordinal) { "discussion", "instruction", "agent_update", "system_event" };
    private static readonly HashSet<string> CliAllowedKinds = new(System.StringComparer.Ordinal) { "discussion", "instruction", "agent_update" };

    private const string SelectColumns = "id AS Id, card_id AS CardId, kind AS Kind, body AS Body, source AS Source, created_at AS CreatedAt";

    public CommentRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null, CardRepository? cards = null)
    {
        _factory = factory;
        _channel = channel;
        _cards = cards;
    }

    public System.Threading.Tasks.Task<CommentRow?> AddAsync(string cardId, string kind, string body, string source)
    {
        if (!CliAllowedKinds.Contains(kind))
            throw new System.ArgumentException($"comment kind '{kind}' is not allowed via this API (reserved: system_event)", nameof(kind));
        var row = new CommentRow
        {
            Id = System.Guid.NewGuid().ToString("N"),
            CardId = cardId,
            Kind = kind,
            Body = body,
            Source = source,
            CreatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
        };
        if (_channel is null)
        {
            AddSync(row);
            return System.Threading.Tasks.Task.FromResult<CommentRow?>(row);
        }
        return _channel.ExecuteTransactionAsync<CommentRow?>((conn, transaction) =>
        {
            AddInternal(conn, row, transaction);
            SyncCardCommentIds(conn, cardId, transaction);
            return System.Threading.Tasks.Task.FromResult<CommentRow?>(row);
        });
    }

    private void AddSync(CommentRow row)
    {
        using var conn = _factory.Open();
        AddInternal(conn, row);
        SyncCardCommentIds(conn, row.CardId);
    }

    private static void AddInternal(
        SqliteConnection conn,
        CommentRow row,
        SqliteTransaction? transaction = null)
    {
        conn.Execute(
            "INSERT INTO comments (id, card_id, kind, body, source, created_at) VALUES (@Id, @CardId, @Kind, @Body, @Source, @CreatedAt)",
            row,
            transaction);
    }

    private static void SyncCardCommentIds(
        SqliteConnection conn,
        string cardId,
        SqliteTransaction? transaction = null)
    {
        var ids = conn.Query<string>(
            "SELECT id FROM comments WHERE card_id = @CardId ORDER BY created_at, rowid",
            new { CardId = cardId },
            transaction).AsList();
        var json = BuildJsonArray(ids);
        conn.Execute(
            "UPDATE cards SET comment_ids_json = @Json, updated_at = @Now WHERE id = @CardId",
            new { Json = json, Now = System.DateTimeOffset.UtcNow.ToString("o"), CardId = cardId },
            transaction);
    }

    private static string BuildJsonArray(System.Collections.Generic.IReadOnlyList<string> ids)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var id in ids)
                writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.Flush();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    public IReadOnlyList<CommentRow> ListByCard(string cardId)
    {
        using var conn = _factory.Open();
        return conn.Query<CommentRow>(
            $"SELECT {SelectColumns} FROM comments WHERE card_id = @CardId ORDER BY created_at, rowid",
            new { CardId = cardId }).AsList();
    }

    public int CountByCard(string cardId)
    {
        using var conn = _factory.Open();
        return conn.ExecuteScalar<int>("SELECT count(*) FROM comments WHERE card_id = @CardId", new { CardId = cardId });
    }

    public System.Threading.Tasks.Task DeleteAsync(string id)
    {
        if (_channel is null)
        {
            DeleteSync(id);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteTransactionAsync<object?>((conn, transaction) =>
        {
            var cardId = DeleteInternal(conn, id, transaction);
            if (cardId is not null)
                SyncCardCommentIds(conn, cardId, transaction);
            return System.Threading.Tasks.Task.FromResult<object?>(null);
        });
    }

    private void DeleteSync(string id)
    {
        using var conn = _factory.Open();
        var cardId = DeleteInternal(conn, id);
        if (cardId is not null)
            SyncCardCommentIds(conn, cardId);
    }

    private static string? DeleteInternal(
        SqliteConnection conn,
        string id,
        SqliteTransaction? transaction = null)
    {
        var cardId = conn.QueryFirstOrDefault<string>(
            "SELECT card_id FROM comments WHERE id = @Id",
            new { Id = id },
            transaction);
        conn.Execute(
            "DELETE FROM comments WHERE id = @Id",
            new { Id = id },
            transaction);
        return cardId;
    }
}
