using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class NoteFileStore
{
    private readonly string _notesRoot;
    private readonly string _indexPath;
    private readonly ILogger _logger;

    public NoteFileStore(string dataDir, ILogger logger)
    {
        _notesRoot = System.IO.Path.Combine(dataDir, "notes");
        _indexPath = System.IO.Path.Combine(dataDir, "notes", "index.db");
        System.IO.Directory.CreateDirectory(_notesRoot);
        _logger = logger;
    }

    public Note Create(Note note)
    {
        if (string.IsNullOrEmpty(note.Id))
            note = note with { Id = System.Guid.NewGuid().ToString("N") };

        var noteDir = ResolveNoteDir(note.WorkspaceId, note.Id);
        System.IO.Directory.CreateDirectory(noteDir);

        var meta = new NoteMeta
        {
            Id = note.Id,
            Title = note.Title,
            WorkspaceId = note.WorkspaceId,
            Source = note.Source,
            Kind = note.Kind,
            CreatedAt = note.CreatedAt == default ? System.DateTimeOffset.UtcNow : note.CreatedAt,
            UpdatedAt = System.DateTimeOffset.UtcNow,
        };

        WriteMeta(noteDir, meta);
        WriteBody(noteDir, note.Kind, note.Content);
        UpsertFtsIndex(note);

        _logger.LogWarning("notes: created {id} ({kind}) in {ws}", note.Id, note.Kind, note.WorkspaceId);
        return note;
    }

    public Note? Get(string workspaceId, string id)
    {
        var noteDir = ResolveNoteDir(workspaceId, id);
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
            WorkspaceId = meta.WorkspaceId,
            Source = meta.Source,
            Kind = meta.Kind,
            CreatedAt = meta.CreatedAt,
            UpdatedAt = meta.UpdatedAt,
        };
    }

    public System.Collections.Generic.IReadOnlyList<NoteMeta> ListByWorkspace(string workspaceId)
    {
        var result = new System.Collections.Generic.List<NoteMeta>();
        var wsDir = System.IO.Path.Combine(_notesRoot, workspaceId);
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

    public void Update(string workspaceId, string id, System.Func<Note, Note> update)
    {
        var existing = Get(workspaceId, id);
        if (existing is null)
        {
            _logger.LogWarning("notes: update failed — note {id} not found in {ws}", id, workspaceId);
            return;
        }

        var updated = update(existing);
        var noteDir = ResolveNoteDir(workspaceId, id);
        var metaPath = System.IO.Path.Combine(noteDir, "meta.json");
        var meta = JsonSerializer.Deserialize(System.IO.File.ReadAllText(metaPath), NoteFileJsonContext.Default.NoteMeta)!;
        meta = meta with { Title = updated.Title, UpdatedAt = System.DateTimeOffset.UtcNow };
        WriteMeta(noteDir, meta);
        WriteBody(noteDir, updated.Kind, updated.Content);
        UpsertFtsIndex(updated);
        _logger.LogWarning("notes: updated {id} in {ws}", id, workspaceId);
    }

    public void Delete(string workspaceId, string id)
    {
        var noteDir = ResolveNoteDir(workspaceId, id);
        if (!System.IO.Directory.Exists(noteDir)) return;
        RemoveFromFtsIndex(id);
        System.IO.Directory.Delete(noteDir, true);
        _logger.LogWarning("notes: deleted {id} in {ws}", id, workspaceId);
    }

    public System.Collections.Generic.IReadOnlyList<Note> Search(string workspaceId, string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<Note>();
        using var conn = new SqliteConnection($"Data Source={_indexPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.note_id, n.workspace_id, n.title, n.body, n.type, n.updated_at
            FROM notes_fts f
            JOIN notes_index n ON n.rowid = f.rowid
            WHERE n.workspace_id = @ws AND notes_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Note
            {
                Id = reader.GetString(0),
                WorkspaceId = reader.GetString(1),
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
        using var conn = new SqliteConnection($"Data Source={_indexPath}");
        conn.Open();
        using var clearCmd = conn.CreateCommand();
        clearCmd.CommandText = "DELETE FROM notes_index;";
        clearCmd.ExecuteNonQuery();

        int count = 0;
        if (!System.IO.Directory.Exists(_notesRoot)) return;

        foreach (var wsDir in System.IO.Directory.GetDirectories(_notesRoot))
        {
            var workspaceId = System.IO.Path.GetFileName(wsDir);
            if (workspaceId == "index.db") continue;
            foreach (var noteDir in System.IO.Directory.GetDirectories(wsDir))
            {
                var metaPath = System.IO.Path.Combine(noteDir, "meta.json");
                if (!System.IO.File.Exists(metaPath)) continue;
                var meta = JsonSerializer.Deserialize(System.IO.File.ReadAllText(metaPath), NoteFileJsonContext.Default.NoteMeta);
                if (meta is null) continue;
                var body = ReadBody(noteDir, meta.Kind);
                UpsertFtsIndex(conn, meta.Id, meta.WorkspaceId, meta.Title, body, meta.Kind, meta.UpdatedAt.ToString("o"));
                count++;
            }
        }

        _logger.LogWarning("notes: rebuilt FTS index from disk ({count} notes)", count);
    }

    public void SaveViewport(string workspaceId, string id, string viewportJson)
    {
        var noteDir = ResolveNoteDir(workspaceId, id);
        if (!System.IO.Directory.Exists(noteDir))
        {
            _logger.LogWarning("notes: save viewport failed — note {id} not found", id);
            return;
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(noteDir, "viewport.json"), viewportJson);
    }

    public string? LoadViewport(string workspaceId, string id)
    {
        var path = System.IO.Path.Combine(ResolveNoteDir(workspaceId, id), "viewport.json");
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }

    public void SaveState(string workspaceId, string id, string stateJson)
    {
        var noteDir = ResolveNoteDir(workspaceId, id);
        if (!System.IO.Directory.Exists(noteDir)) return;
        System.IO.File.WriteAllText(System.IO.Path.Combine(noteDir, "state.json"), stateJson);
    }

    public string? LoadState(string workspaceId, string id)
    {
        var path = System.IO.Path.Combine(ResolveNoteDir(workspaceId, id), "state.json");
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }

    private string ResolveNoteDir(string workspaceId, string noteId)
        => System.IO.Path.Combine(_notesRoot, workspaceId, noteId);

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
        => UpsertFtsIndex(note.Id, note.WorkspaceId, note.Title, note.Content, note.Kind, System.DateTimeOffset.UtcNow.ToString("o"));

    private void UpsertFtsIndex(string id, string workspaceId, string title, string body, string type, string updatedAt)
    {
        using var conn = new SqliteConnection($"Data Source={_indexPath}");
        conn.Open();
        UpsertFtsIndex(conn, id, workspaceId, title, body, type, updatedAt);
    }

    private static void UpsertFtsIndex(SqliteConnection conn, string id, string workspaceId, string title, string body, string type, string updatedAt)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO notes_index (note_id, workspace_id, title, body, type, updated_at)
            VALUES (@id, @ws, @title, @body, @type, @ts)
            ON CONFLICT(note_id) DO UPDATE SET title=@title, body=@body, type=@type, updated_at=@ts;
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@title", (object?)title ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@body", (object?)body ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@ts", updatedAt);
        cmd.ExecuteNonQuery();
    }

    private void RemoveFromFtsIndex(string id)
    {
        using var conn = new SqliteConnection($"Data Source={_indexPath}");
        conn.Open();
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
    public required string WorkspaceId { get; init; }
    public required string Source { get; init; }
    public string Kind { get; init; } = "markdown";
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
}

[JsonSerializable(typeof(NoteMeta))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
public sealed partial class NoteFileJsonContext : JsonSerializerContext { }
