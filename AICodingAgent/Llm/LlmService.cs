using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AICodingAgent.Models;

namespace AICodingAgent.Llm;

/// <summary>
/// DeepSeek (OpenAI 兼容) API 客户端。
/// 支持 function calling（工具调用）。
/// </summary>
public sealed class LlmService : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public LlmService(string apiKey, string model = "deepseek-chat")
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _model = model;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// 发送消息到 DeepSeek API，返回（文本回复, 工具调用列表）。
    /// </summary>
    public async Task<(string? content, List<ToolCall>? toolCalls)> SendAsync(
        List<Message> messages,
        List<ToolDefinition> tools,
        CancellationToken ct)
    {
        var body = new
        {
            model = _model,
            messages,
            tools,
            tool_choice = "auto" as string
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        var response = await _http.PostAsync(
            "https://api.deepseek.com/chat/completions",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"API Error ({response.StatusCode}):\n{responseBody}");
            return (null, null);
        }

        using var doc = JsonDocument.Parse(responseBody);
        var choice = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        var content = choice.TryGetProperty("content", out var c)
            ? c.GetString()
            : null;

        List<ToolCall>? toolCalls = null;
        if (choice.TryGetProperty("tool_calls", out var tc))
        {
            toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(tc.GetRawText(), JsonOpts);
        }

        return (content, toolCalls);
    }
}
