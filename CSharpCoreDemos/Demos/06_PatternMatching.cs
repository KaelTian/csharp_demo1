// ============================================================
// 核心知识点：模式匹配 (Pattern Matching)
// ============================================================
// 核心问题：
// 1. switch 表达式 vs switch 语句？
// 2. 属性模式、位置模式、列表模式？
// 3. 丢弃模式 _ 的用法？
// 4. 关系模式 (< >)？
// 5. 模式匹配在业务中的实际应用？
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class PatternMatchingDemo
{
    public static void Run()
    {
        Console.WriteLine("═══ 模式匹配 ═══\n");

        // === 1. 类型模式 + 声明模式 ===
        Console.WriteLine("--- 1. 类型模式 ---");

        object?[] items = { "Hello", 42, 3.14, null, new { X = 1 }, new List<int> { 1, 2, 3 } };

        foreach (var item in items)
        {
            string desc = item switch
            {
                string s => $"字符串: \"{s}\" (长度={s.Length})",
                int i => $"整数: {i} (平方={i * i})",
                double d => $"浮点数: {d:F2}",
                null => "null 值",
                _ => $"其他类型: {item.GetType().Name}" // 默认匹配
            };
            Console.WriteLine($"  {desc}");
        }
        Console.WriteLine();

        // === 2. 属性模式 ===
        Console.WriteLine("--- 2. 属性模式 ---");

        var users = new[]
        {
            new User("Alice", "admin", 30),
            new User("Bob", "user", 17),
            new User("Charlie", "vip", 25),
            new User("Diana", "user", 65),
        };

        foreach (var user in users)
        {
            var accessLevel = user switch
            {
                { Role: "admin" } => "🔑 完全访问",
                { Role: "vip", Age: >= 18 } => "⭐ VIP 成年用户",
                { Role: "vip" } => "⭐ VIP 未成年用户（受限）",
                { Age: >= 60 } => "👴 老年用户（特殊关怀）",
                { Age: < 18 } => "🔒 未成年用户（限制）",
                _ => "👤 普通用户"
            };
            Console.WriteLine($"  {user.Name}: {accessLevel}");
        }
        Console.WriteLine();

        // === 3. 位置模式 + 解构 ===
        Console.WriteLine("--- 3. 位置模式 ---");

        var points = new[]
        {
            new Point(0, 0),
            new Point(1, 1),
            new Point(5, 0),
            new Point(0, 3),
            new Point(-1, -1),
        };

        foreach (var p in points)
        {
            var location = p switch
            {
                (0, 0) => "原点",
                (var x, 0) => $"X 轴上 (x={x})",
                (0, var y) => $"Y 轴上 (y={y})",
                ( > 0, > 0) => $"第一象限 ({p.X},{p.Y})",
                (< 0, > 0) => $"第二象限 ({p.X},{p.Y})",
                (< 0, < 0) => $"第三象限 ({p.X},{p.Y})",
                ( > 0, < 0) => $"第四象限 ({p.X},{p.Y})",
                _ => $"未知位置"
            };
            Console.WriteLine($"  Point({p.X},{p.Y}): {location}");
        }
        Console.WriteLine();

        // === 4. 关系模式 ===
        Console.WriteLine("--- 4. 关系模式 (< > <= >=) ---");

        double TestScore(double score) => score switch
        {
            < 0 or > 100 => throw new ArgumentOutOfRangeException(nameof(score)),
            >= 90 => 4.0,
            >= 80 => 3.0,
            >= 70 => 2.0,
            >= 60 => 1.0,
            _ => 0.0
        };

        var scores = new[] { 95, 83, 67, 45, 100, 0 };
        foreach (var s in scores)
        {
            Console.WriteLine($"  分数 {s:F0} → GPA {TestScore(s):F1}");
        }
        Console.WriteLine();

        // === 5. 逻辑模式 (and / or / not) ===
        Console.WriteLine("--- 5. 逻辑模式 (and / or / not) ---");

        string Classify(int age) => age switch
        {
            < 0 or > 150 => "非法年龄",
            >= 0 and < 12 => "儿童",
            >= 12 and < 18 => "青少年",
            >= 18 and < 60 => "成年",
            >= 60 => "老年"
        };

        var ages = new[] { -1, 5, 15, 25, 65, 200 };
        foreach (var a in ages) Console.WriteLine($"  {a}岁 → {Classify(a)}");
        Console.WriteLine();

        // === 6. 列表模式 (C# 11) ===
        Console.WriteLine("--- 6. 列表模式 ---");

        int[] MatchArray(int[] arr) => arr switch
        {
            [] => [0],                          // 空数组
            [var first] => [first * 2],          // 单个元素
            [var first, .. var rest] => [first, .. rest] // 一个 + 剩余
        };

        Console.WriteLine($"  [] → {string.Join(",", MatchArray([]))}");
        Console.WriteLine($"  [5] → {string.Join(",", MatchArray([5]))}");
        Console.WriteLine($"  [1,2,3] → {string.Join(",", MatchArray([1, 2, 3]))}");

        // 列表模式匹配特定模式
        int[] CheckPattern(int[] arr) => arr switch
        {
            [1, 2, _] => [100],                  // 前两个是 1,2
            [.., 9] => [999],                     // 最后一个是 9
            [var a, var b, .., var c] => [a, b, c], // 第一个、第二个、最后一个
            _ => [0]
        };

        Console.WriteLine($"  [1,2,3] → {string.Join(",", CheckPattern([1, 2, 3]))}");
        Console.WriteLine($"  [1,2,3,9] → {string.Join(",", CheckPattern([1, 2, 3, 9]))}");
        Console.WriteLine($"  [4,5,6,7] → {string.Join(",", CheckPattern([4, 5, 6, 7]))}");
        Console.WriteLine();

        // === 7. 模式匹配实际业务应用 ===
        Console.WriteLine("--- 7. 实际业务：策略分发 ---");

        var orders = new Order[]
        {
            new("正常订单", 500, "normal", false),
            new("VIP订单", 2000, "vip", false),
            new("促销订单", 300, "normal", true),
            new("大额订单", 10000, "vip", false),
        };

        foreach (var order in orders)
        {
            var (discount, needApproval) = CalculateDiscount(order);
            Console.WriteLine($"  {order.Name}: 折扣={discount}%, 需审批={needApproval}");
        }
    }

    // 业务逻辑：模式匹配用来做策略分发
    static (double discount, bool needApproval) CalculateDiscount(Order order) => order switch
    {
        { Amount: >= 10000 } => (15, true),       // 大额订单需审批
        { IsPromotion: true } => (20, false),      // 促销品
        { Role: "vip", Amount: >= 1000 } => (10, false),
        { Role: "vip" } => (5, false),
        { Amount: >= 500 } => (3, false),
        _ => (0, false)
    };
}

// === 数据模型 ===

public record User(string Name, string Role, int Age);
public record Point(int X, int Y);
public record Order(string Name, decimal Amount, string Role, bool IsPromotion);
