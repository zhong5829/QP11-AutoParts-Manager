using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Services.AI.Abstractions;

namespace QP11.Services.AI;

public sealed class AgnesOrchestrator
{
    private readonly IChatClient _chatClient;
    private readonly IToolRegistry _registry;
    private readonly AgnesOptions _options;

    public AgnesOrchestrator(IChatClient chatClient, IToolRegistry registry, AgnesOptions options)
    {
        _chatClient = chatClient;
        _registry = registry;
        _options = options;
    }

    public async IAsyncEnumerable<AgnesEvent> StreamConversationAsync(
        List<ChatMessage> history, string userInput, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>(history.Count + 3);
        messages.Add(ChatMessage.System(_options.SystemPrompt));
        messages.AddRange(history);

        var userMsg = ChatMessage.User(userInput);
        messages.Add(userMsg);
        history.Add(userMsg);

        var declarations = _registry.GetDeclarations();

        for (int round = 0; _options.MaxToolRounds <= 0 || round < _options.MaxToolRounds; round++)
        {
            var request = new ChatRequest
            {
                Model = _options.Model,
                Messages = messages,
                Tools = declarations,
                EnableStream = _options.EnableStreaming,
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens
            };

            var textBuilder = new StringBuilder();
            List<ToolCall> toolCalls = new();

            await foreach (var chunk in _chatClient.StreamAsync(request, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Delta))
                {
                    textBuilder.Append(chunk.Delta);
                    yield return new AgnesEvent.TextDelta(chunk.Delta);
                }
                if (chunk.ToolCalls.Count > 0)
                    toolCalls = new List<ToolCall>(chunk.ToolCalls);
            }

            if (toolCalls.Count == 0)
            {
                var finalContent = textBuilder.ToString();
                var assistantMsg = ChatMessage.Assistant(finalContent);
                messages.Add(assistantMsg);
                history.Add(assistantMsg);
                yield return new AgnesEvent.Finished();
                yield break;
            }

            var assistantToolMsg = ChatMessage.Assistant(textBuilder.ToString(), toolCalls);
            messages.Add(assistantToolMsg);
            history.Add(assistantToolMsg);

            foreach (var tc in toolCalls)
            {
                yield return new AgnesEvent.ToolCalling(tc.Name, tc.Arguments.ToJsonString());

                ToolResult result;
                try
                {
                    result = await _registry.DispatchAsync(tc.Name, tc.Arguments, cancellationToken);
                }
                catch (System.Exception ex)
                {
                    result = ToolResult.Fail(ex.Message);
                }

                yield return new AgnesEvent.ToolResult(tc.Name, result.Content, result.Success);

                var toolMsg = ChatMessage.Tool(tc.Id, result.Content);
                messages.Add(toolMsg);
                history.Add(toolMsg);
            }
        }

        yield return new AgnesEvent.Error($"工具调用轮次超过上限 {_options.MaxToolRounds}，已终止");
    }
}
