// ============================================================
// C# 核心知识点 Demo - 主菜单
// ============================================================

using CSharpCoreDemos.Demos;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "C# 核心知识点 Demo";

var demos = new Dictionary<string, Func<Task>>
{
    ["1"] = () => { DelegatesAndEventsDemo.Run(); return Task.CompletedTask; },
    ["2"] = () => AsyncAwaitDemo.Run(),
    ["3"] = () => { LinqDeepDiveDemo.Run(); return Task.CompletedTask; },
    ["4"] = RunGenerics,
    ["5"] = () => { RecordsAndImmutabilityDemo.Run(); return Task.CompletedTask; },
    ["6"] = () => { PatternMatchingDemo.Run(); return Task.CompletedTask; },
    ["7"] = () => ConcurrencyDemo.Run(),
    ["8"] = () => { ReflectionDemo.Run(); return Task.CompletedTask; },
    ["9"] = () => { StringAndExceptionDemo.Run(); return Task.CompletedTask; },
};

while (true)
{
    SafeClear();
    PrintMenu();

    var input = SafeReadKey();
    if (input == "q") break;

    if (demos.TryGetValue(input, out var demo))
    {
        SafeClear();
        try
        {
            await demo();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n⚠️ Demo 运行异常: {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("\n── 按任意键返回菜单 ──");
        SafeWaitKey();
    }
}

// ============================================================
// 兼容 VS Code 调试控制台（不支持 ReadKey/Clear 时自动降级）
// ============================================================

static void SafeClear()
{
    try { Console.Clear(); }
    catch { /* VS Code 调试控制台不支持 Clear，忽略 */ }
}

static string SafeReadKey()
{
    try
    {
        var key = Console.ReadKey(intercept: true);
        return key.KeyChar.ToString().ToLower();
    }
    catch
    {
        // VS Code 调试控制台不支持 ReadKey，降级为 ReadLine
        Console.WriteLine();
        return Console.ReadLine()?.Trim().ToLower() ?? "q";
    }
}

static void SafeWaitKey()
{
    try { Console.ReadKey(intercept: true); }
    catch
    {
        // VS Code 调试控制台不支持 ReadKey，降级为 ReadLine
        Console.ReadLine();
    }
}

static async Task RunGenerics()
{
    GenericsAndVarianceDemo.Run();
    await Task.CompletedTask;
}

static void PrintMenu()
{
    Console.WriteLine("╔══════════════════════════════════════════╗");
    Console.WriteLine("║   C# 核心知识点 Demo                 ║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("  ┌──────┬────────────────────────────────┐");
    Console.WriteLine("  │ 编号 │ 主题                           │");
    Console.WriteLine("  ├──────┼────────────────────────────────┤");
    Console.WriteLine("  │  1   │ 委托、事件、Lambda、闭包       │");
    Console.WriteLine("  │  2   │ async/await、Task、异步编程    │");
    Console.WriteLine("  │  3   │ LINQ 深度解析                  │");
    Console.WriteLine("  │  4   │ 泛型、协变、逆变、约束         │");
    Console.WriteLine("  │  5   │ Record、不可变性、值相等性     │");
    Console.WriteLine("  │  6   │ 模式匹配 (Pattern Matching)    │");
    Console.WriteLine("  │  7   │ Dispose 模式、并发与线程安全   │");
    Console.WriteLine("  │  8   │ 反射、特性 (Attribute)         │");
    Console.WriteLine("  │  9   │ 字符串、Span、异常处理         │");
    Console.WriteLine("  └──────┴────────────────────────────────┘");
    Console.WriteLine();
    Console.Write("  选择 Demo (1-9) 或 Q 退出: ");
}
