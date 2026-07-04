using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Protocol;

namespace Cove.Engine;

public sealed class EngineDispatchContext
{
    public EngineDispatchContext(ControlRequest request) => Request = request;

    public ControlRequest Request { get; }

    public ControlResponse Ok<T>(T data, JsonTypeInfo<T> typeInfo)
        => new ControlResponse(Request.Id, true, JsonSerializer.SerializeToElement(data, typeInfo));
}
