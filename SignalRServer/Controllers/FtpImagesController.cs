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
    /// 列出 FTP 服务器上的所有图片文件
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<string>>> ListImages()
    {
        try
        {
            using var client = new AsyncFtpClient(FtpHost, FtpUser, FtpPass);
            await client.Connect();

            var files = await client.GetListing(FtpRoot);

            var imageFiles = files
                .Where(f => f.Type == FtpObjectType.File &&
                            (f.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                             f.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                             f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.Name)
                .ToList();

            await client.Disconnect();
            return Ok(imageFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP 连接或列表获取失败");
            return StatusCode(500, new { error = "无法连接到 FTP 服务器", detail = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定图片文件的流
    /// </summary>
    [HttpGet("{filename}")]
    public async Task<IActionResult> GetImage(string filename)
    {
        // 简单安全检查：防止路径遍历
        if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
        {
            return BadRequest(new { error = "非法的文件名" });
        }

        try
        {
            using var client = new AsyncFtpClient(FtpHost, FtpUser, FtpPass);
            await client.Connect();

            // 判断文件是否存在
            var exists = await client.FileExists($"{FtpRoot}/{filename}");
            if (!exists)
            {
                return NotFound(new { error = $"文件 {filename} 不存在" });
            }

            var ms = new MemoryStream();
            await client.DownloadStream(ms, $"{FtpRoot}/{filename}");
            ms.Position = 0;

            // 根据扩展名推断 ContentType
            var contentType = filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";

            return File(ms, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载图片 {Filename} 失败", filename);
            return StatusCode(500, new { error = $"下载图片失败", detail = ex.Message });
        }
    }
}
