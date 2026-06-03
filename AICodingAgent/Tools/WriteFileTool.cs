using AICodingAgent.Models;

namespace AICodingAgent.Tools;

public class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "写入文件。文件不存在则创建，存在则覆盖。";

    public InputSchema Schema => new(
        new Dictionary<string, PropertySchema>
        {
            ["file_path"] = new("string", "文件路径"),
            ["content"] = new("string", "写入的内容")
        },
        new List<string> { "file_path", "content" }
    );

    public async Task<string> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
    {
        if (!TryGetString(args, "file_path", out var path))
            return "Error: missing required argument 'file_path'";

        TryGetString(args, "content", out var content);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content ?? "", ct);
        return $"OK  wrote {(content ?? "").Length} chars to {path}";
    }

    private static bool TryGetString(Dictionary<string, object> args, string key, out string value)
    {
        if (args.TryGetValue(key, out var obj) && obj != null)
        {
            value = obj.ToString()!;
            return true;
        }
        value = "";
        return false;
    }
}
