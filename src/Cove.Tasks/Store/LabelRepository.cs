using Cove.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Store;

public sealed class LabelRow
{
    public string Id { get; set; } = "";
    public string BayId { get; set; } = "";
    public string Name { get; set; } = "";
    public string HexColor { get; set; } = "6e6e6e";
    public double Position { get; set; }
    public string CreatedAt { get; set; } = "";
}

public sealed class LabelRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;

    private const string SelectColumns = "id AS Id, bay_id AS BayId, name AS Name, hex_color AS HexColor, position AS Position, created_at AS CreatedAt";

    public LabelRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public System.Threading.Tasks.Task<LabelRow?> CreateAsync(string bayId, string id, string name, string hexColor, double position)
    {
        if (_channel is null)
            return System.Threading.Tasks.Task.FromResult(CreateSync(bayId, id, name, hexColor, position));
        return _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(CreateInternal(conn, bayId, id, name, hexColor, position)));
    }

    private LabelRow? CreateSync(string bayId, string id, string name, string hexColor, double position)
    {
        using var conn = _factory.Open();
        return CreateInternal(conn, bayId, id, name, hexColor, position);
    }

    private static LabelRow? CreateInternal(SqliteConnection conn, string bayId, string id, string name, string hexColor, double position)
    {
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        try
        {
            conn.Execute(
                "INSERT INTO labels (id, bay_id, name, hex_color, position, created_at) VALUES (@Id, @BayId, @Name, @HexColor, @Position, @Now)",
                new { Id = id, BayId = bayId, Name = name, HexColor = hexColor, Position = position, Now = now });
        }
        catch (SqliteException)
        {
            return null;
        }
        return new LabelRow { Id = id, BayId = bayId, Name = name, HexColor = hexColor, Position = position, CreatedAt = now };
    }

    public LabelRow? GetByBayAndId(string bayId, string id)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<LabelRow>(
            $"SELECT {SelectColumns} FROM labels WHERE bay_id = @BayId AND id = @Id",
            new { BayId = bayId, Id = id });
    }

    public IReadOnlyList<LabelRow> ListByBay(string bayId)
    {
        using var conn = _factory.Open();
        return conn.Query<LabelRow>(
            $"SELECT {SelectColumns} FROM labels WHERE bay_id = @BayId ORDER BY position",
            new { BayId = bayId }).AsList();
    }

    public System.Threading.Tasks.Task DeleteAsync(string bayId, string id)
    {
        if (_channel is null)
        {
            DeleteSync(bayId, id);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteTransactionAsync<object?>((conn, transaction) =>
        {
            DeleteInternal(conn, bayId, id, transaction);
            return System.Threading.Tasks.Task.FromResult<object?>(null);
        });
    }

    private void DeleteSync(string bayId, string id)
    {
        using var conn = _factory.Open();
        DeleteInternal(conn, bayId, id);
    }

    private static void DeleteInternal(
        SqliteConnection conn,
        string bayId,
        string id,
        SqliteTransaction? transaction = null)
    {
        conn.Execute(
            "DELETE FROM card_labels WHERE label_id = @Id AND card_id IN (SELECT id FROM cards WHERE bay_id = @BayId)",
            new { Id = id, BayId = bayId },
            transaction);
        conn.Execute(
            "DELETE FROM labels WHERE bay_id = @BayId AND id = @Id",
            new { BayId = bayId, Id = id },
            transaction);
    }


    public System.Threading.Tasks.Task AssignToCardAsync(string cardId, string labelId)
    {
        if (_channel is null)
        {
            AssignSync(cardId, labelId);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { AssignInternal(conn, cardId, labelId); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void AssignSync(string cardId, string labelId)
    {
        using var conn = _factory.Open();
        AssignInternal(conn, cardId, labelId);
    }

    private static void AssignInternal(SqliteConnection conn, string cardId, string labelId)
    {
        conn.Execute(
            "INSERT OR IGNORE INTO card_labels (card_id, label_id) VALUES (@CardId, @LabelId)",
            new { CardId = cardId, LabelId = labelId });
    }

    public System.Threading.Tasks.Task UnassignFromCardAsync(string cardId, string labelId)
    {
        if (_channel is null)
        {
            UnassignSync(cardId, labelId);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { UnassignInternal(conn, cardId, labelId); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void UnassignSync(string cardId, string labelId)
    {
        using var conn = _factory.Open();
        UnassignInternal(conn, cardId, labelId);
    }

    private static void UnassignInternal(SqliteConnection conn, string cardId, string labelId)
    {
        conn.Execute("DELETE FROM card_labels WHERE card_id = @CardId AND label_id = @LabelId", new { CardId = cardId, LabelId = labelId });
    }

    public IReadOnlyList<LabelRow> GetLabelsForCard(string cardId)
    {
        using var conn = _factory.Open();
        return conn.Query<LabelRow>(
            $"SELECT l.id AS Id, l.bay_id AS BayId, l.name AS Name, l.hex_color AS HexColor, l.position AS Position, l.created_at AS CreatedAt FROM card_labels cl JOIN cards c ON c.id = cl.card_id JOIN labels l ON l.id = cl.label_id AND l.bay_id = c.bay_id WHERE cl.card_id = @CardId ORDER BY l.position",
            new { CardId = cardId }).AsList();
    }

    public IReadOnlyList<string> FilterCardsByLabel(string bayId, string labelId)
    {
        using var conn = _factory.Open();
        return conn.Query<string>(
            "SELECT cl.card_id FROM card_labels cl JOIN cards c ON c.id = cl.card_id JOIN labels l ON l.id = cl.label_id AND l.bay_id = c.bay_id WHERE c.bay_id = @BayId AND l.id = @LabelId",
            new { BayId = bayId, LabelId = labelId }).AsList();
    }

    public System.Threading.Tasks.Task ReorderAsync(string bayId, string[] orderedIds)
    {
        if (_channel is null)
        {
            ReorderSync(bayId, orderedIds);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { ReorderInternal(conn, bayId, orderedIds); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void ReorderSync(string bayId, string[] orderedIds)
    {
        using var conn = _factory.Open();
        ReorderInternal(conn, bayId, orderedIds);
    }

    private static void ReorderInternal(SqliteConnection conn, string bayId, string[] orderedIds)
    {
        for (int i = 0; i < orderedIds.Length; i++)
        {
            conn.Execute(
                "UPDATE labels SET position = @Position WHERE bay_id = @BayId AND id = @Id",
                new { Position = (double)i, BayId = bayId, Id = orderedIds[i] });
        }
    }
}
