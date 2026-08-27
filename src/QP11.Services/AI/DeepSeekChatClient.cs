using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Services.AI.Abstractions;

namespace QP11.Services.AI;

public sealed class DeepSeekChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly AgnesOptions _options;

    public DeepSeekChatClient(HttpClient http, AgnesOptions options)
    {
        _http = http;
        _options = options;
    }

    public async IAsyncEnumerable<ChatChunk> StreamAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"DeepSeek API 返回 {(int)response.StatusCode}: {err}");
        }

        if (request.EnableStream)
        {
            await foreach (var chunk in ParseStreamAsync(response.Content, cancellationToken))
                yield return chunk;
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return ParseNonStream(body);
        }
    }

    private string BuildPayload(ChatRequest request)
    {
        var messages = new JsonArray();
        foreach (var m in request.Messages)
        {
            var obj = new JsonObject { ["role"] = m.Role };
            if (!string.IsNullOrEmpty(m.Content))
                obj["content"] = m.Content;
            if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                var tcs = new JsonArray();
                foreach (var tc in m.ToolCalls)
                {
                    tcs.Add(new JsonObject
                    {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = tc.Name,
                            ["arguments"] = tc.Arguments.ToJsonString()
                        }
                    });
                }
                obj["tool_calls"] = tcs;
            }
            if (!string.IsNullOrEmpty(m.ToolCallId))
                obj["tool_call_id"] = m.ToolCallId;
            if (!string.IsNullOrEmpty(m.Name))
                obj["name"] = m.Name;
            messages.Add(obj);
        }

        var root = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = request.EnableStream,
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxTokens
        };

        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var t in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = t.Parameters.DeepClone()
                    }
                });
            }
            root["tools"] = tools;
        }

        return root.ToJsonString();
    }

    private async IAsyncEnumerable<ChatChunk> ParseStreamAsync(
        HttpContent content, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var accumulator = new Dictionary<int, ToolCall>();
        var argBuffer = new Dictionary<int, StringBuilder>();
        var order = new List<int>();

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data:")) continue;
            var data = line.Substring(5).TrimStart();
            if (data == "[DONE]")
            {
                FinalizeToolCalls(accumulator, argBuffer);
                yield return new ChatChunk { Finished = true, ToolCalls = BuildList(accumulator, order) };
                yield break;
            }

            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }
            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("choices", out var choicesEl) ||
                    choicesEl.GetArrayLength() == 0) continue;
                var choice = choicesEl[0];
                if (!choice.TryGetProperty("delta", out var delta)) continue;

                if (delta.TryGetProperty("content", out var contentEl) &&
                    contentEl.ValueKind == JsonValueKind.String)
                {
                    var text = contentEl.GetString();
                    if (!string.IsNullOrEmpty(text))
                        yield return new ChatChunk { Delta = text };
                }

                if (delta.TryGetProperty("tool_calls", out var tcEl))
                {
                    foreach (var tc in tcEl.EnumerateArray())
                    {
                        var index = tc.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : 0;
                        if (!accumulator.ContainsKey(index))
                        {
                            accumulator[index] = new ToolCall();
                            argBuffer[index] = new StringBuilder();
                            order.Add(index);
                        }
                        var acc = accumulator[index];
                        if (tc.TryGetProperty("id", out var idEl))
                            acc.Id = idEl.GetString() ?? acc.Id;
                        if (tc.TryGetProperty("function", out var fnEl))
                        {
                            if (fnEl.TryGetProperty("name", out var nameEl))
                                acc.Name = nameEl.GetString() ?? acc.Name;
                            if (fnEl.TryGetProperty("arguments", out var argEl))
                            {
                                var frag = argEl.GetString();
                                if (!string.IsNullOrEmpty(frag))
                                    argBuffer[index].Append(frag);
                            }
                        }
                    }
                }
            }
        }

        FinalizeToolCalls(accumulator, argBuffer);
        yield return new ChatChunk { Finished = true, ToolCalls = BuildList(accumulator, order) };
    }

    private static void FinalizeToolCalls(
        Dictionary<int, ToolCall> accumulator, Dictionary<int, StringBuilder> argBuffer)
    {
        foreach (var kvp in argBuffer)
        {
            if (!accumulator.TryGetValue(kvp.Key, out var tc)) continue;
            var raw = kvp.Value.ToString();
            if (string.IsNullOrEmpty(raw)) continue;
            try
            {
                var node = JsonNode.Parse(raw);
                tc.Arguments = node?.AsObject() ?? new JsonObject();
            }
            catch
            {
                tc.Arguments = new JsonObject();
            }
        }
    }

    private static IReadOnlyList<ToolCall> BuildList(
        Dictionary<int, ToolCall> accumulator, List<int> order)
    {
        if (accumulator.Count == 0) return System.Array.Empty<ToolCall>();
        var list = new List<ToolCall>(order.Count);
        foreach (var idx in order)
            if (accumulator.TryGetValue(idx, out var tc))
                list.Add(tc);
        return list;
    }

    private static ChatChunk ParseNonStream(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("choices", out var choicesEl) || choicesEl.GetArrayLength() == 0)
            return new ChatChunk { Finished = true };

        var message = choicesEl[0].GetProperty("message");
        var content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;

        var toolCalls = new List<ToolCall>();
        if (message.TryGetProperty("tool_calls", out var tcEl))
        {
            foreach (var tc in tcEl.EnumerateArray())
            {
                var item = new ToolCall
                {
                    Id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : ""
                };
                if (tc.TryGetProperty("function", out var fnEl))
                {
                    item.Name = fnEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var args = fnEl.TryGetProperty("arguments", out var a) ? a.GetString() : null;
                    if (!string.IsNullOrEmpty(args))
                    {
                        try { item.Arguments = JsonNode.Parse(args)?.AsObject() ?? new JsonObject(); }
                        catch { item.Arguments = new JsonObject(); }
                    }
                }
                toolCalls.Add(item);
            }
        }

        return new ChatChunk { Delta = content, ToolCalls = toolCalls, Finished = true };
    }
}
