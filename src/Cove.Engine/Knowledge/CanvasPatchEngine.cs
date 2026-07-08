using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class CanvasPatchResult(bool Applied, string? Error, JsonNode? Result)
{
    public bool Applied { get; } = Applied;
    public string? Error { get; } = Error;
    public JsonNode? Result { get; } = Result;
}

public sealed class CanvasPatchEngine
{
    private readonly ILogger _logger;
    private readonly System.Threading.Channels.Channel<JsonNode> _patchChannel;
    private readonly System.Threading.Timer _flushTimer;
    private readonly System.TimeSpan _flushDelay;
    private JsonNode? _current;
    private readonly object _stateLock = new();
    private readonly System.Collections.Generic.List<JsonNode> _pendingPatches = new();
    private System.Threading.Tasks.Task? _flushTask;
    private bool _disposed;

    public event System.EventHandler<JsonNode>? StateChanged;

    public CanvasPatchEngine(ILogger logger, System.TimeSpan? flushDelay = null)
    {
        _logger = logger;
        _flushDelay = flushDelay ?? System.TimeSpan.FromMilliseconds(50);
        _patchChannel = System.Threading.Channels.Channel.CreateUnbounded<JsonNode>();
        _flushTimer = new System.Threading.Timer(TriggerFlush, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
    }

    public void Initialize(JsonNode initialState)
    {
        lock (_stateLock)
            _current = initialState.DeepClone();
    }

    public CanvasPatchResult ApplyPatch(JsonNode patchNode)
    {
        if (patchNode is not JsonArray ops)
            return new CanvasPatchResult(false, "patch must be a JSON array of operations", null);

        lock (_stateLock)
        {
            if (_current is null)
                _current = new JsonObject();

            foreach (var opNode in ops)
            {
                if (opNode is not JsonObject op)
                    continue;

                var path = op["path"]?.GetValue<string>() ?? "";
                var opType = op["op"]?.GetValue<string>() ?? "";

                if (string.IsNullOrEmpty(opType))
                    continue;

                try
                {
                    switch (opType)
                    {
                        case "add":
                            _current = ApplyAdd(_current, path, op["value"]);
                            break;
                        case "replace":
                            ApplyReplace(_current, path, op["value"]);
                            break;
                        case "remove":
                            ApplyRemove(_current, path);
                            break;
                        case "test":
                            if (!ApplyTest(_current, path, op["value"]))
                            {
                                _logger.LogWarning("canvas-patch: test-mismatch at {path}, skipping remaining ops", path);
                                return new CanvasPatchResult(false, $"test-mismatch at {path}", _current);
                            }
                            break;
                        case "move":
                            ApplyMove(_current, path, op["from"]?.GetValue<string>() ?? "");
                            break;
                        case "copy":
                            ApplyCopy(_current, path, op["from"]?.GetValue<string>() ?? "");
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogWarning("canvas-patch: op {op} at {path} failed: {err}", opType, path, ex.Message);
                    return new CanvasPatchResult(false, $"{opType} at {path}: {ex.Message}", _current);
                }
            }

            return new CanvasPatchResult(true, null, _current);
        }
    }

    public void QueuePatch(JsonNode patch)
    {
        _patchChannel.Writer.TryWrite(patch);
        _flushTimer.Change(_flushDelay, System.Threading.Timeout.InfiniteTimeSpan);
    }

    public async System.Threading.Tasks.Task FlushAsync()
    {
        _flushTimer.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);

        var toApply = new System.Collections.Generic.List<JsonNode>();
        while (_patchChannel.Reader.TryRead(out var patch))
            toApply.Add(patch);

        if (toApply.Count == 0) return;

        int appliedCount = 0;
        foreach (var patch in toApply)
        {
            var result = ApplyPatch(patch);
            if (result.Applied)
                appliedCount++;
        }

        lock (_stateLock)
        {
            if (_current is not null)
                StateChanged?.Invoke(this, _current.DeepClone());
        }

        _logger.LogWarning("canvas-patch: flushed {applied}/{total} patches", appliedCount, toApply.Count);
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public JsonNode? GetState()
    {
        lock (_stateLock)
            return _current?.DeepClone();
    }

    private void TriggerFlush(object? state)
    {
        _flushTask = FlushAsync();
    }

    private static JsonNode ApplyAdd(JsonNode root, string path, JsonNode? value)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return value?.DeepClone() ?? root;
        }

        var parts = path.Trim('/').Split('/');
        var lastPart = UnescapeToken(parts[^1]);
        JsonNode current = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = UnescapeToken(parts[i]);
            if (current is JsonObject obj)
            {
                if (!obj.ContainsKey(part))
                {
                    var nextPart = i + 1 < parts.Length ? UnescapeToken(parts[i + 1]) : lastPart;
                    obj[part] = int.TryParse(nextPart, out _) ? new JsonArray() : new JsonObject();
                }
                current = obj[part]!;
            }
            else if (current is JsonArray arr && int.TryParse(part, out int idx))
            {
                while (arr.Count <= idx)
                    arr.Add(null);
                current = arr[idx]!;
            }
            else
            {
                return root;
            }
        }

        if (current is JsonObject targetObj)
        {
            targetObj[lastPart] = value?.DeepClone();
        }
        else if (current is JsonArray targetArr)
        {
            if (int.TryParse(lastPart, out int idx))
            {
                while (targetArr.Count <= idx)
                    targetArr.Add(null);
                targetArr[idx] = value?.DeepClone();
            }
            else
            {
                targetArr.Add(value?.DeepClone());
            }
        }
        return root;
    }

    private static void ApplyReplace(JsonNode root, string path, JsonNode? value)
    {
        var parts = path.Trim('/').Split('/');
        JsonNode current = root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = UnescapeToken(parts[i]);
            if (current is JsonObject obj && obj.ContainsKey(part))
                current = obj[part]!;
            else if (current is JsonArray arr && int.TryParse(part, out int idx) && idx < arr.Count)
                current = arr[idx]!;
            else
                return;
        }

        var lastPart = UnescapeToken(parts[^1]);
        if (current is JsonObject targetObj)
            targetObj[lastPart] = value?.DeepClone();
        else if (current is JsonArray targetArr && int.TryParse(lastPart, out int targetIdx) && targetIdx < targetArr.Count)
            targetArr[targetIdx] = value?.DeepClone();
    }

    private static void ApplyRemove(JsonNode root, string path)
    {
        var parts = path.Trim('/').Split('/');
        JsonNode current = root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = UnescapeToken(parts[i]);
            if (current is JsonObject obj && obj.ContainsKey(part))
                current = obj[part]!;
            else if (current is JsonArray arr && int.TryParse(part, out int idx) && idx < arr.Count)
                current = arr[idx]!;
            else
                return;
        }

        var lastPart = UnescapeToken(parts[^1]);
        if (current is JsonObject targetObj)
            targetObj.Remove(lastPart);
        else if (current is JsonArray targetArr && int.TryParse(lastPart, out int targetIdx) && targetIdx < targetArr.Count)
            targetArr.RemoveAt(targetIdx);
    }

    private static bool ApplyTest(JsonNode root, string path, JsonNode? expected)
    {
        var found = Navigate(root, path);
        if (found is null && expected is null) return true;
        if (found is null || expected is null) return false;
        return JsonNode.DeepEquals(found, expected);
    }

    private static void ApplyMove(JsonNode root, string path, string from)
    {
        var value = Navigate(root, from);
        if (value is null) return;
        var cloned = value.DeepClone();
        ApplyRemove(root, from);
        ApplyAdd(root, path, cloned);
    }

    private static void ApplyCopy(JsonNode root, string path, string from)
    {
        var value = Navigate(root, from);
        if (value is null) return;
        ApplyAdd(root, path, value.DeepClone());
    }

    private static JsonNode? Navigate(JsonNode root, string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return root;
        var parts = path.Trim('/').Split('/');
        JsonNode current = root;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = UnescapeToken(parts[i]);
            if (current is JsonObject obj && obj.ContainsKey(part))
                current = obj[part]!;
            else if (current is JsonArray arr && int.TryParse(part, out int idx) && idx < arr.Count)
                current = arr[idx]!;
            else
                return null;
        }

        return current;
    }

    private static string UnescapeToken(string token)
    {
        return token.Replace("~1", "/").Replace("~0", "~");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer?.Dispose();
        _patchChannel.Writer.TryComplete();
    }
}
