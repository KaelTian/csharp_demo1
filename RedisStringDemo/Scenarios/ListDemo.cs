using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// Redis List 类型典型场景
///
/// List 基于 quicklist 实现（双向链表 + ziplist 混合结构）：
///   - 宏观：双向链表，O(1) 头尾插入/弹出
///   - 微观：每个节点是连续内存块（ziplist），减少指针开销
///
/// 核心能力：左/右两端插入和弹出 → 可做栈/队列/有限集合/消息队列
///
/// 涉及命令：LPUSH, RPUSH, LPOP, RPOP, LTRIM, LRANGE, LLEN, BLPOP, BRPOP
/// </summary>
public static class ListDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  List 类型典型场景");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：栈（Stack）LIFO ──
        Console.WriteLine("┌─ 场景A：栈（Stack）LIFO ─────────────────┐");
        Console.WriteLine();

        var stackKey = "stack:demo";

        // 清空旧数据
        await db.KeyDeleteAsync(stackKey);

        Console.WriteLine("  RPUSH 入栈: A → B → C → D → E");
        var items = new[] { "A", "B", "C", "D", "E" };
        foreach (var item in items)
            await db.ListRightPushAsync(stackKey, item);

        Console.WriteLine();
        Console.WriteLine("  RPOP 出栈（后进先出）:");
        for (int i = 0; i < 3; i++)
        {
            var popped = await db.ListRightPopAsync(stackKey);
            Console.WriteLine($"    RPOP → {popped}");
        }

        var remaining = await db.ListRangeAsync(stackKey);
        Console.WriteLine($"  栈中剩余: {string.Join(", ", remaining)}");
        Console.WriteLine();

        // ── 场景 B：队列（Queue）FIFO ──
        Console.WriteLine("┌─ 场景B：队列（Queue）FIFO ───────────────┐");
        Console.WriteLine();

        var queueKey = "queue:demo";
        await db.KeyDeleteAsync(queueKey);

        Console.WriteLine("  RPUSH 入队: 任务1 → 任务2 → 任务3 → 任务4 → 任务5");
        for (int i = 1; i <= 5; i++)
            await db.ListRightPushAsync(queueKey, $"任务{i}");

        Console.WriteLine();
        Console.WriteLine("  LPOP 出队（先进先出）:");
        while ((await db.ListLengthAsync(queueKey)) > 0)
        {
            var task = await db.ListLeftPopAsync(queueKey);
            Console.WriteLine($"    [处理] {task}");
        }

        Console.WriteLine("  队列已空，所有任务处理完毕");
        Console.WriteLine();

        // ── 场景 C：有限集合（LTRIM）──
        Console.WriteLine("┌─ 场景C：有限集合（LTRIM 裁剪）───────────┐");
        Console.WriteLine("  典型用途: 最近浏览记录、最新通知、操作日志");
        Console.WriteLine();

        var recentKey = "recent:views:1001";
        await db.KeyDeleteAsync(recentKey);

        Console.WriteLine("  模拟用户浏览商品，保留最近 5 条记录:");
        var products = new[] {
            "商品A:MacBook Pro", "商品B:iPhone 16", "商品C:AirPods",
            "商品D:iPad Air", "商品E:Apple Watch", "商品F:Mac mini",
            "商品G:iMac", "商品H:Vision Pro"
        };

        foreach (var product in products)
        {
            // 新浏览记录插入头部
            await db.ListLeftPushAsync(recentKey, product);
            // LTRIM 只保留前 5 条
            await db.ListTrimAsync(recentKey, 0, 4);
            Console.WriteLine($"    + {product}");
        }

        var recentItems = await db.ListRangeAsync(recentKey);
        Console.WriteLine();
        Console.WriteLine("  最终最近浏览记录（最新 5 条）:");
        foreach (var item in recentItems)
            Console.WriteLine($"    {item}");

        Console.WriteLine();

        // ── 场景 D：分页查询（LRANGE）──
        Console.WriteLine("┌─ 场景D：LRANGE 分页查询 ─────────────────┐");
        Console.WriteLine();

        var logKey = "logs:system";
        await db.KeyDeleteAsync(logKey);

        // 模拟 12 条系统日志
        for (int i = 1; i <= 12; i++)
            await db.ListRightPushAsync(logKey, $"[{6}/{i}] 系统事件 #{i}");

        Console.WriteLine("  系统日志列表（3 条/页）:");
        for (int page = 0; page < 4; page++)
        {
            var start = page * 3;
            var stop = start + 2;
            var pageItems = await db.ListRangeAsync(logKey, start, stop);

            if (pageItems.Length == 0) break;

            Console.WriteLine($"  ── 第 {page + 1} 页（LRANGE {start} {stop}）──");
            foreach (var item in pageItems)
                Console.WriteLine($"    {item}");
        }

        Console.WriteLine();

        // ── 场景 E：阻塞消息队列（BLPOP/BRPOP）──
        Console.WriteLine("┌─ 场景E：阻塞消息队列 ────────────────────┐");
        Console.WriteLine("  典型用途: 任务队列、通知推送、异步处理");
        Console.WriteLine();

        var msgQueue = "queue:notifications";
        await db.KeyDeleteAsync(msgQueue);

        Console.WriteLine("  ── 生产者-消费者模式 ──");
        Console.WriteLine("  消费者 A 启动（BLPOP 等待消息，超时 5 秒）...");

        // 使用阻塞弹出：BLPOP queue:notifications 5
        // consumerTask 会阻塞等待，主线程作为生产者
        var consumerTask = Task.Run(async () =>
        {
            Console.WriteLine("  消费者 A 正在等待消息...");
            var consumerDb = RedisConnection.GetDatabase();
            var rawResult = await consumerDb.ExecuteAsync("BLPOP", msgQueue, 5);
            var result = (RedisValue[]?)rawResult;

            if (result is { Length: >= 2 })
                Console.WriteLine($"  ✓ 消费者 A 收到: [{result[0]}] = {result[1]}");
            else
                Console.WriteLine($"  ✗ 消费者 A 超时退出（无消息到达）");
        });

        // 确保消费者已启动监听
        await Task.Delay(500);

        Console.WriteLine("  生产者推送消息...");
        await db.ListRightPushAsync(msgQueue, "用户 #1001 下单成功");
         await db.ListRightPushAsync(msgQueue, "用户 #1002 下单成功");
        Console.WriteLine($"  → 已推送: 用户 #1001 下单成功");

        // 等待消费者处理完成
        await consumerTask;

        Console.WriteLine();

        // 第二次演示：超时场景
        Console.WriteLine("  ── 超时演示 ──");
        Console.WriteLine("  消费者 B 启动（BLPOP 空队列，超时 3 秒）...");

        var timeoutTask = Task.Run(async () =>
        {
            var consumerDb = RedisConnection.GetDatabase();
            // BLPOP 空队列，3 秒后超时返回
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rawResult = await consumerDb.ExecuteAsync("BLPOP", "queue:empty", 3);
            var result = (RedisValue[]?)rawResult;
            sw.Stop();

            if (result is { Length: >= 2 })
                Console.WriteLine($"  ✓ 消费者 B 收到: {result[1]}");
            else
                Console.WriteLine($"  ✗ 消费者 B 超时（等待 {sw.ElapsedMilliseconds}ms，返回 nil）");
        });

        await timeoutTask;
        Console.WriteLine("  超时机制: Event Loop 的时间事件驱动，到期自动唤醒");
        Console.WriteLine();

        // ── 场景 F：可靠队列（RPOPLPUSH 备份）──
        Console.WriteLine("┌─ 场景F：可靠队列（RPOPLPUSH 备份）───────┐");
        Console.WriteLine("  处理消息前先备份到 backup list，处理失败可恢复");
        Console.WriteLine();

        var processingKey = "queue:orders:processing";
        var backupKey = "queue:orders:backup";
        await db.KeyDeleteAsync("queue:orders");
        await db.KeyDeleteAsync(processingKey);
        await db.KeyDeleteAsync(backupKey);

        // 模拟 3 个订单
        await db.ListRightPushAsync("queue:orders", new RedisValue[] { "ORD001", "ORD002", "ORD003" });
        Console.WriteLine("  待处理订单: ORD001, ORD002, ORD003");

        // 原子操作: RPOPLPUSH → 从 source 弹出并推入 destination
        var order = await db.ListRightPopLeftPushAsync("queue:orders", backupKey);
        Console.WriteLine($"  RPOPLPUSH: 取出 {order} 并备份到 backup list");

        // 模拟处理失败
        Console.WriteLine($"  [处理] {order} → 失败!");
        Console.WriteLine($"  可从 backup list 恢复: {backupKey}");
        Console.WriteLine();

        var remainingOrders = await db.ListRangeAsync("queue:orders");
        var backupItems = await db.ListRangeAsync(backupKey);
        Console.WriteLine($"  原队列剩余: {(remainingOrders.Length > 0 ? string.Join(", ", remainingOrders) : "空")}");
        Console.WriteLine($"  备份列表:   {string.Join(", ", backupItems)}");

        Console.WriteLine();
        Console.WriteLine("✓ List 类型演示完成。总结:");
        Console.WriteLine("  • 两端插入/弹出 = 栈(LIFO) + 队列(FIFO)");
        Console.WriteLine("  • LTRIM 实现有限集合（固定容量列表）");
        Console.WriteLine("  • LRANGE 支持分页/范围查询");
        Console.WriteLine("  • BLPOP/BRPOP 实现阻塞消息队列");
        Console.WriteLine("  • RPOPLPUSH 实现可靠处理（备份 + 重试）");
        Console.WriteLine("  • 底层: quicklist = 双向链表 + ziplist 节点");
        Console.WriteLine();
    }
}
