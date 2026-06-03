using System.Text.Json.Serialization;

namespace AICodingAgent.Models;

/// <summary>
/// 工具的 OpenAI function calling 定义。
///
/// 序列化示例：
/// {
///   "type": "function",
///   "function": {
///     "name": "read_file",
///     "description": "读取文件内容",
///     "parameters": { "type": "object", "properties": {...}, "required": [...] }
///   }
/// }
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition Function { get; }

    public ToolDefinition(string name, string description, InputSchema parameters)
    {
        Function = new FunctionDefinition(name, description, parameters);
    }
}

public class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("description")]
    public string Description { get; }

    [JsonPropertyName("parameters")]
    public InputSchema Parameters { get; }

    public FunctionDefinition(string name, string description, InputSchema parameters)
    {
        Name = name;
        Description = description;
        Parameters = parameters;
    }
}

public class InputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertySchema> Properties { get; }

    [JsonPropertyName("required")]
    public List<string> Required { get; }

    public InputSchema(Dictionary<string, PropertySchema> properties, List<string> required)
    {
        Properties = properties;
        Required = required;
    }
}

public class PropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; }

    [JsonPropertyName("description")]
    public string Description { get; }

    public PropertySchema(string type, string description)
    {
        Type = type;
        Description = description;
    }
}
