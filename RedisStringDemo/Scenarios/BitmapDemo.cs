using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// 场景6：位图应用（用户签到 / 活跃统计）
///
/// Redis String 底层支持位操作，一个 key 可以存储海量位数据。
/// 每个 bit 代表一个状态（0/1），极大节省内存。
/// 1 个 key（365 bit）≈ 46 字节 就能存一年的签到数据。
///
/// 涉及命令：SETBIT, GETBIT, BITCOUNT, BITFIELD
/// </summary>
public static class BitmapDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  位图应用（用户签到 / 活跃统计）");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：用户月度签到 ──
        Console.WriteLine("┌─ 场景A：用户月度签到 ───────────────────┐");
        Console.WriteLine();

        var today = DateTime.Now;
        var checkInKey = $"checkin:user:1001:{today:yyyyMM}";

        await db.KeyDeleteAsync(checkInKey);

        // 模拟用户签到（第1天到第15天，随机签到）
        Console.WriteLine("  用户签到记录（6月1日 ~ 6月15日）:");
        Console.WriteLine("  格式: [日] 状态");
        Console.WriteLine("  ─────────────────────────────");

        var checkInDays = new[] { 1, 2, 5, 6, 7, 10, 12, 13, 14, 15 };
        foreach (var day in checkInDays)
        {
            // SETBIT key offset value — offset 从 0 开始
            await db.StringSetBitAsync(checkInKey, day - 1, true);
        }

        // 展示签到结果
        for (int day = 1; day <= 15; day++)
        {
            var bit = await db.StringGetBitAsync(checkInKey, day - 1);
            var mark = bit ? "✓" : " ";
            Console.Write($"  [{day,2}] {mark} ");
            if (day % 5 == 0) Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine();

        // 统计当月总签到天数
        var totalDays = await db.StringBitCountAsync(checkInKey);
        Console.WriteLine($"  当月总签到天数: {totalDays} 天");

        // 用二进制字符串展示签到位图
        var rawBytes = await db.StringGetAsync(checkInKey);
        if (!rawBytes.IsNullOrEmpty)
        {
            var bits = new System.Text.StringBuilder();
            var bytes = (byte[])rawBytes!;
            foreach (var b in bytes)
                bits.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            Console.WriteLine($"  签到位图（二进制）: {bits}");
        }

        Console.WriteLine();

        // ── 场景 B：连续签到统计 ──
        Console.WriteLine("┌─ 场景B：连续签到判断 ───────────────────┐");
        Console.WriteLine();

        // 检查昨天和今天是否签到来判断连续
        var yesterdayBit = await db.StringGetBitAsync(checkInKey, today.Day - 2);  // offset=day-1，昨天=day-2
        var todayBit = await db.StringGetBitAsync(checkInKey, today.Day - 1);

        Console.WriteLine($"  昨日({today.AddDays(-1):MM/dd})签到: {(yesterdayBit ? "✓" : "✗")}");
        Console.WriteLine($"  今日({today:MM/dd})签到: {(todayBit ? "✓" : "✗")}");
        Console.WriteLine($"  连续签到状态: {(yesterdayBit && todayBit ? "✓ 连续!" : "  —")}");

        Console.WriteLine();

        // ── 场景 C：日活跃用户统计 ──
        Console.WriteLine("┌─ 场景C：日活跃用户（DAU）统计 ──────────┐");
        Console.WriteLine();

        var dauKey = $"dau:2026:06:11";
        await db.KeyDeleteAsync(dauKey);

        // 模拟用户登录（用户ID 作为 offset）
        var activeUsers = new[] { 1001, 1002, 1005, 1010, 1020, 1050, 1100, 1200, 1500, 2000 };
        Console.WriteLine($"  今日活跃用户: [{string.Join(", ", activeUsers)}]");

        foreach (var uid in activeUsers)
        {
            await db.StringSetBitAsync(dauKey, uid, true);
        }

        var dau = await db.StringBitCountAsync(dauKey);
        Console.WriteLine($"  今日 DAU: {dau}");

        // 模拟第二天
        var dauKey2 = $"dau:2026:06:12";
        await db.KeyDeleteAsync(dauKey2);
        var activeUsers2 = new[] { 1001, 1002, 1003, 1005, 1010, 1020, 1030, 1100, 1200, 1500, 2000, 3000 };
        foreach (var uid in activeUsers2)
        {
            await db.StringSetBitAsync(dauKey2, uid, true);
        }

        // BITOP 计算连续两天的活跃用户（AND 交集）
        var retainKey = "dau:retain:0611-0612";
        await db.KeyDeleteAsync(retainKey);
        await db.ExecuteAsync("BITOP", "AND", retainKey, dauKey, dauKey2);

        var retain = await db.StringBitCountAsync(retainKey);
        Console.WriteLine($"  次日留存用户数: {retain}（BITOP AND 计算两日交集）");
        Console.WriteLine();

        Console.WriteLine("✓ 位图应用演示完成。总结:");
        Console.WriteLine("  • 1 个位 = 1 个状态，节省内存（1 年签到 ≈ 46 字节）");
        Console.WriteLine("  • SETBIT/GETBIT 实现签到、在线状态");
        Console.WriteLine("  • BITCOUNT 统计天数/活跃数");
        Console.WriteLine("  • BITOP 实现留存分析（AND = 留存, OR = 累计）");
        Console.WriteLine("  • Key 格式: checkin:user:{id}:{yyyMM} / dau:{yyyy:MM:dd}");
        Console.WriteLine();
    }
}
