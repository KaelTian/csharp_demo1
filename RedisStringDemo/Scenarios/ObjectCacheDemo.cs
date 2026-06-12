using System.Text.Json;
using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// 场景4：对象缓存（Session / 配置 / 用户信息）
///
/// C# 对象 → JSON 序列化 → Redis String 存储
/// 适合缓存用户 Session、配置信息、热点数据等结构化数据。
///
/// 涉及命令：SET, GET, SETEX, TTL, EXISTS
/// </summary>
public static class ObjectCacheDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  对象缓存（Session / 配置 / 用户信息）");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：缓存用户 Session ──
        Console.WriteLine("┌─ 场景A：用户 Session 缓存 ──────────────┐");
        Console.WriteLine();

        // 用户登录后，将 Session 信息存入 Redis
        var session = new UserSession
        {
            UserId = 1001,
            UserName = "张三",
            Role = "admin",
            LoginTime = DateTime.Now,
            Token = "eyJhbGciOiJIUzI1NiIs..."
        };

        var sessionKey = $"session:{session.Token}";
        var sessionJson = JsonSerializer.Serialize(session);
        await db.StringSetAsync(sessionKey, sessionJson, TimeSpan.FromMinutes(30));

        Console.WriteLine("  用户登录，Session 已缓存:");
        Console.WriteLine($"  Key:  {sessionKey}");
        Console.WriteLine($"  TTL:  30 分钟");
        Console.WriteLine($"  JSON: {sessionJson}");
        Console.WriteLine();

        // 后续请求从 Redis 读取 Session
        var cachedSession = await db.StringGetAsync(sessionKey);
        if (!cachedSession.IsNullOrEmpty)
        {
            var restored = JsonSerializer.Deserialize<UserSession>(cachedSession!);
            Console.WriteLine("  从 Redis 还原 Session:");
            Console.WriteLine($"  UserId:   {restored!.UserId}");
            Console.WriteLine($"  UserName: {restored.UserName}");
            Console.WriteLine($"  Role:     {restored.Role}");
            var sessionTtl = await db.KeyTimeToLiveAsync(sessionKey);
            Console.WriteLine($"  TTL 剩余: {sessionTtl?.TotalSeconds:F0} 秒");
        }

        Console.WriteLine();

        // ── 场景 B：缓存应用配置 ──
        Console.WriteLine("┌─ 场景B：应用配置缓存 ───────────────────┐");
        Console.WriteLine();

        // 应用启动时从配置文件加载，缓存到 Redis
        var appConfig = new AppConfig
        {
            SiteName = "技术博客",
            PageSize = 20,
            EnableComment = true,
            MaintenanceMode = false,
            AllowedUploadTypes = new[] { ".jpg", ".png", ".gif", ".mp4" },
            MaxUploadSizeMb = 50
        };

        var configKey = "config:app:site";
        var configJson = JsonSerializer.Serialize(appConfig);

        // 使用 SETEX 一步完成设值 + 过期
        await db.StringSetAsync(configKey, configJson, TimeSpan.FromHours(1));
        Console.WriteLine($"  应用配置已缓存");
        Console.WriteLine($"  JSON: {configJson}");
        Console.WriteLine();

        // 模拟其它服务读取配置
        var rawConfig = await db.StringGetAsync(configKey);
        var config = JsonSerializer.Deserialize<AppConfig>(rawConfig!);
        Console.WriteLine("  其他服务读取配置:");
        Console.WriteLine($"  站点名称:      {config!.SiteName}");
        Console.WriteLine($"  每页数量:      {config.PageSize}");
        Console.WriteLine($"  评论功能:      {(config.EnableComment ? "开启" : "关闭")}");
        Console.WriteLine($"  允许上传类型:  {string.Join(", ", config.AllowedUploadTypes)}");
        Console.WriteLine($"  最大上传:      {config.MaxUploadSizeMb}MB");
        var configTtl = await db.KeyTimeToLiveAsync(configKey);
        Console.WriteLine($"  缓存剩余 TTL:  {configTtl?.TotalMinutes:F0} 分钟");

        Console.WriteLine();

        // ── 场景 C：检查 Key 是否存在（防穿透）──
        Console.WriteLine("┌─ 场景C：缓存穿透防护 ───────────────────┐");
        Console.WriteLine();

        var userKey = "user:9999:profile";

        var exists = await db.KeyExistsAsync(userKey);
        Console.WriteLine($"  查询不存在的用户 Key [{userKey}]:");
        Console.WriteLine($"  EXISTS: {exists}");

        // 用空值缓存防止缓存穿透
        if (!exists)
        {
            // 即使数据不存在，也缓存一个空标记，防止恶意查询穿透到 DB
            await db.StringSetAsync(userKey, "NULL", TimeSpan.FromSeconds(30));
            Console.WriteLine("  → 已缓存空标记（30秒过期），防止缓存穿透");
        }

        Console.WriteLine();
        Console.WriteLine("✓ 对象缓存演示完成。总结:");
        Console.WriteLine("  • C# 对象 → JSON → Redis String，反序列化还原");
        Console.WriteLine("  • SETEX 原子设值 + 过期，适合 Session/配置缓存");
        Console.WriteLine("  • EXISTS 预查可防止缓存穿透");
        Console.WriteLine("  • Key 格式: session:{token} / config:{domain} / user:{id}:profile");
        Console.WriteLine();
    }
}

internal class UserSession
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime LoginTime { get; set; }
    public string Token { get; set; } = "";
}

internal class AppConfig
{
    public string SiteName { get; set; } = "";
    public int PageSize { get; set; }
    public bool EnableComment { get; set; }
    public bool MaintenanceMode { get; set; }
    public string[] AllowedUploadTypes { get; set; } = [];
    public int MaxUploadSizeMb { get; set; }
}
