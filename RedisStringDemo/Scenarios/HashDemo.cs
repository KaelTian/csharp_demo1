using StackExchange.Redis;

namespace RedisStringDemo.Scenarios;

/// <summary>
/// Redis Hash 类型典型场景
///
/// Hash 适合存储对象/实体，相比 String JSON 序列化的优势：
///   1. 可单独读写某个字段（避免大 JSON 的全量反序列化）
///   2. 支持对单个字段做原子运算（HINCRBY）
///   3. 内存效率更高（对小对象有优化）
///
/// 涉及命令：HSET, HGET, HGETALL, HMGET, HINCRBY, HDEL, HLEN, HEXISTS, HSETNX, HKEYS, HVALS
/// </summary>
public static class HashDemo
{
    public static async Task RunAsync()
    {
        var db = RedisConnection.GetDatabase();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  Hash 类型典型场景");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        // ── 场景 A：用户信息存储（对象 field 映射）──
        Console.WriteLine("┌─ 场景A：用户信息存储（field 映射对象属性）┐");
        Console.WriteLine();

        var userKey = "user:2001";

        // 清除旧数据
        await db.KeyDeleteAsync(userKey);

        // HSET 设置单个 field
        await db.HashSetAsync(userKey, "name", "李四");
        await db.HashSetAsync(userKey, "age", "28");
        await db.HashSetAsync(userKey, "city", "北京");
        await db.HashSetAsync(userKey, "title", "高级工程师");
        await db.HashSetAsync(userKey, "score", "8500");

        Console.WriteLine("  HSET 逐字段写入用户数据:");
        Console.WriteLine("    user:2001 → name=李四, age=28, city=北京, ...");

        // HGET 读取单个 field
        var name = await db.HashGetAsync(userKey, "name");
        var age = await db.HashGetAsync(userKey, "age");

        Console.WriteLine();
        Console.WriteLine("  HGET 单独读取某个字段:");
        Console.WriteLine($"    name = {name}");
        Console.WriteLine($"    age  = {age}");

        // HMGET 批量获取多个 field
        var fields = await db.HashGetAsync(userKey, new RedisValue[] { "name", "city", "title" });
        Console.WriteLine();
        Console.WriteLine("  HMGET 批量读取多个字段:");
        Console.WriteLine($"    name={fields[0]}, city={fields[1]}, title={fields[2]}");

        // HGETALL 获取全部
        var allFields = await db.HashGetAllAsync(userKey);
        Console.WriteLine();
        Console.WriteLine("  HGETALL 获取全部字段:");
        foreach (var entry in allFields)
            Console.WriteLine($"    {entry.Name} = {entry.Value}");

        Console.WriteLine();

        // ── 场景 B：购物车管理 ──
        Console.WriteLine("┌─ 场景B：购物车管理 ───────────────────────┐");
        Console.WriteLine();

        var cartKey = "cart:1001";
        await db.KeyDeleteAsync(cartKey);

        // 用户将商品加入购物车
        Console.WriteLine("  [操作] 将商品加入购物车:");
        Console.WriteLine("    HSET cart:1001 apple 2");
        Console.WriteLine("    HSET cart:1001 banana 3");
        Console.WriteLine("    HSET cart:1001 milk 1");
        Console.WriteLine("    HSET cart:1001 bread 2");
        Console.WriteLine();

        await db.HashSetAsync(cartKey, new HashEntry[]
        {
            new("apple", "2"),
            new("banana", "3"),
            new("milk", "1"),
            new("bread", "2"),
        });

        var cartCount = await db.HashLengthAsync(cartKey);
        Console.WriteLine($"  HLEN 购物车商品种类: {cartCount}");

        // 修改商品数量（原子递增）
        await db.HashIncrementAsync(cartKey, "apple", 1);
        Console.WriteLine("  HINCRBY cart:1001 apple 1 → 苹果数量+1");

        await db.HashIncrementAsync(cartKey, "milk", 2);
        Console.WriteLine("  HINCRBY cart:1001 milk 2 → 牛奶数量+2");

        var apple = await db.HashGetAsync(cartKey, "apple");
        var milk = await db.HashGetAsync(cartKey, "milk");
        Console.WriteLine($"  当前: apple={apple}, milk={milk}");

        // 删除商品
        await db.HashDeleteAsync(cartKey, "bread");
        Console.WriteLine("  HDEL cart:1001 bread → 面包已移除");

        // 检查商品是否存在
        var hasBread = await db.HashExistsAsync(cartKey, "bread");
        var hasApple = await db.HashExistsAsync(cartKey, "apple");
        Console.WriteLine($"  HEXISTS: bread={hasBread}, apple={hasApple}");

        Console.WriteLine();

        // 列出购物车所有内容
        var cartItems = await db.HashGetAllAsync(cartKey);
        Console.WriteLine("  ── 购物车最终内容 ──");
        foreach (var item in cartItems)
            Console.WriteLine($"    {item.Name} x {item.Value}");

        Console.WriteLine();

        // ── 场景 C：文章详情缓存 + 阅读计数 ──
        Console.WriteLine("┌─ 场景C：文章缓存 + 阅读计数 ─────────────┐");
        Console.WriteLine();

        var articleKey = "article:3001";

        // HSETNX: 仅在 field 不存在时设置（防止覆盖已有数据）
        var setTitle = await db.HashSetAsync(articleKey, "title", "分布式系统设计实践", When.NotExists);
        var setAuthor = await db.HashSetAsync(articleKey, "author", "王工", When.NotExists);
        var setViews = await db.HashSetAsync(articleKey, "views", "0", When.NotExists);

        Console.WriteLine("  HSETNX（仅不存在时设值）:");
        Console.WriteLine($"    设置标题: {(setTitle ? "成功" : "已存在，跳过")}");
        Console.WriteLine($"    设置作者: {(setAuthor ? "成功" : "已存在，跳过")}");
        Console.WriteLine($"    初始化阅读量: {(setViews ? "成功" : "已存在，跳过")}");

        // 再次尝试设置标题（此时已存在，应该跳过）
        var setTitleAgain = await db.HashSetAsync(articleKey, "title", "新标题-不会被写入", When.NotExists);
        Console.WriteLine($"    重复设置标题: {(setTitleAgain ? "写入" : "已存在，跳过")}");

        Console.WriteLine();

        // HINCRBY 原子增加阅读量
        Console.WriteLine("  模拟 20 个用户并发阅读（HINCRBY）:");
        var viewTasks = new List<Task>();
        for (int i = 0; i < 20; i++)
            viewTasks.Add(db.HashIncrementAsync(articleKey, "views"));

        await Task.WhenAll(viewTasks);

        var views = await db.HashGetAsync(articleKey, "views");
        Console.WriteLine($"  最终阅读量: {views}（期待 20，原子操作无丢失）");

        // 获取文章所有元数据
        var articleFields = await db.HashGetAllAsync(articleKey);
        Console.WriteLine();
        Console.WriteLine("  HGETALL 查看文章全部信息:");
        foreach (var f in articleFields)
            Console.WriteLine($"    {f.Name} = {f.Value}");

        Console.WriteLine();

        // ── 场景 D：Hash vs String 存储对比 ──
        Console.WriteLine("┌─ 场景D：Hash vs String 存储对比 ──────────┐");
        Console.WriteLine();

        var userStringKey = "user:3001:string";

        // String 方式：需要序列化整个对象
        var userObj = new { Name = "赵六", Age = 32, City = "上海", Title = "架构师", Score = 15000 };
        var jsonString = System.Text.Json.JsonSerializer.Serialize(userObj);

        await db.StringSetAsync(userStringKey, jsonString);

        // 模拟只修改一个字段 —— 需要全部读出再写回
        var raw = await db.StringGetAsync(userStringKey);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(raw!);
        deserialized!["Score"] = 16000;
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(deserialized);
        await db.StringSetAsync(userStringKey, updatedJson);

        Console.WriteLine("  String 方式（JSON 序列化）:");
        Console.WriteLine("    修改 Score 字段 → 需全量读写: GET → 反序列化 → 修改 → 序列化 → SET");
        Console.WriteLine($"    JSON 体积: {jsonString.Length} 字节");

        Console.WriteLine();

        // Hash 方式：只改一个 field
        var userHashKey = "user:3001:hash";
        await db.HashSetAsync(userHashKey, new HashEntry[]
        {
            new("name", "赵六"),
            new("age", "32"),
            new("city", "上海"),
            new("title", "架构师"),
            new("score", "15000"),
        });

        await db.HashIncrementAsync(userHashKey, "score", 1000);
        Console.WriteLine("  Hash 方式（field 映射）:");
        Console.WriteLine("    修改 Score 字段 → 只需: HINCRBY user:3001:hash score 1000");
        Console.WriteLine("    优势: 无需传输全量数据，IO 更少，代码更简洁");

        var score = await db.HashGetAsync(userHashKey, "score");
        Console.WriteLine($"    修改后 score = {score}");
        Console.WriteLine();

        // ── 内存占用概览 ──
        Console.WriteLine("  ── 内存效率总结 ──");
        Console.WriteLine("    Redis 对小 Hash（< 512 fields）采用 ziplist 编码，极省内存");
        Console.WriteLine("    而 String 方式每次修改都要传输全量 JSON，网络开销更大");
        Console.WriteLine();

        Console.WriteLine();
        Console.WriteLine("✓ Hash 类型演示完成。总结:");
        Console.WriteLine("  • HSET/HGET/HGETALL 实现对象属性级读写");
        Console.WriteLine("  • HINCRBY 原子操作单个 field（无需整对象锁定）");
        Console.WriteLine("  • HSETNX 用于分布式场景下初始化防覆盖");
        Console.WriteLine("  • HDEL + HEXISTS 增删查改完整支持");
        Console.WriteLine("  • Key 格式: user:{id} / cart:{uid} / article:{id}");
        Console.WriteLine("  • 适用: 用户资料、商品信息、配置项、购物车");
        Console.WriteLine();
    }
}
