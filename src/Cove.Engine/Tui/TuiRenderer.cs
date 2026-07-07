namespace Cove.Engine.Tui;

public sealed record TuiState
{
    public string? FocusedPaneId { get; init; }
    public int PaneCount { get; init; }
    public string? ActiveRoom { get; init; }
}

public sealed class TuiRenderer
{
    public string Render(TuiState state)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("┌─ Cove ─────────────────────────────────┐");
        sb.AppendLine("│                                        │");

        if (state.PaneCount == 0)
        {
            sb.AppendLine("│  No panes — press Cmd+T to open one    │");
        }
        else
        {
            sb.AppendLine($"│  Panes: {state.PaneCount,-30}          │");
            if (state.FocusedPaneId is { } id)
                sb.AppendLine($"│  Focus: {id,-30}          │");
            if (state.ActiveRoom is { } room)
                sb.AppendLine($"│  Room:  {room,-30}          │");
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
