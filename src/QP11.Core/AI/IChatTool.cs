using System.Text.Json.Nodes;

namespace QP11.Core.AI;

public interface IChatTool
{
    string Name { get; }

    string Description { get; }

    JsonNode ParameterSchema { get; }

    Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default);
}
