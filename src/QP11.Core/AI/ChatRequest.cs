using System.Collections.Generic;

namespace QP11.Core.AI;

public sealed class ChatRequest
{
    public string Model { get; init; } = "deepseek-chat";

    public IReadOnlyList<ChatMessage> Messages { get; init; } = System.Array.Empty<ChatMessage>();

    public IReadOnlyList<ToolDeclaration> Tools { get; init; } = System.Array.Empty<ToolDeclaration>();

    public bool EnableStream { get; init; } = true;

    public double Temperature { get; init; } = 0.3;

    public int MaxTokens { get; init; } = 2048;
}
