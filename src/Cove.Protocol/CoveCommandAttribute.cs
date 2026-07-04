namespace Cove.Protocol;

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
public sealed class CoveCommandAttribute : System.Attribute
{
    public CoveCommandAttribute(string key) => Key = key;

    public string Key { get; }
}
