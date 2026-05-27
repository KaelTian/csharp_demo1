// ============================================================
// 核心知识点：字符串、Span<T>、性能优化、异常处理
// ============================================================
// 核心问题：
// 1. string 是不可变的，拼接用 StringBuilder？
// 2. 字符串驻留 (String Interning)？
// 3. Span<T> 和 Memory<T> 是什么？
// 4. 自定义异常的最佳实践？
// 5. Exception filter (when) 的用法？
// 6. throw 和 throw ex 的区别？
// ============================================================

using System.Buffers;
using System.Text;

namespace CSharpCoreDemos.Demos;

public static class StringAndExceptionDemo
{
    public static void Run()
    {
        Console.WriteLine("═══ 字符串、Span 与异常处理 ═══\n");

        // === 1. 字符串不可变性和 StringBuilder ===
        Console.WriteLine("--- 1. 字符串拼接性能 ---");

        const int n = 10_000;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ❌ 错误：循环中字符串拼接
        string bad = "";
        for (int i = 0; i < n; i++)
            bad += i.ToString();

        sw.Stop();
        Console.WriteLine($"  string += ×{n}: {sw.ElapsedMilliseconds}ms (每次创建新对象)");

        // ✅ 正确：使用 StringBuilder
        sw.Restart();
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
            sb.Append(i);

        sw.Stop();
        Console.WriteLine($"  StringBuilder ×{n}: {sw.ElapsedMilliseconds}ms\n");

        // === 2. 字符串驻留 ===
        Console.WriteLine("--- 2. 字符串驻留 (String Interning) ---");

        // 编译期常量的字符串会自动驻留
        string a = "Hello";
        string b = "Hello";
        Console.WriteLine($"  字符串常量引用相等: {ReferenceEquals(a, b)}"); // True

        // 运行时创建的字符串不会自动驻留
        string c = "Hel";
        string d = "lo";
        string e = c + d; // 运行时拼接，不驻留
        Console.WriteLine($"  运行时字符串引用相等: {ReferenceEquals(a, e)}"); // False
        Console.WriteLine($"  但值相等: {a == e}"); // True

        // 手动驻留
        string f = string.Intern(e);
        Console.WriteLine($"  手动驻留后引用相等: {ReferenceEquals(a, f)}"); // True
        Console.WriteLine("  ⚠️  Intern 需谨慎使用，驻留的字符串不会被 GC\n");

        // === 3. Span<T> 和 Memory<T> ===
        Console.WriteLine("--- 3. Span<T> 和 Memory<T> ---");
        Console.WriteLine("  Span<T>: 栈上分配，可访问连续内存（数组/字符串/栈内存）");
        Console.WriteLine("  Memory<T>: 堆上分配，可在 async 中使用");
        Console.WriteLine("  ✅ 零拷贝切片！\n");

        // Span 切片
        int[] numbers = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        Span<int> whole = numbers;

        // 不复制数据，直接创建视图
        Span<int> slice = whole[2..5]; // [2, 3, 4]
        Console.WriteLine($"  Span 切片: [{string.Join(", ", slice.ToArray())}]");

        // 修改原数组会影响切片（共享内存）
        numbers[3] = 100;
        Console.WriteLine($"  修改原数组后切片: [{string.Join(", ", slice.ToArray())}]");
        Console.WriteLine();

        // String切片 (无需分配新字符串)
        Console.WriteLine("--- 字符串的 Span 切片 ---");

        string text = "Hello, World!";
        ReadOnlySpan<char> span = text.AsSpan();
        var subSpan = span[7..12]; // "World"
        Console.WriteLine($"  Span 子串: '{new string(subSpan)}' (零分配！)\n");

        // === 4. StringBuilder 高级用法 ===
        Console.WriteLine("--- 4. StringBuilder 高级用法 ---");

        var sb2 = new StringBuilder();
        sb2.Append("姓名: ").AppendLine("Alice");
        sb2.Append("年龄: ").AppendLine("30");
        sb2.AppendFormat("余额: {0:C}", 99.99m);

        // 池化 StringBuilder
        var sbPool = ArrayPool<StringBuilder>.Shared.Rent(1)[0] ?? new StringBuilder();
        sbPool.Append("池化 StringBuilder");
        var poolResult = sbPool.ToString();
        sbPool.Clear();
        // 归还池 (可选)
        Console.WriteLine($"  StringBuilder 高级: {sb2}");
        Console.WriteLine();

        // === 5. 自定义异常 ===
        Console.WriteLine("--- 5. 自定义异常 ---");

        try
        {
            ProcessOrder(-1);
        }
        catch (OrderException ex) when (ex.ErrorCode == "INVALID_AMOUNT")
        {
            // Exception filter: 仅匹配特定条件
            Console.WriteLine($"  Filter 捕获: {ex.Message} (Code: {ex.ErrorCode})");
        }
        catch (OrderException ex)
        {
            Console.WriteLine($"  其他 OrderException: {ex.Message}");
        }
        Console.WriteLine();

        // === 6. throw vs throw ex ===
        Console.WriteLine("--- 6. throw 和 throw ex ---");

        try
        {
            MethodThatThrows();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  throw (保留堆栈): {ex.StackTrace?.Split('\n')[0]}");
        }

        try
        {
            MethodThatThrowsEx();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  throw ex (重置堆栈): {ex.StackTrace?.Split('\n')[0]}");
        }
        Console.WriteLine("  ✅ 永远用 throw，不要用 throw ex\n");

        // === 7. 异常筛选器 (when) ===
        Console.WriteLine("--- 7. 异常筛选器实战 ---");

        try
        {
            throw new HttpRequestException($"HTTP Error", null, System.Net.HttpStatusCode.NotFound);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine("  404: 资源不存在，不记录错误");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            Console.WriteLine("  500: 服务器错误，记录告警");
        }
        Console.WriteLine();

        // === 8. 值类型使用 Span 优化 ===
        Console.WriteLine("--- 8. ArrayPool 缓冲池 ---");

        sw.Restart();
        ProcessWithArrayPool(100_000);
        sw.Stop();
        Console.WriteLine($"  使用 ArrayPool: {sw.ElapsedMilliseconds}ms");
    }

    static void ProcessOrder(decimal amount)
    {
        if (amount < 0)
            throw new OrderException("INVALID_AMOUNT", "订单金额不能为负数", "请检查金额");
        if (amount > 100000)
            throw new OrderException("EXCEED_LIMIT", "超出限额", "请联系客服");
    }

    static void MethodThatThrows()
    {
        try
        {
            throw new InvalidOperationException("测试异常");
        }
        catch
        {
            throw; // ✅ 保留原始堆栈
        }
    }

    static void MethodThatThrowsEx()
    {
        try
        {
            throw new InvalidOperationException("测试异常");
        }
        catch (Exception ex)
        {
            throw ex; // ❌ 重置堆栈到此处
        }
    }

    static void ProcessWithArrayPool(int size)
    {
        // 从池中租用数组，避免频繁分配
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            for (int i = 0; i < size; i++)
                buffer[i] = (byte)(i % 256);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

// === 自定义异常类 ===

public class OrderException : Exception
{
    public string ErrorCode { get; }
    public string Suggestion { get; }

    // 自定义异常的规范：
    // 1. 继承 Exception
    // 2. 实现所有三个标准构造函数
    // 3. 添加额外的属性
    // 4. 标记为 [Serializable] (如果需要跨 AppDomain)
    public OrderException(string errorCode, string message, string suggestion)
        : base(message)
    {
        ErrorCode = errorCode;
        Suggestion = suggestion;
    }

    public OrderException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        Suggestion = "";
    }

    public OrderException() : base() { ErrorCode = "UNKNOWN"; Suggestion = ""; }
    public OrderException(string message) : base(message) { ErrorCode = "UNKNOWN"; Suggestion = ""; }
    public OrderException(string message, Exception inner) : base(message, inner) { ErrorCode = "UNKNOWN"; Suggestion = ""; }
}
