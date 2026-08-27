using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;

namespace QP11.Services.AI.Tools;

public abstract class ChatToolBase : IChatTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonNode ParameterSchema { get; }

    public abstract Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default);

    protected static string GetStringArg(JsonObject args, string key, string defaultValue = "")
    {
        if (args.TryGetPropertyValue(key, out var node) && node != null)
        {
            return node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToString();
        }
        return defaultValue;
    }

    protected static long? GetLongArg(JsonObject args, string key)
    {
        if (args.TryGetPropertyValue(key, out var node) && node != null)
        {
            if (node is JsonValue v)
            {
                if (v.TryGetValue<long>(out var l)) return l;
                if (v.TryGetValue<int>(out var i)) return i;
                if (v.TryGetValue<string>(out var s) && long.TryParse(s, out var parsed)) return parsed;
            }
            if (long.TryParse(node.ToString(), out var manual)) return manual;
        }
        return null;
    }

    protected static int? GetIntArg(JsonObject args, string key, int? defaultValue = null)
    {
        if (args.TryGetPropertyValue(key, out var node) && node != null)
        {
            if (node is JsonValue v)
            {
                if (v.TryGetValue<int>(out var i)) return i;
                if (v.TryGetValue<long>(out var l)) return (int)l;
                if (v.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed)) return parsed;
            }
            if (int.TryParse(node.ToString(), out var manual)) return manual;
        }
        return defaultValue;
    }

    protected static JsonObject NumberSchema(string description)
        => new() { ["type"] = "number", ["description"] = description };

    protected static JsonObject StringSchema(string description)
        => new() { ["type"] = "string", ["description"] = description };
}
