using AICodingAgent.Models;

namespace AICodingAgent.Tools;

/// <summary>
/// 所有工具的公共接口。每个工具都有自己的名称、描述、参数 Schema 和执行逻辑。
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    InputSchema Schema { get; }
    Task<string> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct);
}
