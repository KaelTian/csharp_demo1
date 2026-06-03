using System.Reflection;
using AICodingAgent.Core;
using AICodingAgent.Llm;
using AICodingAgent.Tools;

namespace AICodingAgent;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // --version / -v 参数：打印版本号并退出
        if (args.Contains("--version") || args.Contains("-v"))
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"AICodingAgent v{version}");
            return;
        }

        // 优先级：--key 参数 > 环境变量
        var apiKey = ParseArg(args, "--key")
                    ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        var model = ParseArg(args, "--model") ?? "deepseek-v4-flash";

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.Error.WriteLine("错误: 未设置 DEEPSEEK_API_KEY");
            Console.Error.WriteLine("用法: --key sk-xxx [--model deepseek-v4-flash]");
            Console.Error.WriteLine("或者设置环境变量 DEEPSEEK_API_KEY");
            return;
        }

        using var llm = new LlmService(apiKey, model);
        var tools = new ToolRegistry();
        tools.Register(new ReadFileTool());
        tools.Register(new WriteFileTool());
        tools.Register(new ExecuteCommandTool());
        tools.Register(new GlobTool());
        tools.Register(new GrepTool());

        var agent = new AgentLoop(llm, tools);
        await agent.RunAsync(CancellationToken.None);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n再见！");
        Console.ResetColor();
    }

    static string? ParseArg(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}
