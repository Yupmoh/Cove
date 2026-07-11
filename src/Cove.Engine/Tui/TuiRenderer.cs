namespace Cove.Engine.Tui;

public sealed record TuiState
{
    public string? FocusedNookId { get; init; }
    public int NookCount { get; init; }
    public string? ActiveShore { get; init; }
}

public sealed class TuiRenderer
{
    public string Render(TuiState state)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("┌─ Cove ─────────────────────────────────┐");
        sb.AppendLine("│                                        │");

        if (state.NookCount == 0)
        {
            sb.AppendLine("│  No nooks — press Cmd+T to open one    │");
        }
        else
        {
            sb.AppendLine($"│  Nooks: {state.NookCount,-30}          │");
            if (state.FocusedNookId is { } id)
                sb.AppendLine($"│  Focus: {id,-30}          │");
            if (state.ActiveShore is { } shore)
                sb.AppendLine($"│  Shore:  {shore,-30}          │");
        }

        sb.AppendLine("│                                        │");
        sb.AppendLine("└────────────────────────────────────────┘");
        return sb.ToString();
    }
}

public static class TuiFormatter
{
    public static string FormatCommand(string uri, string? jsonParams)
    {
        if (string.IsNullOrEmpty(jsonParams))
            return $"{uri}";
        return $"{uri} {jsonParams}";
    }
}
