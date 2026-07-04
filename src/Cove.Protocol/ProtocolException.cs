namespace Cove.Protocol;

public sealed class ProtocolException : Exception
{
    public string Code { get; }

    public ProtocolException(string code, string message) : base(message) => Code = code;
}
