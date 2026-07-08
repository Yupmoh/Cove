using System.Text.Json;

namespace Cove.Tasks.LaunchConfig;

public static class LaunchConfigSerializer
{
    public static string Serialize(LaunchConfigModel config)
        => JsonSerializer.Serialize(config, TaskJsonContext.Default.LaunchConfigModel);

    public static LaunchConfigModel? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize(json, TaskJsonContext.Default.LaunchConfigModel);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
