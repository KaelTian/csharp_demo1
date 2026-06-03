using System.Text.RegularExpressions;
using AICodingAgent.Models;

namespace AICodingAgent.Tools;

public class GrepTool : ITool
{
    public string Name => "grep";
    public string Description => "在文件内容中搜索正则表达式模式，返回匹配的文件和行号。";

    public InputSchema Schema => new(
        new Dictionary<string, PropertySchema>
        {
            ["pattern"] = new("string", "要搜索的正则表达式"),
            ["glob"] = new("string", "可选: 限定文件类型，如 '*.cs'")
        },
        new List<string> { "pattern" }
    );

    public Task<string> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
    {
        if (!args.TryGetValue("pattern", out var p) || p == null)
            return Task.FromResult("Error: missing required argument 'pattern'");

        var pattern = p.ToString()!;
        args.TryGetValue("glob", out var globObj);
        var glob = globObj?.ToString();

        var baseDir = Directory.GetCurrentDirectory();
        var searchPattern = string.IsNullOrEmpty(glob) ? "*" : glob;

        var results = new List<string>();
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var file in Directory.GetFiles(baseDir, searchPattern, SearchOption.AllDirectories)
            .Take(500))
        {
            if (ct.IsCancellationRequested) break;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (regex.IsMatch(lines[i]))
                {
                    results.Add($"{Path.GetRelativePath(baseDir, file)}:{i + 1}: {lines[i].Trim()}");
                }
                if (results.Count > 200) break;
            }
            if (results.Count > 200) break;
        }

        return Task.FromResult(results.Count > 0
            ? string.Join("\n", results.Take(200))
            : $"No matches for '{pattern}'");
    }
}
