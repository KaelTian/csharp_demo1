// ============================================================
// 核心知识点：委托、事件、Lambda 与闭包
// ============================================================
// 核心问题：
// 1. delegate 和 event 的区别是什么？
// 2. Action/Func 与自定义 delegate 的选择？
// 3. Lambda 闭包捕获变量的陷阱？
// 4. 什么是 multicast delegate？
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class DelegatesAndEventsDemo
{
    // 自定义委托声明
    public delegate void LogHandler(string message);

    // 发布者类
    public class Logger
    {
        // event 关键字添加了类型安全保护：
        // 外部只能 += / -=，不能 = null 或 Invoke
        public event LogHandler? OnLog;

        // 对比：普通委托字段 - 外部可以随意覆盖
        public LogHandler? OnLogDirect;

        public void Log(string message)
        {
            // 安全触发模式（线程安全拷贝）
            var handler = OnLog;
            if (handler != null)
            {
                handler($"[EVENT] {message}");
            }

            OnLogDirect?.Invoke($"[DIRECT] {message}");
        }
    }

    // 现代 C# 使用 EventHandler<T> 泛型模式
    public class ModernPublisher
    {
        // EventHandler<T> 等价于 delegate void Handler(object? sender, T args)
        public event EventHandler<string>? OnMessage;

        public void Publish(string msg)
        {
            OnMessage?.Invoke(this, msg);
        }
    }

    public static void Run()
    {
        Console.WriteLine("═══ 委托与事件 ═══\n");

        // === 1. 委托的基本使用 ===
        Console.WriteLine("--- 1. 委托与方法组转换 ---");

        LogHandler handler1 = ConsoleWriteLine;
        handler1("直接调用委托");

        // 多播委托
        LogHandler multiHandler = ConsoleWriteLine;
        multiHandler += LogToFile;   // 添加第二个方法
        multiHandler += ConsoleWriteLine; // 可以重复添加

        Console.WriteLine("多播委托调用：");
        multiHandler("多播消息");

        // 多播委托的调用列表
        foreach (var del in multiHandler.GetInvocationList())
        {
            Console.WriteLine($"  -> 方法: {del.Method.Name}");
        }

        // === 2. event vs 普通委托字段 ===
        Console.WriteLine("\n--- 2. event 封装性 ---");

        var logger = new Logger();
        logger.OnLog += ConsoleWriteLine;    // OK
        logger.OnLogDirect += ConsoleWriteLine; // OK

        // logger.OnLog = ConsoleWriteLine;      // 编译错误！event 不能赋值
        logger.OnLogDirect = ConsoleWriteLine;  // OK - 普通字段可以覆盖

        logger.Log("测试消息");

        // === 3. Action/Func 内置委托 ===
        Console.WriteLine("\n--- 3. Action / Func / Predicate ---");

        // Action: 无返回值
        Action sayHello = () => Console.WriteLine("Hello!");
        sayHello();

        // Action<T>: 带参数无返回值
        Action<string, int> printRepeated = (s, n) =>
        {
            for (int i = 0; i < n; i++) Console.Write(s);
            Console.WriteLine();
        };
        printRepeated("Ha", 3);

        // Func<TResult>: 有返回值
        Func<int, int, int> add = (a, b) => a + b;
        Console.WriteLine($"Func add: {add(3, 4)}");

        // Predicate<T>: 等价于 Func<T, bool>
        Predicate<int> isPositive = x => x > 0;
        Console.WriteLine($"Predicate: {isPositive(5)}");

        // === 4. 闭包陷阱 ===
        Console.WriteLine("\n--- 4. 闭包陷阱与解决方案 ---");

        // 陷阱：捕获循环变量
        var actions = new List<Action>();
        for (int i = 0; i < 5; i++)
        {
            actions.Add(() => Console.Write(i + " "));
        }

        Console.Write("闭包陷阱: ");
        foreach (var act in actions) act(); // 输出 5 5 5 5 5
        Console.WriteLine();

        // 修正：在循环内创建局部副本
        var actionsFixed = new List<Action>();
        for (int i = 0; i < 5; i++)
        {
            var captured = i; // 局部变量，每次迭代都是新变量
            actionsFixed.Add(() => Console.Write(captured + " "));
        }

        Console.Write("修正后:   ");
        foreach (var act in actionsFixed) act(); // 输出 0 1 2 3 4
        Console.WriteLine();

        // C# 5+ 的 foreach 已修复闭包问题（每次迭代使用独立变量）
        var actionsForeach = new List<Action>();
        foreach (var n in Enumerable.Range(0, 5))
        {
            actionsForeach.Add(() => Console.Write(n + " "));
        }
        Console.Write("foreach:  ");
        foreach (var act in actionsForeach) act();
        Console.WriteLine(" (C# 5+ 默认正确)");

        // === 5. EventHandler<T> 标准模式 ===
        Console.WriteLine("\n--- 5. EventHandler<T> 标准模式 ---");

        var pub = new ModernPublisher();
        pub.OnMessage += (sender, msg) =>
        {
            Console.WriteLine($"收到消息: {msg} (sender={sender?.GetType().Name})");
        };
        pub.Publish("你好，事件！");
    }

    // Helper 方法
    private static void ConsoleWriteLine(string msg)
    {
        Console.WriteLine($"  Console: {msg}");
    }

    private static void LogToFile(string msg)
    {
        // 模拟写文件
        Console.WriteLine($"  File: {msg}");
    }
}
