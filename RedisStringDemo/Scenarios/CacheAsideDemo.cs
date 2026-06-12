using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// 场景1：Cache-Aside 缓存穿透保护
///
/// 这是 Redis 最经典的使用模式。
/// 查询流程：先查 Redis → 命中直接返回 → 未命中查 MySQL → 回写 Redis → 返回
///
/// 涉及命令：GET, SET, SETEX, EXISTS, DEL
/// </summary>
public static class CacheAsideDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();
        var fakeDb = new FakeDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  Cache-Aside 缓存穿透保护");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        const int productId = 1;
        var cacheKey = $"product:{productId}";

        // 清理缓存，确保第一次是未命中
        await db.KeyDeleteAsync(cacheKey);

        // ── 第一次查询：缓存未命中 → 穿透到数据库 ──
        Console.WriteLine($"[请求1] 查询商品 #{productId}");
        Console.WriteLine("  → Redis 查找...");

        var cached = await db.StringGetAsync(cacheKey);
        if (cached.IsNullOrEmpty)
        {
            Console.WriteLine("  → 缓存未命中（MISS），穿透到数据库...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var product = await fakeDb.GetProductAsync(productId);
            sw.Stop();

            Console.WriteLine($"  → 数据库查询完成，耗时 {sw.ElapsedMilliseconds}ms");

            if (product != null)
            {
                // 将数据库结果写入 Redis，设置 60 秒过期
                var json = System.Text.Json.JsonSerializer.Serialize(product);
                await db.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(60));
                Console.WriteLine($"  → 已回写 Redis（TTL: 60s），数据: {product.Name} ¥{product.Price}");
            }
        }
        else
        {
            Console.WriteLine($"  → 缓存命中（HIT）: {cached}");
        }

        Console.WriteLine();

        // ── 第二次查询：缓存命中 ──
        Console.WriteLine($"[请求2] 再次查询商品 #{productId}");

        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        cached = await db.StringGetAsync(cacheKey);
        sw2.Stop();

        Console.WriteLine($"  → 缓存命中（HIT），耗时 {sw2.ElapsedMilliseconds}ms");
        if (!cached.IsNullOrEmpty)
        {
            var product2 = System.Text.Json.JsonSerializer.Deserialize<Product>(cached!);
            Console.WriteLine($"  → 数据: {product2!.Name} ¥{product2.Price}");
        }

        Console.WriteLine();

        // ── 模拟数据更新：先更新 DB，再删除缓存 ──
        Console.WriteLine("[更新] 管理员修改商品名称...");
        await fakeDb.UpdateProductNameAsync(productId, "MacBook Pro 14 (2026款)");
        await db.KeyDeleteAsync(cacheKey);
        Console.WriteLine("  → 数据库已更新，Redis 缓存已删除（下次查询会重新加载）");
        Console.WriteLine();

        // ── 第三次查询：缓存被删，重新穿透 ──
        Console.WriteLine("[请求3] 更新后再次查询（缓存已失效）...");
        cached = await db.StringGetAsync(cacheKey);
        if (cached.IsNullOrEmpty)
        {
            Console.WriteLine("  → 缓存未命中（MISS），重新从数据库加载...");
            var product3 = await fakeDb.GetProductAsync(productId);
            if (product3 != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(product3);
                await db.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(60));
                Console.WriteLine($"  → 新数据已缓存: {product3.Name} ¥{product3.Price}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("✓ Cache-Aside 模式演示完成。总结:");
        Console.WriteLine("  • 缓存命中时：纳秒级响应，不查数据库");
        Console.WriteLine("  • 缓存穿透时：查数据库 + 回写，设 TTL 防雪崩");
        Console.WriteLine("  • 数据更新时：先写 DB，再删缓存（被动更新）");
        Console.WriteLine("  • Key 格式: product:{id}  ➔  value: JSON 序列化");
        Console.WriteLine();
    }
}
