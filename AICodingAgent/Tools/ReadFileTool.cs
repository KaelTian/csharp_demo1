using AICodingAgent.Models;

namespace AICodingAgent.Tools;

public class ReadFileTool : ITool
{
    public string Name => "read_file";
    public string Description => "读取文件内容，返回带行号的文本。";

    public InputSchema Schema => new(
        new Dictionary<string, PropertySchema>
        {
            ["file_path"] = new("string", "要读取的文件路径（绝对或相对路径）")
        },
        new List<string> { "file_path" }
    );

    public async Task<string> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
    {
        if (!TryGetString(args, "file_path", out var path))
            return "Error: missing required argument 'file_path'";

        if (!File.Exists(path))
            return $"Error: file not found: {path}";

        var content = await File.ReadAllTextAsync(path, ct);
        var lines = content.Split('\n');
        return string.Join("\n", lines.Select((line, i) => $"{i + 1}\t{line}"));
    }

    private static bool TryGetString(Dictionary<string, object> args, string key, out string value)
    {
        if (args.TryGetValue(key, out var obj) && obj != null)
        {
            value = obj.ToString()!;
            return !string.IsNullOrEmpty(value);
        }
        value = "";
        return false;
    }
}
