using AICodingAgent.Llm;
using AICodingAgent.Models;
using AICodingAgent.Tools;

namespace AICodingAgent.Core;

/// <summary>
/// Agent 主循环：思考 → 行动 → 观察。
///
/// 每轮迭代：
///   1. 发送对话历史 + 工具定义给 LLM
///   2. 解析回复中的文本和工具调用
///   3. 有工具调用 → 执行 → 结果加入对话 → 回到 1
///   4. 无工具调用 → 本轮结束，等待用户下个任务
/// </summary>
public class AgentLoop
{
    private readonly LlmService _llm;
    private readonly ToolRegistry _tools;
    private readonly List<Message> _messages = new();

    private const string SystemPrompt =
        """
        你是一个 AI 编程助手，帮助用户完成软件工程任务。你可以读写文件、搜索文件和执行命令。

        规则：
        - 先理解需求，再动手
        - 需要信息时使用工具
        - 做出修改后验证结果
        - 任务完成后用中文总结你所做的
        - 保持简洁，不要啰嗦

        可用工具：read_file, write_file, execute_command, glob, grep
        """;

    public AgentLoop(LlmService llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
        _messages.Add(Message.System(SystemPrompt));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════╗");
        Console.WriteLine("║   AI Coding Agent v1         ║");
        Console.WriteLine("║   Powered by DeepSeek        ║");
        Console.WriteLine("╚══════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine("输入你的目标（或输入 'exit' 退出）：\n");

        while (!ct.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("▸ ");
            Console.ResetColor();
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Trim().ToLower() == "exit") break;

            _messages.Add(Message.User(input));
            await RunIterationAsync(ct);
        }
    }

    private async Task RunIterationAsync(CancellationToken ct)
    {
        const int maxSteps = 25;

        for (int step = 0; step < maxSteps; step++)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n── Step {step + 1} ──");
            Console.ResetColor();

            var (content, toolCalls) = await _llm.SendAsync(
                _messages, _tools.GetDefinitions(), ct);

            if (content == null && toolCalls == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("API 返回错误，请检查 API Key 和网络连接。");
                Console.ResetColor();
                break;
            }

            // 把 assistant 回复加入对话
            _messages.Add(Message.Assistant(content, toolCalls));

            if (content != null)
            {
                Console.WriteLine(content);
            }

            // 没有工具调用 → 本轮结束
            if (toolCalls == null || toolCalls.Count == 0)
                break;

            // 执行每个工具调用
            foreach (var toolCall in toolCalls)
            {
                var args = ParseArgs(toolCall.Function.Arguments);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ▶ {toolCall.Function.Name}({string.Join(", ", args.Select(a => $"{a.Key}={a.Value}"))})");
                Console.ResetColor();

                var result = await _tools.ExecuteAsync(toolCall.Function.Name, args, ct);

                var display = result.Length > 600
                    ? result[..600] + $"\n  ... ({result.Length} chars total)"
                    : result;

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  Result: {display}");
                Console.ResetColor();

                _messages.Add(Message.Tool(toolCall.Id, result));
            }

            if (step == maxSteps - 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("⚠ 达到最大迭代次数，部分任务可能未完成。");
                Console.ResetColor();
            }
        }

        TrimMessages();
    }

    /// <summary>
    /// 安全裁剪消息历史。只删除完整轮次，不破坏 tool_call/tool 配对和角色交替。
    /// </summary>
    private void TrimMessages()
    {
        const int maxMessages = 60;
        if (_messages.Count <= maxMessages) return;

        // 从后往前找到第 3 个完全结束的轮次（assistant 不含 tool_calls）
        // 删除它之前的所有消息（保留第一条 system 消息）
        int roundsFound = 0;
        int trimEnd = 1; // 保留 index 0 (system)

        for (int i = _messages.Count - 1; i >= 1 && roundsFound < 3; i--)
        {
            if (_messages[i].Role != "assistant") continue;
            if (_messages[i].ToolCalls is { Count: > 0 }) continue;

            // 找到一个完整轮次结束，向前找对应的 user 消息作为起点
            roundsFound++;
            if (roundsFound == 3)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_messages[j].Role == "user" && _messages[j].ToolCallId == null)
                    {
                        trimEnd = j;
                        break;
                    }
                }
                break;
            }
        }

        if (trimEnd > 1)
            _messages.RemoveRange(1, trimEnd - 1);
    }

    /// <summary>
    /// 解析工具参数的 JSON 字符串。
    /// </summary>
    private static Dictionary<string, object> ParseArgs(string argumentsJson)
    {
        if (string.IsNullOrEmpty(argumentsJson))
            return new Dictionary<string, object>();

        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
            return dict ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }
}
