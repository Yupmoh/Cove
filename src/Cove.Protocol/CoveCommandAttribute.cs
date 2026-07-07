namespace Cove.Protocol;

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
public sealed class CoveCommandAttribute : System.Attribute
{
    public CoveCommandAttribute(string key) => Key = key;

    public string Key { get; }
    public string? Description { get; init; }
    public string? Source { get; init; }
}
