using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Persistence;
using Cove.Protocol;
using Cove.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Knowledge;

public sealed class NoteFileStore
{
    private readonly string _notesRoot;
    private readonly SqliteConnectionFactory _database;
    private readonly ILogger _logger;
    private readonly NoteSnapshotService? _snapshots;

    public NoteFileStore(
        string dataDir,
        ILogger logger,
        NoteSnapshotService? snapshots = null,
        SqliteConnectionFactory? database = null)
    {
        _notesRoot = System.IO.Path.Combine(dataDir, "notes");
        var indexPath = System.IO.Path.Combine(dataDir, "notes", "index.db");
        System.IO.Directory.CreateDirectory(_notesRoot);
        _logger = logger;
        _snapshots = snapshots;
        _database = database ?? new SqliteConnectionFactory(indexPath, logger);
    }

    public Note Create(Note note)
    {
        if (string.IsNullOrEmpty(note.Id))
            note = note with { Id = System.Guid.NewGuid().ToString("N") };

        if (!TryResolveNoteDir(note.BayId, note.Id, out var noteDir))
            throw new System.ArgumentException("bayId and noteId must be safe path segments");
        System.IO.Directory.CreateDirectory(noteDir);

        var meta = new NoteMeta
        {
            Id = note.Id,
            Title = note.Title,
            BayId = note.BayId,
            Source = note.Source,
            Kind = note.Kind,
            CreatedAt = note.CreatedAt == default ? System.DateTimeOffset.UtcNow : note.CreatedAt,
            UpdatedAt = System.DateTimeOffset.UtcNow,
        };

        WriteMeta(noteDir, meta);
        WriteBody(noteDir, note.Kind, note.Content);
        UpsertFtsIndex(note);

        _logger.LogWarning("notes: created {id} ({kind}) in {ws}", note.Id, note.Kind, note.BayId);
        _snapshots?.Snapshot(note.BayId, note.Id, $"create: {note.Title}");
        return note;
    }

    public Note? Get(string bayId, string id)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return null;
        var metaPath = System.IO.Path.Combine(noteDir, "meta.json");
        if (!System.IO.File.Exists(metaPath)) return null;

        var meta = JsonSerializer.Deserialize(System.IO.File.ReadAllText(metaPath), NoteFileJsonContext.Default.NoteMeta);
        if (meta is null) return null;

        var body = ReadBody(noteDir, meta.Kind);
        return new Note
        {
            Id = meta.Id,
            Title = meta.Title,
            Content = body,
            BayId = meta.BayId,
            Source = meta.Source,
            Kind = meta.Kind,
            CreatedAt = meta.CreatedAt,
            UpdatedAt = meta.UpdatedAt,
        };
    }

    public System.Collections.Generic.IReadOnlyList<NoteMeta> ListByBay(string bayId)
    {
        var result = new System.Collections.Generic.List<NoteMeta>();
        if (!TryResolveBayDir(bayId, out var wsDir))
            return result;
        if (!System.IO.Directory.Exists(wsDir)) return result;

        foreach (var noteDir in System.IO.Directory.GetDirectories(wsDir))
        {
            var metaPath = System.IO.Path.Combine(noteDir, "meta.json");
            if (!System.IO.File.Exists(metaPath)) continue;
            var meta = JsonSerializer.Deserialize(System.IO.File.ReadAllText(metaPath), NoteFileJsonContext.Default.NoteMeta);
            if (meta is not null)
                result.Add(meta);
        }
        return result.OrderByDescending(m => m.UpdatedAt).ToList();
    }

    public void Update(string bayId, string id, System.Func<Note, Note> update)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return;
        var existing = Get(bayId, id);
        if (existing is null)
        {
            _logger.LogWarning("notes: update failed — note {id} not found in {ws}", id, bayId);
            return;
        }

        var updated = update(existing);
        var metaPath = System.IO.Path.Combine(noteDir, "meta.json");
        var meta = JsonSerializer.Deserialize(System.IO.File.ReadAllText(metaPath), NoteFileJsonContext.Default.NoteMeta)!;
        meta = meta with { Title = updated.Title, UpdatedAt = System.DateTimeOffset.UtcNow };
        WriteMeta(noteDir, meta);
        WriteBody(noteDir, updated.Kind, updated.Content);
        UpsertFtsIndex(updated);
        _logger.LogWarning("notes: updated {id} in {ws}", id, bayId);
        _snapshots?.Snapshot(bayId, id, $"update: {updated.Title}");
    }

    public void Delete(string bayId, string id)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return;
        if (!System.IO.Directory.Exists(noteDir)) return;
        RemoveFromFtsIndex(id);
        System.IO.Directory.Delete(noteDir, true);
        _logger.LogWarning("notes: deleted {id} in {ws}", id, bayId);
    }

    public System.Collections.Generic.IReadOnlyList<NoteHistoryEntry> GetHistory(string bayId, string id)
    {
        if (!TryResolveNoteDir(bayId, id, out _))
            return [];
        if (_snapshots is null)
        {
            _logger.LogWarning("notes: snapshot service not available, cannot get history for {id}", id);
            return [];
        }
        return _snapshots.GetHistory(bayId, id);
    }

    public System.Collections.Generic.IReadOnlyList<Note> Search(string bayId, string query, int limit = 20)
    {
        RebuildIndexFromDisk();
        var result = new System.Collections.Generic.List<Note>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.note_id, n.bay_id, n.title, n.body, n.type, n.updated_at
            FROM notes_fts f
            JOIN notes_index n ON n.rowid = f.rowid
            WHERE n.bay_id = @ws AND notes_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Note
            {
                Id = reader.GetString(0),
                BayId = reader.GetString(1),
                Title = reader.GetString(2),
                Content = reader.GetString(3),
                Source = "notes-index",
                Kind = reader.GetString(4),
                UpdatedAt = System.DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }
        return result;
    }

    public void RebuildIndexFromDisk()
    {
        using var conn = _database.Open();
        using var transaction = conn.BeginTransaction();
        using var clearCmd = conn.CreateCommand();
        clearCmd.Transaction = transaction;
        clearCmd.CommandText = "DELETE FROM notes_index;";
        clearCmd.ExecuteNonQuery();

        int count = 0;
        if (System.IO.Directory.Exists(_notesRoot))
        {
            foreach (var wsDir in System.IO.Directory.GetDirectories(_notesRoot))
            {
                foreach (var noteDir in System.IO.Directory.GetDirectories(wsDir))
                {
                    try
                    {
                        var metaPath = System.IO.Path.Combine(noteDir, "meta.json");
                        if (!System.IO.File.Exists(metaPath)) continue;
                        var meta = JsonSerializer.Deserialize(System.IO.File.ReadAllText(metaPath), NoteFileJsonContext.Default.NoteMeta);
                        if (meta is null) continue;
                        var body = ReadBody(noteDir, meta.Kind);
                        UpsertFtsIndex(
                            conn,
                            transaction,
                            meta.Id,
                            meta.BayId,
                            meta.Title,
                            body,
                            meta.Kind,
                            meta.UpdatedAt.ToString("o"));
                        count++;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            "notes: skipped invalid note directory {path}: {error}",
                            noteDir,
                            exception.Message);
                    }
                }
            }
        }

        transaction.Commit();
        _logger.LogWarning("notes: rebuilt FTS index from disk ({count} notes)", count);
    }

    public void SaveViewport(string bayId, string id, string viewportJson)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return;
        if (!System.IO.Directory.Exists(noteDir))
        {
            _logger.LogWarning("notes: save viewport failed — note {id} not found", id);
            return;
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(noteDir, "viewport.json"), viewportJson);
    }

    public string? LoadViewport(string bayId, string id)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return null;
        var path = System.IO.Path.Combine(noteDir, "viewport.json");
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }

    public void SaveState(string bayId, string id, string stateJson)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return;
        if (!System.IO.Directory.Exists(noteDir))
        {
            _logger.NoteSaveStateMissing(id);
            return;
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(noteDir, "state.json"), stateJson);
    }

    public string? LoadState(string bayId, string id)
    {
        if (!TryResolveNoteDir(bayId, id, out var noteDir))
            return null;
        var path = System.IO.Path.Combine(noteDir, "state.json");
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }

    public string SaveMedia(string bayId, string id, string fileName, byte[] data)
    {
        if (!TryResolveNoteDir(bayId, id, out _))
            throw new System.ArgumentException("bayId and noteId must be safe path segments");
        if (!PathContainment.IsSafeSegment(fileName))
        {
            _logger.NoteMediaUnsafeFileName(fileName, id, bayId);
            throw new System.ArgumentException("media fileName must be a safe path segment", nameof(fileName));
        }

        var mediaId = $"img-{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{fileName}";
        if (!PathContainment.TryResolveContained(_notesRoot, out _, out var mediaPath, bayId, id, "media", mediaId)
            || !PathContainment.IsContainedPhysical(_notesRoot, mediaPath))
        {
            _logger.NoteMediaOutsideRoot(fileName, id, bayId);
            throw new System.ArgumentException("media path must stay within the notes root", nameof(fileName));
        }

        var mediaDir = System.IO.Path.GetDirectoryName(mediaPath)!;
        System.IO.Directory.CreateDirectory(mediaDir);
        System.IO.File.WriteAllBytes(mediaPath, data);
        _logger.LogWarning("notes: saved media {mediaId} for note {id} in {ws}", mediaId, id, bayId);
        return System.IO.Path.Combine(bayId, id, "media", mediaId);
    }

    private bool TryResolveBayDir(string bayId, out string bayDir)
    {
        bayDir = string.Empty;
        if (!PathContainment.IsSafeSegment(bayId)
            || !PathContainment.TryResolveContained(_notesRoot, out _, out bayDir, bayId)
            || !PathContainment.IsContainedPhysical(_notesRoot, bayDir))
        {
            _logger.NoteUnsafeBayIdentifier(bayId);
            return false;
        }
        return true;
    }

    private bool TryResolveNoteDir(string bayId, string noteId, out string noteDir)
    {
        noteDir = string.Empty;
        if (!PathContainment.IsSafeSegment(bayId)
            || !PathContainment.IsSafeSegment(noteId)
            || !PathContainment.TryResolveContained(_notesRoot, out _, out noteDir, bayId, noteId)
            || !PathContainment.IsContainedPhysical(_notesRoot, noteDir))
        {
            _logger.NoteUnsafeIdentifiers(noteId, bayId);
            return false;
        }
        return true;
    }

    private static void WriteMeta(string noteDir, NoteMeta meta)
        => System.IO.File.WriteAllText(System.IO.Path.Combine(noteDir, "meta.json"), JsonSerializer.Serialize(meta, NoteFileJsonContext.Default.NoteMeta));

    private static void WriteBody(string noteDir, string kind, string content)
    {
        var ext = kind switch
        {
            "markdown" => ".md",
            "sketch" => ".excalidraw",
            "canvas" => ".canvas.json",
            "html" => ".html",
            "mermaid" => ".mmd",
            _ => ".md",
        };
        System.IO.File.WriteAllText(System.IO.Path.Combine(noteDir, "note" + ext), content);
    }

    private static string ReadBody(string noteDir, string kind)
    {
        var ext = kind switch
        {
            "markdown" => ".md",
            "sketch" => ".excalidraw",
            "canvas" => ".canvas.json",
            "html" => ".html",
            "mermaid" => ".mmd",
            _ => ".md",
        };
        var path = System.IO.Path.Combine(noteDir, "note" + ext);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
    }

    private void UpsertFtsIndex(Note note)
        => UpsertFtsIndex(note.Id, note.BayId, note.Title, note.Content, note.Kind, System.DateTimeOffset.UtcNow.ToString("o"));

    private void UpsertFtsIndex(string id, string bayId, string title, string body, string type, string updatedAt)
    {
        using var conn = _database.Open();

        UpsertFtsIndex(conn, id, bayId, title, body, type, updatedAt);
    }

    private static void UpsertFtsIndex(SqliteConnection conn, string id, string bayId, string title, string body, string type, string updatedAt)
        => UpsertFtsIndex(conn, transaction: null, id, bayId, title, body, type, updatedAt);

    private static void UpsertFtsIndex(
        SqliteConnection conn,
        SqliteTransaction? transaction,
        string id,
        string bayId,
        string title,
        string body,
        string type,
        string updatedAt)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO notes_index (note_id, bay_id, title, body, type, updated_at)
            VALUES (@id, @ws, @title, @body, @type, @ts)
            ON CONFLICT(note_id) DO UPDATE SET
                bay_id=@ws,
                title=@title,
                body=@body,
                type=@type,
                updated_at=@ts;
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@title", (object?)title ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@body", (object?)body ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@ts", updatedAt);
        cmd.ExecuteNonQuery();
    }

    private void RemoveFromFtsIndex(string id)
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes_index WHERE note_id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}

public sealed record NoteMeta
{
    public string Id { get; init; } = "";
    public required string Title { get; init; }
    public required string BayId { get; init; }
    public required string Source { get; init; }
    private readonly string? _kind;
    public string Kind { get => _kind ?? "markdown"; init => _kind = value; }
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
}

[JsonSerializable(typeof(NoteMeta))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
public sealed partial class NoteFileJsonContext : JsonSerializerContext { }

internal static partial class NoteFileStoreLog
{
    [ZLoggerMessage(LogLevel.Warning, "notes rejected unsafe bay identifier bayId={bayId}")]
    public static partial void NoteUnsafeBayIdentifier(this ILogger logger, string bayId);

    [ZLoggerMessage(LogLevel.Warning, "notes rejected unsafe identifiers noteId={noteId} bayId={bayId}")]
    public static partial void NoteUnsafeIdentifiers(this ILogger logger, string noteId, string bayId);

    [ZLoggerMessage(LogLevel.Warning, "notes media save rejected unsafe file name fileName={fileName} noteId={noteId} bayId={bayId}")]
    public static partial void NoteMediaUnsafeFileName(this ILogger logger, string fileName, string noteId, string bayId);

    [ZLoggerMessage(LogLevel.Warning, "notes media save rejected path outside root fileName={fileName} noteId={noteId} bayId={bayId}")]
    public static partial void NoteMediaOutsideRoot(this ILogger logger, string fileName, string noteId, string bayId);

    [ZLoggerMessage(LogLevel.Warning, "notes save state failed note not found noteId={noteId}")]
    public static partial void NoteSaveStateMissing(this ILogger logger, string noteId);
}
