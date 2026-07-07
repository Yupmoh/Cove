using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Engine.Tasks;

using Cove.Protocol;

public sealed class TaskStore
{
    private readonly string _dir;
    private readonly string _cardsDir;
    private readonly string _countersDir;
    private readonly object _lock = new();

    public TaskStore(string dataDir)
    {
        _dir = dataDir;
        _cardsDir = Path.Combine(dataDir, "task-cards");
        _countersDir = Path.Combine(dataDir, "task-counters");
        Directory.CreateDirectory(_cardsDir);
        Directory.CreateDirectory(_countersDir);
    }

    public TaskCard Create(TaskCard card)
    {
        lock (_lock)
        {
            var number = IncrementCounter(card.WorkspaceId);
            var created = card with
            {
                Id = System.Guid.NewGuid().ToString("N"),
                TaskNumber = number,
                CreatedAt = System.DateTimeOffset.UtcNow,
                UpdatedAt = System.DateTimeOffset.UtcNow,
            };
            WriteCard(created);
            return created;
        }
    }

    public TaskCard? Get(string id)
    {
        var path = Path.Combine(_cardsDir, id + ".json");
        if (!File.Exists(path))
            return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, TasksJsonContext.Default.TaskCard);
    }

    public TaskCard? ResolveByHumanId(string humanId)
    {
        var prefix = "COVE-";
        if (!humanId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return null;
        if (!int.TryParse(humanId[prefix.Length..], out var number))
            return null;
        foreach (var file in Directory.EnumerateFiles(_cardsDir, "*.json"))
        {
            var card = JsonSerializer.Deserialize(File.ReadAllText(file), TasksJsonContext.Default.TaskCard);
            if (card is { } c && c.TaskNumber == number)
                return c;
        }
        return null;
    }

    public IReadOnlyList<TaskCard> ListByWorkspace(string workspaceId)
    {
        var result = new List<TaskCard>();
        foreach (var file in Directory.EnumerateFiles(_cardsDir, "*.json"))
        {
            var card = JsonSerializer.Deserialize(File.ReadAllText(file), TasksJsonContext.Default.TaskCard);
            if (card is { } c && c.WorkspaceId == workspaceId)
                result.Add(c);
        }
        return result;
    }

    public void Update(string id, System.Func<TaskCard, TaskCard> update)
    {
        var existing = Get(id);
        if (existing is null)
            return;
        var updated = update(existing) with { UpdatedAt = System.DateTimeOffset.UtcNow };
        WriteCard(updated);
    }

    public void Delete(string id)
    {
        var path = Path.Combine(_cardsDir, id + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }

    private int IncrementCounter(string workspaceId)
    {
        var counterPath = Path.Combine(_countersDir, workspaceId + ".json");
        var current = 0;
        if (File.Exists(counterPath))
        {
            if (int.TryParse(File.ReadAllText(counterPath), out var val))
                current = val;
        }
        current++;
        File.WriteAllText(counterPath, current.ToString());
        return current;
    }

    private void WriteCard(TaskCard card)
    {
        var path = Path.Combine(_cardsDir, card.Id + ".json");
        var json = JsonSerializer.Serialize(card, TasksJsonContext.Default.TaskCard);
        File.WriteAllText(path, json);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TaskCard))]
[JsonSerializable(typeof(List<TaskCard>))]
public sealed partial class TasksJsonContext : JsonSerializerContext { }
