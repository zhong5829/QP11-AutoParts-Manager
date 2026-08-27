using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace QP11.Core.AI;

public sealed class ToolCall
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public JsonObject Arguments { get; set; } = new();
}

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";

    public string? Content { get; set; }

    public List<ToolCall>? ToolCalls { get; set; }

    public string? ToolCallId { get; set; }

    public string? Name { get; set; }

    public static ChatMessage System(string content) => new() { Role = "system", Content = content };

    public static ChatMessage User(string content) => new() { Role = "user", Content = content };

    public static ChatMessage Assistant(string? content, List<ToolCall>? toolCalls = null)
        => new() { Role = "assistant", Content = content, ToolCalls = toolCalls };

    public static ChatMessage Tool(string toolCallId, string content)
        => new() { Role = "tool", ToolCallId = toolCallId, Content = content };
}

public sealed class ToolDeclaration
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public JsonNode Parameters { get; init; } = new JsonObject();
}

public sealed class ChatChunk
{
    public string? Delta { get; init; }

    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = System.Array.Empty<ToolCall>();

    public bool Finished { get; init; }
}
