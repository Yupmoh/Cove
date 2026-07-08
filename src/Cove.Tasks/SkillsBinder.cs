using System.Text.Json;
using Cove.Tasks.Store;

namespace Cove.Tasks;

public static class SkillsBinder
{
    public static async System.Threading.Tasks.Task BindAsync(CardRepository cards, string cardId, string? agentRef, System.Collections.Generic.IReadOnlyList<SkillSelection> skills, string? profileSlug)
    {
        var card = cards.GetById(cardId);
        if (card is null) return;
        card.AgentRef = agentRef;
        card.ProfileSlug = profileSlug;
        card.SkillSelectionJson = SerializeSkills(skills);
        await cards.UpdateAsync(card);
    }

    public static TaskBinding GetBinding(CardRepository cards, string cardId)
    {
        var card = cards.GetById(cardId);
        if (card is null) return new TaskBinding(null, null, []);
        return new TaskBinding(card.AgentRef, card.ProfileSlug, ParseSkillSelection(card.SkillSelectionJson));
    }

    public static TaskProfilePayload ResolveTaskProfile(CardRow card)
    {
        return new TaskProfilePayload(card.AgentRef, card.ProfileSlug, ParseSkillSelection(card.SkillSelectionJson));
    }

    public static System.Collections.Generic.IReadOnlyList<SkillSelection> ParseSkillSelection(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new System.Collections.Generic.List<SkillSelection>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var provenance = TryGetString(el, "provenance") ?? "";
                var name = TryGetString(el, "name") ?? "";
                var mode = TryGetString(el, "mode") ?? "auto";
                result.Add(new SkillSelection(provenance, name, mode));
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string SerializeSkills(System.Collections.Generic.IReadOnlyList<SkillSelection> skills)
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var s in skills)
            {
                writer.WriteStartObject();
                writer.WriteString("provenance", s.Provenance);
                writer.WriteString("name", s.Name);
                writer.WriteString("mode", s.Mode);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string? TryGetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
}
