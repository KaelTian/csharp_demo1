// ============================================================
// 核心知识点：LINQ 深度解析
// ============================================================
// 核心问题：
// 1. IEnumerable<T> vs IQueryable<T> 区别？
// 2. 延迟执行 vs 立即执行？
// 3. LINQ 的常见性能陷阱？
// 4. Select/SelectMany、GroupBy、Join 如何用？
// 5. LINQ 内部实现机制？
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class LinqDeepDiveDemo
{
    public static void Run()
    {
        Console.WriteLine("═══ LINQ 深度解析 ═══\n");

        var data = Enumerable.Range(1, 100).ToList();

        // === 1. 延迟执行 vs 立即执行 ===
        Console.WriteLine("--- 1. 延迟执行 (Deferred Execution) ---");

        // Where/Select/OrderBy 都是延迟执行
        var query = data.Where(x =>
        {
            Console.Write("."); // 只有遍历时才执行
            return x > 95;
        });
        Console.WriteLine("  查询已定义但未执行（没有输出点号）");

        Console.Write("  开始遍历: ");
        foreach (var item in query) { } // 实际执行在这里
        Console.WriteLine("\n");

        // 立即执行：ToList()、ToArray()、Count()、First()、Any() 等
        Console.WriteLine("--- 立即执行 (Immediate Execution) ---");
        var immediate = data.Where(x => x > 98).ToList(); // 立即执行
        Console.WriteLine($"  immediate.Count = {immediate.Count}\n");

        // === 2. 多次枚举陷阱 ===
        Console.WriteLine("--- 2. 多次枚举陷阱 ---");

        var expensive = GetExpensiveSequence(); // IEnumerable，每次枚举都重新计算
        var count1 = expensive.Count();  // 执行一次
        var count2 = expensive.Count();  // 再执行一次！开销翻倍
        Console.WriteLine($"  多次枚举: count1={count1}, count2={count2}\n");

        // ✅ 正确做法：ToList() 缓存结果
        var cached = GetExpensiveSequence().ToList();

        // === 3. Select 和 SelectMany ===
        Console.WriteLine("--- 3. Select vs SelectMany ---");

        var students = GetStudents();
        // Select: 一个输入 → 一个输出
        var names = students.Select(s => s.Name);
        Console.WriteLine($"  Select (学生名): {string.Join(", ", names)}");

        // SelectMany: 一个输入 → 多个输出（展平）
        var allScores = students.SelectMany(s => s.Scores);
        Console.WriteLine($"  SelectMany (所有分数): {string.Join(", ", allScores)}");

        // SelectMany 重载：带索引器
        var scoreWithIndex = students.SelectMany(
            (s, idx) => s.Scores.Select(score => $"学生{idx}:{score}")
        );
        Console.WriteLine($"  SelectMany with index: {string.Join(", ", scoreWithIndex)}\n");

        // === 4. GroupBy 分组 ===
        Console.WriteLine("--- 4. GroupBy 分组 ---");

        var groupByGrade = students.GroupBy(s => s.Grade);
        foreach (var group in groupByGrade)
        {
            Console.WriteLine($"  {group.Key} 年级: {group.Count()} 人, 平均分 {group.Average(s => s.Scores.Average()):F1}");
        }
        Console.WriteLine();

        // === 5. Join 和 GroupJoin ===
        Console.WriteLine("--- 5. Join / GroupJoin ---");

        var courses = new[]
        {
            new Course("C1", "语文"),
            new Course("C2", "数学"),
            new Course("C3", "英语"),
        };

        var enrollments = new[]
        {
            new Enrollment("Alice", "C1"),
            new Enrollment("Alice", "C2"),
            new Enrollment("Bob", "C1"),
            new Enrollment("Charlie", "C3"),
        };

        // Inner Join
        var query5 = from e in enrollments
                     join c in courses on e.CourseId equals c.Id
                     select $"{e.StudentName} → {c.Name}";

        Console.WriteLine("  Inner Join:");
        foreach (var item in query5) Console.WriteLine($"    {item}");

        // GroupJoin (左连接)
        var groupJoin = courses.GroupJoin(
            enrollments,
            c => c.Id,
            e => e.CourseId,
            (course, enrolledStudents) => $"{course.Name}: {string.Join(", ", enrolledStudents.Select(e => e.StudentName).DefaultIfEmpty("(无学生)"))}"
        );

        Console.WriteLine("  GroupJoin (左连接):");
        foreach (var item in groupJoin) Console.WriteLine($"    {item}");
        Console.WriteLine();

        // === 6. Aggregate 聚合 ===
        Console.WriteLine("--- 6. Aggregate 高级聚合 ---");

        var numbers = new[] { 1, 2, 3, 4, 5 };
        var sum = numbers.Aggregate((acc, n) => acc + n); // 1+2+3+4+5 = 15
        var product = numbers.Aggregate((acc, n) => acc * n); // 120
        Console.WriteLine($"  Aggregate Sum: {sum}, Product: {product}");

        // 带初始值的 Aggregate
        var result = numbers.Aggregate("初始", (acc, n) => $"{acc}+{n}");
        Console.WriteLine($"  Aggregate with seed: {result}\n");

        // === 7. IEnumerable vs IQueryable ===
        Console.WriteLine("--- 7. IEnumerable vs IQueryable ---");
        Console.WriteLine("  IEnumerable: 在客户端内存中执行所有操作");
        Console.WriteLine("  IQueryable:  构建表达式树，由提供者解析（如 EF Core → SQL）");
        Console.WriteLine("  ⚠️  错用场景: 用 IEnumerable 做 Where 过滤 = 全表加载到内存");
        Console.WriteLine("  ✅  IQueryable 的 Where 会生成 SQL WHERE 子句\n");

        // === 8. LINQ 方法链 vs 查询语法 ===
        Console.WriteLine("--- 8. 方法链 vs 查询语法 ---");

        // 方法链（Fluent）
        var fluent = data.Where(x => x % 2 == 0)
                         .OrderByDescending(x => x)
                         .Take(5)
                         .ToList();

        // 查询语法（SQL-like）
        var querySyntax = (from x in data
                           where x % 2 == 0
                           orderby x descending
                           select x).Take(5).ToList();

        Console.WriteLine($"  方法链:   {string.Join(", ", fluent)}");
        Console.WriteLine($"  查询语法: {string.Join(", ", querySyntax)}");
        Console.WriteLine("  ✅ 建议: 简单操作用方法链，复杂查询用查询语法");
    }

    // === 数据模型 ===

    private static IEnumerable<int> GetExpensiveSequence()
    {
        Console.WriteLine("  [执行昂贵查询：数据库查询/文件读取]");
        for (int i = 0; i < 5; i++)
            yield return i;
    }

    private static Student[] GetStudents() => new[]
    {
        new Student("Alice", "A", new[] { 85, 90, 92 }),
        new Student("Bob", "A", new[] { 70, 75, 80 }),
        new Student("Charlie", "B", new[] { 88, 92 }),
        new Student("Diana", "B", new[] { 95, 98 }),
        new Student("Eve", "A", new[] { 60, 65 }),
    };

    public record Student(string Name, string Grade, int[] Scores);
    public record Course(string Id, string Name);
    public record Enrollment(string StudentName, string CourseId);
}
