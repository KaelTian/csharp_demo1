using AICodingAgent.Models;

namespace AICodingAgent.Tools;

public class GlobTool : ITool
{
    public string Name => "glob";
    public string Description => "按 glob 模式查找文件。支持 ** 递归匹配。例: '**/*.cs' 找所有 C# 文件。";

    public InputSchema Schema => new(
        new Dictionary<string, PropertySchema>
        {
            ["pattern"] = new("string", "glob 模式，如 '**/*.cs'")
        },
        new List<string> { "pattern" }
    );

    public Task<string> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
    {
        if (!args.TryGetValue("pattern", out var p) || p == null)
            return Task.FromResult("Error: missing required argument 'pattern'");

        var pattern = p.ToString()!;
        var baseDir = Directory.GetCurrentDirectory();

        if (pattern.StartsWith("**/"))
        {
            var ext = Path.GetExtension(pattern);
            var files = Directory.GetFiles(baseDir,
                string.IsNullOrEmpty(ext) ? "*" : $"*{ext}",
                SearchOption.AllDirectories);
            return Task.FromResult(string.Join("\n",
                files.Select(f => Path.GetRelativePath(baseDir, f)).Take(200)));
        }

        var direct = Directory.GetFiles(baseDir, pattern);
        return Task.FromResult(string.Join("\n",
            direct.Select(f => Path.GetRelativePath(baseDir, f))));
    }
}
