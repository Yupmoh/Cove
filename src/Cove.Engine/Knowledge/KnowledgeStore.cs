using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Knowledge;

public sealed class NoteStore
{
    private readonly string _dir;

    public NoteStore(string dataDir)
    {
        _dir = System.IO.Path.Combine(dataDir, "notes");
        System.IO.Directory.CreateDirectory(_dir);
    }

    public Note Create(Note note)
    {
        var created = note with
        {
            Id = System.Guid.NewGuid().ToString("N"),
            CreatedAt = System.DateTimeOffset.UtcNow,
            UpdatedAt = System.DateTimeOffset.UtcNow,
        };
        Write(created);
        return created;
    }

    public Note? Get(string id)
    {
        var path = System.IO.Path.Combine(_dir, id + ".json");
        if (!System.IO.File.Exists(path))
            return null;
        return JsonSerializer.Deserialize(System.IO.File.ReadAllText(path), KnowledgeJsonContext.Default.Note);
    }

    public System.Collections.Generic.IReadOnlyList<Note> ListByWorkspace(string workspaceId)
    {
        var result = new System.Collections.Generic.List<Note>();
        foreach (var file in System.IO.Directory.EnumerateFiles(_dir, "*.json"))
        {
            var note = JsonSerializer.Deserialize(System.IO.File.ReadAllText(file), KnowledgeJsonContext.Default.Note);
            if (note is { } n && n.WorkspaceId == workspaceId)
                result.Add(n);
        }
        return result;
    }

    public void Update(string id, System.Func<Note, Note> update)
    {
        var existing = Get(id);
        if (existing is null)
            return;
        Write(update(existing) with { UpdatedAt = System.DateTimeOffset.UtcNow });
    }

    public void Delete(string id)
    {
        var path = System.IO.Path.Combine(_dir, id + ".json");
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private void Write(Note note)
    {
        var path = System.IO.Path.Combine(_dir, note.Id + ".json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(note, KnowledgeJsonContext.Default.Note));
    }
}

public sealed class TimelineStore
{
    private readonly string _dir;

    public TimelineStore(string dataDir)
    {
        _dir = System.IO.Path.Combine(dataDir, "timeline");
        System.IO.Directory.CreateDirectory(_dir);
    }

    public TimelineEntry Append(TimelineEntry entry)
    {
        var created = entry with
        {
            Id = System.Guid.NewGuid().ToString("N"),
            Timestamp = System.DateTimeOffset.UtcNow,
        };
        var path = System.IO.Path.Combine(_dir, created.Id + ".json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(created, KnowledgeJsonContext.Default.TimelineEntry));
        return created;
    }

    public System.Collections.Generic.IReadOnlyList<TimelineEntry> ListByWorkspace(string workspaceId)
    {
        var result = new System.Collections.Generic.List<TimelineEntry>();
        foreach (var file in System.IO.Directory.EnumerateFiles(_dir, "*.json"))
        {
            var entry = JsonSerializer.Deserialize(System.IO.File.ReadAllText(file), KnowledgeJsonContext.Default.TimelineEntry);
            if (entry is { } e && e.WorkspaceId == workspaceId)
                result.Add(e);
        }
        return result.OrderByDescending(e => e.Timestamp).ToList();
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(TimelineEntry))]
[JsonSerializable(typeof(System.Collections.Generic.List<Note>))]
[JsonSerializable(typeof(System.Collections.Generic.List<TimelineEntry>))]
public sealed partial class KnowledgeJsonContext : JsonSerializerContext { }
