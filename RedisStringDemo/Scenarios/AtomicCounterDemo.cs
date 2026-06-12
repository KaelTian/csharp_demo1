using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// 场景2：原子计数器（视频播放量 / 文章点赞）
///
/// Redis INCR/DECR 是原子操作，在高并发下保证计数准确。
/// MySQL 的行锁在写并发高时性能差，而 Redis 单线程模型天然原子。
///
/// 涉及命令：INCR, INCRBY, DECR, GETSET, GET
/// </summary>
public static class AtomicCounterDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  原子计数器（视频播放量 / 文章点赞）");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：视频播放量（并发 INCR）──
        Console.WriteLine("┌─ 场景A：视频播放量统计 ─────────────────┐");

        var videoKey = "video:1001:views";
        await db.KeyDeleteAsync(videoKey);

        Console.WriteLine("  模拟 50 个用户同时观看同一视频（并发 INCR）...");
        Console.WriteLine("  每次观看调用一次 INCR video:1001:views");

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(db.StringIncrementAsync(videoKey));
        }
        await Task.WhenAll(tasks);

        var views = await db.StringGetAsync(videoKey);
        Console.WriteLine($"  最终播放量: {views} 次（期待 50，确保无并发丢失）");
        Console.WriteLine();

        // ── 场景 B：文章点赞与取消点赞 ──
        Console.WriteLine("┌─ 场景B：文章点赞/取消点赞 ───────────────┐");

        var articleKey = "article:2001:likes";
        await db.KeyDeleteAsync(articleKey);
        var fakeDb = new FakeDatabase();

        // 模拟 30 人点赞，10 人取消点赞
        Console.WriteLine("  30 人点赞（INCR），10 人取消点赞（DECR）...");

        var likeTasks = new List<Task>();
        for (int i = 0; i < 30; i++)
            likeTasks.Add(db.StringIncrementAsync(articleKey));
        for (int i = 0; i < 10; i++)
            likeTasks.Add(db.StringDecrementAsync(articleKey));
        await Task.WhenAll(likeTasks);

        var likes = (long)await db.StringGetAsync(articleKey);
        Console.WriteLine($"  最终点赞数: {likes}（期待 20）");

        // 展示定时持久化到 MySQL 的模式
        Console.WriteLine();
        Console.WriteLine("  ── 定时将 Redis 计数持久化到 MySQL ──");
        fakeDb.SetArticleLikes("article:2001", (int)likes);

        // 同步后使用 GETSET 原子重置，防止重复统计
        var oldValue = await db.StringGetSetAsync(articleKey, 0);
        Console.WriteLine($"  GETSET 原子重置: 旧值={oldValue}, 新值=0");
        Console.WriteLine($"  MySQL 中的持久化计数: {fakeDb.GetArticleLikes("article:2001")}");

        // 如果在上次持久化之后又有新的点赞
        for (int i = 0; i < 3; i++)
            await db.StringIncrementAsync(articleKey);
        var remaining = await db.StringGetAsync(articleKey);
        Console.WriteLine($"  持久化后又新增 3 次点赞，Redis 中剩余: {remaining}（下次定时任务会继续同步）");
        Console.WriteLine();

        // ── 场景 C：INCRBY 批量操作 ──
        Console.WriteLine("┌─ 场景C：批量计数 ────────────────────────┐");

        var batchKey = "live:room:3001:heat";
        await db.KeyDeleteAsync(batchKey);

        await db.StringIncrementAsync(batchKey, 100);   // 送礼物+100
        await db.StringIncrementAsync(batchKey, 50);    // 点赞+50
        await db.StringDecrementAsync(batchKey, 20);    // 用户离开-20

        var heat = await db.StringGetAsync(batchKey);
        Console.WriteLine($"  直播热度（批量增减）: {heat}（期待 130）");

        Console.WriteLine();
        Console.WriteLine("✓ 原子计数器演示完成。总结:");
        Console.WriteLine("  • INCR/DECR 为原子操作，高并发不丢失");
        Console.WriteLine("  • 适用于播放量、点赞、收藏、热度等场景");
        Console.WriteLine("  • 配合 GETSET 可实现定时持久化 + 重置");
        Console.WriteLine("  • Key 格式: video:{id}:views / article:{id}:likes");
        Console.WriteLine();
    }
}
