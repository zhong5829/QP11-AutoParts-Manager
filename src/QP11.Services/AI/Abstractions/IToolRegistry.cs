using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;

namespace QP11.Services.AI.Abstractions;

public interface IToolRegistry
{
    IReadOnlyList<ToolDeclaration> GetDeclarations();

    Task<ToolResult> DispatchAsync(string toolName, JsonObject args, CancellationToken cancellationToken = default);
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IChatTool> _tools;
    private readonly List<ToolDeclaration> _declarations;

    public ToolRegistry(IEnumerable<IChatTool> tools)
    {
        _tools = new Dictionary<string, IChatTool>(System.StringComparer.Ordinal);
        _declarations = new List<ToolDeclaration>();
        foreach (var t in tools)
        {
            if (_tools.ContainsKey(t.Name)) continue;
            _tools[t.Name] = t;
            _declarations.Add(new ToolDeclaration
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.ParameterSchema
            });
        }
    }

    public IReadOnlyList<ToolDeclaration> GetDeclarations() => _declarations;

    public async Task<ToolResult> DispatchAsync(
        string toolName, JsonObject args, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return ToolResult.Fail($"未注册的工具: {toolName}");

        try
        {
            return await tool.ExecuteAsync(args, cancellationToken);
        }
        catch (System.Exception ex)
        {
            return ToolResult.Fail($"工具 {toolName} 执行异常: {ex.Message}");
        }
    }
}
