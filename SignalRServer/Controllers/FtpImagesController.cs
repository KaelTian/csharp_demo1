using Microsoft.AspNetCore.Mvc;
using FluentFTP;

namespace SignalRServer.Controllers;

[ApiController]
[Route("api/ftp/images")]
public class FtpImagesController : ControllerBase
{
    private readonly ILogger<FtpImagesController> _logger;
    private readonly IConfiguration _configuration;

    public FtpImagesController(ILogger<FtpImagesController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private string FtpHost => _configuration.GetValue<string>("Ftp:Host") ?? "192.168.0.189";
    private string FtpUser => _configuration.GetValue<string>("Ftp:Username") ?? "kael";
    private string FtpPass => _configuration.GetValue<string>("Ftp:Password") ?? "123456";
    private string FtpRoot => _configuration.GetValue<string>("Ftp:RootPath") ?? "/";

    /// <summary>
    /// 列出 FTP 服务器上指定目录的图片文件和子目录
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> ListImages([FromQuery] string? path = null)
    {
        try
        {
            using var client = new AsyncFtpClient(FtpHost, FtpUser, FtpPass);
            await client.Connect();

            var targetPath = string.IsNullOrEmpty(path) ? FtpRoot : $"{FtpRoot}/{path}";
            var items = await client.GetListing(targetPath);

            var directories = items
                .Where(f => f.Type == FtpObjectType.Directory)
                .Select(f => f.Name)
                .ToList();

            var imageFiles = items
                .Where(f => f.Type == FtpObjectType.File &&
                            (f.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                             f.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                             f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.Name)
                .ToList();

            await client.Disconnect();
            return Ok(new { directories, files = imageFiles, currentPath = path ?? "" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP 连接或列表获取失败");
            return StatusCode(500, new { error = "无法连接到 FTP 服务器", detail = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定图片文件的流（支持多级子目录路径）
    /// </summary>
    [HttpGet("{*filepath}")]
    public async Task<IActionResult> GetImage(string filepath)
    {
        // 防止路径遍历攻击：只拦截 ".."，允许正常子目录 "/"
        if (filepath.Contains(".."))
        {
            return BadRequest(new { error = "非法的文件路径" });
        }

        try
        {
            using var client = new AsyncFtpClient(FtpHost, FtpUser, FtpPass);
            await client.Connect();

            // 判断文件是否存在
            var exists = await client.FileExists($"{FtpRoot}/{filepath}");
            if (!exists)
            {
                return NotFound(new { error = $"文件 {filepath} 不存在" });
            }

            var ms = new MemoryStream();
            await client.DownloadStream(ms, $"{FtpRoot}/{filepath}");
            ms.Position = 0;

            // 根据扩展名推断 ContentType
            var contentType = filepath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
            _logger.LogInformation($"获取图片信息: {filepath} {contentType}");
            return File(ms, contentType,filepath, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载图片 {Filename} 失败", filepath);
            return StatusCode(500, new { error = $"下载图片失败", detail = ex.Message });
        }
    }
}
