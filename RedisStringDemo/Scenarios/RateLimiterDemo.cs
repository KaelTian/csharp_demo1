using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// 场景5：接口限流（固定窗口）
///
/// 利用 INCR + EXPIRE 实现固定窗口限流：
/// 以当前分钟为 key，每请求 INCR +1，到达阈值后拒绝。
/// 窗口结束自动过期重置。
///
/// 涉及命令：INCR, EXPIRE, TTL
/// </summary>
public static class RateLimiterDemo
{
    private static readonly int MaxRequests = 10;         // 每分钟最大请求数
    private static readonly int WindowSeconds = 60;       // 窗口大小（秒）
    private static readonly int BlockSeconds = 30;        // 超出后静默阻塞时间

    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  接口限流（固定窗口限流器）");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：单个用户的接口限流 ──
        Console.WriteLine("┌─ 场景A：用户 API 限流 ──────────────────┐");
        Console.WriteLine($"  规则: 每分钟最多 {MaxRequests} 次请求");
        Console.WriteLine();

        // 模拟用户发请求
        for (int i = 1; i <= MaxRequests + 3; i++)
        {
            var allowed = await CheckRateLimitAsync(db, "user:101", MaxRequests, WindowSeconds);

            if (allowed)
            {
                // 模拟处理业务逻辑
                await Task.Delay(100);
                Console.WriteLine($"  ✓ 第 {i,2} 次请求: 通过");
            }
            else
            {
                // 查看阻塞还有多久
                var ttl = await db.KeyTimeToLiveAsync("ratelimit:user:101:blocked");
                var blockRemaining = ttl?.TotalSeconds ?? 0;
                Console.WriteLine($"  ✗ 第 {i,2} 次请求: 限流拒绝!（阻塞剩余 {blockRemaining:F0} 秒）");
            }
        }

        Console.WriteLine();

        // ── 场景 B：检查当前限制状态 ──
        Console.WriteLine("┌─ 场景B：查看限流状态 ───────────────────┐");
        Console.WriteLine();

        var windowKey = $"ratelimit:user:101:{DateTime.Now:yyyyMMddHHmm}";
        var currentCount = await db.StringGetAsync(windowKey);
        var countTtl = await db.KeyTimeToLiveAsync(windowKey);

        Console.WriteLine($"  当前窗口 Key: {windowKey}");
        Console.WriteLine($"  当前计数:    {currentCount}");
        Console.WriteLine($"  窗口重置:    {(countTtl?.TotalSeconds ?? 0):F0} 秒后");

        Console.WriteLine();
        Console.WriteLine("✓ 接口限流演示完成。总结:");
        Console.WriteLine("  • 固定窗口限流: Key = ratelimit:{user}:{分钟}");
        Console.WriteLine("  • 每请求 INCR +1，超阈值后拒绝");
        Console.WriteLine("  • 配合 EXPIRE 实现窗口自动过期");
        Console.WriteLine("  • 注意: 固定窗口有边界突发问题，正式环境推荐滑动窗口或令牌桶");
        Console.WriteLine();
    }

    /// <summary>
    /// 检查是否允许通过限流
    /// </summary>
    private static async Task<bool> CheckRateLimitAsync(
        IDatabase db, string userId, int maxRequests, int windowSeconds)
    {
        var windowKey = $"ratelimit:{userId}:{DateTime.Now:yyyyMMddHHmm}";

        // 检查是否被临时阻塞
        var blockedKey = $"ratelimit:{userId}:blocked";
        var isBlocked = await db.KeyExistsAsync(blockedKey);
        if (isBlocked)
            return false;

        // INCR 并设置过期（首次 INCR 后设置 EXPIRE）
        var count = await db.StringIncrementAsync(windowKey);

        if (count == 1)
        {
            // 第一个请求，设置窗口过期时间
            await db.KeyExpireAsync(windowKey, TimeSpan.FromSeconds(windowSeconds));
        }

        if (count <= maxRequests)
            return true;

        // 超出限制，临时阻塞
        await db.StringSetAsync(blockedKey, "1", TimeSpan.FromSeconds(BlockSeconds));
        return false;
    }
}
