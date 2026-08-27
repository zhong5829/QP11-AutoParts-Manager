using System.Text.Json.Nodes;

namespace QP11.Core.AI;

public sealed record ToolResult(bool Success, string Content, string? Error = null)
{
    public static ToolResult Ok(string content) => new(true, content, null);

    public static ToolResult Ok(JsonNode payload) => new(true, payload.ToJsonString(), null);

    public static ToolResult Fail(string error) => new(false, string.Empty, error);
}
