using System.Diagnostics;
using AICodingAgent.Models;

namespace AICodingAgent.Tools;

public class ExecuteCommandTool : ITool
{
    public string Name => "execute_command";
    public string Description => "执行 shell 命令并返回输出。用于运行构建、测试、git 等操作。";

    public InputSchema Schema => new(
        new Dictionary<string, PropertySchema>
        {
            ["command"] = new("string", "要执行的命令")
        },
        new List<string> { "command" }
    );

    public async Task<string> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
    {
        if (!args.TryGetValue("command", out var cmdObj) || cmdObj == null)
            return "Error: missing required argument 'command'";

        var command = cmdObj.ToString()!;

        // 执行前请求用户确认（安全考虑）
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  ⚠ 即将执行: {command}\n  确认? (Y/n): ");
        Console.ResetColor();
        var confirm = Console.ReadLine()?.Trim().ToLower();
        if (confirm == "n" || confirm == "no")
            return "(skipped by user)";

        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{EscapeArg(command)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        using var process = Process.Start(psi);
        if (process == null)
            return "Error: failed to start process";

        try
        {
            // 并发读取 stdout/stderr 避免管道缓冲区死锁
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
            await Task.WhenAll(outputTask, errorTask);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                return "Error: command timed out (60s)";
            }

            var output = await outputTask;
            var error = await errorTask;

            var result = "";
            if (!string.IsNullOrEmpty(output)) result += $"STDOUT:\n{output}\n";
            if (!string.IsNullOrEmpty(error)) result += $"STDERR:\n{error}\n";
            result += $"Exit code: {process.ExitCode}";
            return result.Trim();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            return "Error: command timed out (60s)";
        }
    }

    private static string EscapeArg(string arg)
    {
        // 只转义双引号和反引号，防止 shell 注入
        return arg
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("`", "\\`")
            .Replace("$", "\\$");
    }
}
