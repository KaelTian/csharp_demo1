using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// Redis Sorted Set 类型典型场景
///
/// Sorted Set = Set（唯一性）+ 分值（Score），按分值排序
/// 底层: skiplist（跳跃表）+ hashtable 组合
///   - skiplist: 按 score 排序，范围查询 O(logN)
///   - hashtable: O(1) 按 member 查 score
///
/// 涉及命令：ZADD, ZRANK, ZREVRANK, ZRANGE, ZREVRANGE,
///          ZINCRBY, ZSCORE, ZCARD, ZREM, ZRANGEBYSCORE,
///          ZREMRANGEBYSCORE, ZREMRANGEBYRANK
/// </summary>
public static class SortedSetDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  Sorted Set 类型典型场景");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：游戏积分排行榜 ──
        Console.WriteLine("┌─ 场景A：游戏积分排行榜 ──────────────────┐");
        Console.WriteLine();

        await db.KeyDeleteAsync("game:leaderboard:2026W24");

        // ZADD 添加玩家积分
        var players = new SortedSetEntry[]
        {
            new("玩家A", 1500),
            new("玩家B", 2800),
            new("玩家C", 950),
            new("玩家D", 3200),
            new("玩家E", 2100),
            new("玩家F", 1750),
            new("玩家G", 5000),
        };
        await db.SortedSetAddAsync("game:leaderboard:2026W24", players);

        Console.WriteLine("  ZADD 玩家积分:");
        foreach (var p in players)
            Console.WriteLine($"    {p.Element}: {p.Score} 分");

        // ZREVRANGE 降序排列（高分在前）
        var top3 = await db.SortedSetRangeByRankWithScoresAsync(
            "game:leaderboard:2026W24", 0, 2, Order.Descending);

        Console.WriteLine();
        Console.WriteLine("  ZREVRANGE 排行榜 TOP 3:");
        var rank = 1;
        foreach (var entry in top3)
            Console.WriteLine($"    🥇 第{rank++}名: {entry.Element} = {entry.Score}");
        Console.WriteLine();

        // ZRANK / ZREVRANK 查询排名
        var rankA = await db.SortedSetRankAsync("game:leaderboard:2026W24", "玩家A");
        var revRankA = await db.SortedSetRankAsync("game:leaderboard:2026W24", "玩家A", Order.Descending);
        var scoreA = await db.SortedSetScoreAsync("game:leaderboard:2026W24", "玩家A");

        Console.WriteLine("  查询玩家A:");
        Console.WriteLine($"    ZRANK（正序排名）: 第 {rankA} 名");
        Console.WriteLine($"    ZREVRANK（倒序排名）: 第 {revRankA} 名");
        Console.WriteLine($"    ZSCORE（当前积分）: {scoreA}");
        Console.WriteLine();

        // ZINCRBY 实时更新积分
        Console.WriteLine("  玩家A 完成一局游戏，ZINCRBY +500 分");
        await db.SortedSetIncrementAsync("game:leaderboard:2026W24", "玩家A", 500);
        var newScoreA = await db.SortedSetScoreAsync("game:leaderboard:2026W24", "玩家A");
        Console.WriteLine($"  玩家A 当前积分: {newScoreA}");

        var newRankA = await db.SortedSetRankAsync("game:leaderboard:2026W24", "玩家A", Order.Descending);
        Console.WriteLine($"  玩家A 最新排名: 第 {newRankA} 名");
        Console.WriteLine();

        // ── 场景 B：热门文章 / 商品热榜 ──
        Console.WriteLine("┌─ 场景B：热门文章榜单（ZINCRBY 实时热度）─┐");
        Console.WriteLine();

        await db.KeyDeleteAsync("hot:articles");

        Console.WriteLine("  ZINCRBY 模拟用户访问/点赞实时增加热度:");
        var articles = new[] { "文章A", "文章B", "文章C", "文章D", "文章E" };

        // 模拟访问量
        foreach (var a in articles)
        {
            var visits = Random.Shared.Next(5, 20);
            for (int i = 0; i < visits; i++)
                await db.SortedSetIncrementAsync("hot:articles", a, 1);
        }

        var hotList = await db.SortedSetRangeByRankWithScoresAsync(
            "hot:articles", 0, -1, Order.Descending);

        Console.WriteLine("  最终热度排名:");
        var hotRank = 1;
        foreach (var entry in hotList)
            Console.WriteLine($"    #{hotRank++,2} {entry.Element}: {entry.Score} 次访问");

        Console.WriteLine();

        // ── 场景 C：延迟队列（按时间戳排序）──
        Console.WriteLine("┌─ 场景C：延迟任务队列 ────────────────────┐");
        Console.WriteLine("  利用时间戳作为 score，到期执行");
        Console.WriteLine();

        await db.KeyDeleteAsync("delay:orders");

        // 模拟订单 30 分钟后自动取消，score = 时间戳
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var orders = new SortedSetEntry[]
        {
            new("ORD001", now + 1800),  // 30分钟后
            new("ORD002", now + 3600),  // 1小时后
            new("ORD003", now + 600),   // 10分钟后
            new("ORD004", now + 7200),  // 2小时后
            new("ORD005", now + 120),   // 2分钟后
        };
        await db.SortedSetAddAsync("delay:orders", orders);

        Console.WriteLine("  订单（ZRANGEBYSCORE 0 当前时间戳，取到期订单）:");
        Console.WriteLine($"  当前时间戳: {now}");
        Console.WriteLine();

        // 模拟时间推移 — 查 15 分钟后的到期订单
        var checkTime = now + 900; // 15分钟后
        var expiredOrders = await db.SortedSetRangeByScoreAsync(
            "delay:orders", 0, checkTime);

        Console.WriteLine($"  在 +15min 时刻检查（score ≤ {checkTime}）:");
        foreach (var order in expiredOrders)
            Console.WriteLine($"    → {order} 已到期，执行自动取消");

        // 从队列中移除已处理的订单
        await db.SortedSetRemoveRangeByScoreAsync("delay:orders", 0, checkTime);
        var remaining = await db.SortedSetLengthAsync("delay:orders");
        Console.WriteLine($"  已移除到期订单，队列剩余: {remaining} 个");

        Console.WriteLine();

        // ── 场景 D：滑动窗口限流（1 分钟窗口）──
        Console.WriteLine("┌─ 场景D：滑动窗口限流 ────────────────────┐");
        Console.WriteLine("  用时间戳做 score，窗口外数据自动清理");
        Console.WriteLine();

        await db.KeyDeleteAsync("ratelimit:sliding:user:101");

        var windowMs = 60000L;          // 1 分钟窗口
        var maxRequests = 5;            // 窗口内最多 5 次
        var userId = "user:101";

        Console.WriteLine($"  规则: {maxRequests} 次/分钟（滑动窗口）");
        Console.WriteLine();

        // 模拟发送请求
        for (int i = 1; i <= maxRequests + 2; i++)
        {
            var allow = await SlidingWindowCheckAsync(db, userId, windowMs, maxRequests);
            Console.WriteLine($"    第 {i,2} 次请求: {(allow ? "✓ 通过" : "✗ 限流拒绝")}");
            await Task.Delay(50); // 模拟请求间隔
        }

        Console.WriteLine();
        Console.WriteLine("✓ Sorted Set 类型演示完成。总结:");
        Console.WriteLine("  • ZADD + ZREVRANGE 实现排行榜");
        Console.WriteLine("  • ZINCRBY 实时更新热度/积分");
        Console.WriteLine("  • ZRANK/ZREVRANK 查询排名（O(logN)）");
        Console.WriteLine("  • ZRANGEBYSCORE 实现延迟队列、时间线");
        Console.WriteLine("  • ZREMRANGEBYSCORE 清理窗口外数据（滑动窗口限流）");
        Console.WriteLine("  • 底层: skiplist + hashtable，读写都是 O(logN)");
        Console.WriteLine();
    }

    /// <summary>
    /// 滑动窗口限流检查
    /// </summary>
    private static async Task<bool> SlidingWindowCheckAsync(
        IDatabase db, string userId, long windowMs, int maxRequests)
    {
        var key = $"ratelimit:sliding:{userId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - windowMs;

        // 清理窗口外的旧记录
        await db.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

        // 统计窗口内请求数
        var count = await db.SortedSetLengthAsync(key);

        if (count >= maxRequests)
            return false;

        // 记录本次请求
        var member = $"{userId}:{now}:{Random.Shared.Next(1000)}";
        await db.SortedSetAddAsync(key, member, now);
        await db.KeyExpireAsync(key, TimeSpan.FromMinutes(2)); // 防内存泄漏

        return true;
    }
}
