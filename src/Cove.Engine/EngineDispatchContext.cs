using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine.Pty;
using Cove.Protocol;

namespace Cove.Engine;

public sealed class EngineDispatchContext
{
    public EngineDispatchContext(ControlRequest request, PaneRegistry? panes = null)
    {
        Request = request;
        Panes = panes;
    }

    public ControlRequest Request { get; }
    public PaneRegistry? Panes { get; }

    public ControlResponse Ok<T>(T data, JsonTypeInfo<T> typeInfo)
        => new ControlResponse(Request.Id, true, JsonSerializer.SerializeToElement(data, typeInfo));

    public ControlResponse Ok()
        => new ControlResponse(Request.Id, true, null);

    public ControlResponse Fail(string code, string message)
        => new ControlResponse(Request.Id, false, null, new ControlError(code, message));
}
