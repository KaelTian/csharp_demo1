using AICodingAgent.Models;

namespace AICodingAgent.Tools;

/// <summary>
/// 工具的注册中心。所有工具在这里注册，提供定义列表和按名称执行的能力。
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public List<ToolDefinition> GetDefinitions() =>
        _tools.Values
            .Select(t => new ToolDefinition(t.Name, t.Description, t.Schema))
            .ToList();

    public async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return $"Error: unknown tool '{toolName}'";

        try
        {
            return await tool.ExecuteAsync(args, ct);
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
    }
}
