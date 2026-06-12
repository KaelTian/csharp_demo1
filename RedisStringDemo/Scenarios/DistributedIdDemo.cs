using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// 场景3：分布式全局ID生成器
///
/// 传统数据库自增ID在分库分表下不好用，Redis INCR 可以生成全局唯一ID。
/// 格式: {业务前缀}:{日期}:{序列号}
///
/// 涉及命令：INCR, GET
/// </summary>
public static class DistributedIdDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  分布式全局ID生成器");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        var today = DateTime.Now.ToString("yyyyMMdd");

        // ── 场景 A：订单号生成 ──
        Console.WriteLine("┌─ 场景A：订单号生成 ──────────────────────┐");
        Console.WriteLine("  格式: ORD:{日期}:{序列号}");
        Console.WriteLine("  模拟两个服务同时生成订单号...");
        Console.WriteLine();

        // 模拟两个服务同时生成订单
        var serviceA = GenerateOrderIdAsync(db, today, "Service-A");
        var serviceB = GenerateOrderIdAsync(db, today, "Service-B");

        // 批量生成订单
        for (int i = 0; i < 3; i++)
        {
            await GenerateOrderIdAsync(db, today, "Service-A");
            await Task.Delay(10);
            await GenerateOrderIdAsync(db, today, "Service-B");
            await Task.Delay(10);
        }

        await serviceA;
        await serviceB;

        Console.WriteLine();

        // ── 场景 B：用户ID生成 ──
        Console.WriteLine("┌─ 场景B：用户ID生成 ──────────────────────┐");

        var uidSeqKey = "global:uid:seq";
        // 初始设置为 10000（从 10001 开始）
        await db.StringSetAsync(uidSeqKey, 10000);

        var uids = new List<long>();
        for (int i = 0; i < 5; i++)
        {
            var newId = await db.StringIncrementAsync(uidSeqKey);
            uids.Add(newId);
        }

        Console.WriteLine($"  生成的用户ID: {string.Join(", ", uids)}");

        Console.WriteLine();

        // ── 场景 C：展示ID在 Redis 中的存储 ──
        Console.WriteLine("┌─ 场景C：查看Redis中的ID状态 ─────────────┐");
        var orderKey = $"id:seq:order:{today}";
        var uidKey = "global:uid:seq";

        var currentOrderSeq = await db.StringGetAsync(orderKey);
        var currentUid = await db.StringGetAsync(uidKey);

        Console.WriteLine($"  订单序列 key: {orderKey} = {currentOrderSeq}");
        Console.WriteLine($"  UID 序列 key: {uidKey} = {currentUid}");
        Console.WriteLine();

        Console.WriteLine("✓ 分布式ID生成演示完成。总结:");
        Console.WriteLine("  • INCR 天然适合生成全局唯一递增序列");
        Console.WriteLine("  • 日期前缀 + Redis 序列实现业务可读ID");
        Console.WriteLine("  • 单次 INCR QPS 可达 10万+，毫秒级响应");
        Console.WriteLine("  • Key 格式: id:seq:order:{date} / global:uid:seq");
        Console.WriteLine();
    }

    private static async Task GenerateOrderIdAsync(IDatabase db, string today, string serviceName)
    {
        var seqKey = $"id:seq:order:{today}";
        var seq = await db.StringIncrementAsync(seqKey);
        var orderId = $"ORD{today}{seq:D5}";
        Console.WriteLine($"  [{serviceName}] 生成订单号: {orderId}");
    }
}
