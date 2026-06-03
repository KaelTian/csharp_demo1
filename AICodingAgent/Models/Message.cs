using System.Text.Json.Serialization;

namespace AICodingAgent.Models;

/// <summary>
/// 对话消息。支持 OpenAI / DeepSeek 的四种角色：
/// system, user, assistant, tool（工具调用的结果）。
/// </summary>
public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("content")]
    public string? Content { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; }

    [JsonConstructor]
    public Message(string role, string? content, List<ToolCall>? toolCalls = null, string? toolCallId = null)
    {
        Role = role;
        Content = content;
        ToolCalls = toolCalls;
        ToolCallId = toolCallId;
    }

    public static Message System(string text) => new("system", text);
    public static Message User(string text) => new("user", text);
    public static Message Assistant(string? content, List<ToolCall>? toolCalls = null)
        => new("assistant", content, toolCalls);
    public static Message Tool(string toolCallId, string content)
        => new("tool", content, toolCallId: toolCallId);
}

/// <summary>
/// 工具调用 — 放在 assistant 消息里。
/// function.arguments 是 JSON 字符串。
/// </summary>
public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("type")]
    public string Type { get; } = "function";

    [JsonPropertyName("function")]
    public FunctionCall Function { get; }

    [JsonConstructor]
    public ToolCall(string id, FunctionCall function)
    {
        Id = id;
        Function = function;
    }
}

public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("arguments")]
    public string Arguments { get; }

    [JsonConstructor]
    public FunctionCall(string name, string arguments)
    {
        Name = name;
        Arguments = arguments;
    }
}
