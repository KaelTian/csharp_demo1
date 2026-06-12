using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// Redis Set 类型典型场景
///
/// Set 特点：无序、唯一、支持集合运算（交/并/差）
/// 底层用 hashtable 或 intset（全整数时），O(1) 增删查
///
/// 涉及命令：SADD, SREM, SMEMBERS, SCARD, SISMEMBER,
///          SINTER, SUNION, SDIFF,
///          SRANDMEMBER, SPOP, SMOVE
/// </summary>
public static class SetDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  Set 类型典型场景");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：文章标签系统 ──
        Console.WriteLine("┌─ 场景A：文章标签系统 ─────────────────────┐");
        Console.WriteLine();

        // 两篇文章打标签
        await db.KeyDeleteAsync("article:5001:tags");
        await db.KeyDeleteAsync("article:5002:tags");
        await db.KeyDeleteAsync("article:5003:tags");

        await db.SetAddAsync("article:5001:tags", new RedisValue[] { "C#", ".NET", "Redis", "性能优化" });
        await db.SetAddAsync("article:5002:tags", new RedisValue[] { "Redis", "分布式", "架构设计" });
        await db.SetAddAsync("article:5003:tags", new RedisValue[] { "C#", "架构设计", "微服务" });

        Console.WriteLine("  文章 #5001 标签: C#, .NET, Redis, 性能优化");
        Console.WriteLine("  文章 #5002 标签: Redis, 分布式, 架构设计");
        Console.WriteLine("  文章 #5003 标签: C#, 架构设计, 微服务");
        Console.WriteLine();

        // SINTER — 查找哪些标签是共用的
        var commonTags = await db.SetCombineAsync(SetOperation.Intersect,
            "article:5001:tags", "article:5002:tags");
        Console.WriteLine($"  SINTER(#5001, #5002) 共同标签: [{string.Join(", ", commonTags)}]");

        // SUNION — 合并所有标签
        var unionTags = await db.SetCombineAsync(SetOperation.Union,
            new RedisKey[] { "article:5001:tags", "article:5002:tags", "article:5003:tags" });
        Console.WriteLine($"  SUNION 全部标签: [{string.Join(", ", unionTags)}]");

        // SDIFF — 文章 #5001 有但 #5002 没有的标签
        var diffTags = await db.SetCombineAsync(SetOperation.Difference,
            "article:5001:tags", "article:5002:tags");
        Console.WriteLine($"  SDIFF(#5001 - #5002): [{string.Join(", ", diffTags)}]");

        // 通过某标签反查文章
        Console.WriteLine();
        Console.WriteLine("  ── 通过标签反查文章 ──");
        await db.SetAddAsync("tag:C#:articles", new RedisValue[] { "5001", "5003" });
        await db.SetAddAsync("tag:Redis:articles", new RedisValue[] { "5001", "5002" });

        var csharpArticles = await db.SetMembersAsync("tag:C#:articles");
        var redisArticles = await db.SetMembersAsync("tag:Redis:articles");

        Console.WriteLine($"  标签「C#」的文章: [{string.Join(", ", csharpArticles)}]");
        Console.WriteLine($"  标签「Redis」的文章: [{string.Join(", ", redisArticles)}]");

        Console.WriteLine();

        // ── 场景 B：社交关系 - 关注/粉丝/共同关注 ──
        Console.WriteLine("┌─ 场景B：社交关系 ─────────────────────────┐");
        Console.WriteLine();

        await db.KeyDeleteAsync("user:1001:following");
        await db.KeyDeleteAsync("user:1002:following");
        await db.KeyDeleteAsync("user:1003:following");

        // 用户关注列表
        await db.SetAddAsync("user:1001:following", new RedisValue[] { "2001", "2002", "2003", "2004" });
        await db.SetAddAsync("user:1002:following", new RedisValue[] { "2001", "2003", "2005", "2006", "2007" });
        await db.SetAddAsync("user:1003:following", new RedisValue[] { "2004", "2008", "2009" });

        Console.WriteLine("  用户 #1001 关注: [2001, 2002, 2003, 2004]");
        Console.WriteLine("  用户 #1002 关注: [2001, 2003, 2005, 2006, 2007]");
        Console.WriteLine("  用户 #1003 关注: [2004, 2008, 2009]");

        // 共同关注
        var mutual = await db.SetCombineAsync(SetOperation.Intersect,
            "user:1001:following", "user:1002:following");
        Console.WriteLine($"  共同关注（你可能认识）: [{string.Join(", ", mutual)}]");

        // 推荐关注（#1002 关注了但 #1001 没关注的）
        var recommend = await db.SetCombineAsync(SetOperation.Difference,
            "user:1002:following", "user:1001:following");
        Console.WriteLine($"  推荐给 #1001（#1002 关注了但你还没）: [{string.Join(", ", recommend)}]");

        // 关注数
        var count = await db.SetLengthAsync("user:1001:following");
        Console.WriteLine($"  SCARD #1001 关注数: {count}");

        Console.WriteLine();

        // ── 场景 C：抽奖系统（去重 + 随机抽取）──
        Console.WriteLine("┌─ 场景C：抽奖系统 ─────────────────────────┐");
        Console.WriteLine();

        await db.KeyDeleteAsync("lottery:20260611");

        var participants = new[] { "用户A", "用户B", "用户C", "用户D", "用户E", "用户F", "用户G", "用户H", "用户A", "用户B" };
        Console.WriteLine($"  参与活动（含重复登记）: {string.Join(", ", participants)}");

        foreach (var user in participants)
            await db.SetAddAsync("lottery:20260611", user);  // SADD 自动去重

        var uniqueCount = await db.SetLengthAsync("lottery:20260611");
        Console.WriteLine($"  SADD 去重后实际参与人数（SCARD）: {uniqueCount}");

        // SRANDMEMBER 随机抽奖（不中奖不走）
        Console.WriteLine();
        Console.WriteLine("  抽奖环节:");
        var winner1 = await db.SetRandomMemberAsync("lottery:20260611");
        Console.WriteLine($"  SRANDMEMBER 三等奖: {winner1}");

        // SPOP 随机抽取并移除（中奖者离开奖池）
        var winner2 = await db.SetPopAsync("lottery:20260611");
        Console.WriteLine($"  SPOP 二等奖（抽走）: {winner2}");

        var winner3 = await db.SetPopAsync("lottery:20260611");
        Console.WriteLine($"  SPOP 一等奖（抽走）: {winner3}");

        Console.WriteLine();

        // ── 场景 D：UV 统计（独立访客）──
        Console.WriteLine("┌─ 场景D：UV 统计 ──────────────────────────┐");
        Console.WriteLine();

        await db.KeyDeleteAsync("uv:index:20260611");

        // 模拟当天访问的用户
        var visitors = new[] { "ip:192.168.1.1", "ip:192.168.1.2", "ip:192.168.1.1", "ip:192.168.1.3", "ip:192.168.1.2" };
        foreach (var ip in visitors)
            await db.SetAddAsync("uv:index:20260611", ip);

        var uv = await db.SetLengthAsync("uv:index:20260611");
        Console.WriteLine($"  原始访问记录: {visitors.Length} 条（含重复）");
        Console.WriteLine($"  SADD 去重 + SCARD 统计 → 独立访客(UV): {uv}");

        Console.WriteLine();
        Console.WriteLine("✓ Set 类型演示完成。总结:");
        Console.WriteLine("  • SADD/SISMEMBER 实现标签系统、社交关系");
        Console.WriteLine("  • SINTER/SUNION/SDIFF 集合运算（共同关注/推荐）");
        Console.WriteLine("  • SRANDMEMBER/SPOP 随机抽奖（不重复中奖）");
        Console.WriteLine("  • SCARD 去重统计 UV");
        Console.WriteLine("  • SISMEMBER O(1) 判断元素是否存在（布隆过滤器的替代方案）");
        Console.WriteLine("  • Key 格式: article:{id}:tags / user:{id}:following / uv:{page}:{date}");
        Console.WriteLine();
    }
}
