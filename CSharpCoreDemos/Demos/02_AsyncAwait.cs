// ============================================================
// 核心知识点：async/await、Task、异步编程
// ============================================================
// 核心问题：
// 1. async/await 的本质是什么？（状态机）
// 2. Task vs ValueTask 区别？
// 3. ConfigureAwait(false) 有什么用？
// 4. async void 为什么危险？
// 5. Task.WhenAll / WhenAny 用法
// 6. CancellationToken 取消模式
// 7. IAsyncEnumerable 流式处理
// 8. 常见的死锁场景
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class AsyncAwaitDemo
{
    public static async Task Run()
    {
        Console.WriteLine("═══ async/await 与 Task ═══\n");

        // === 1. 基础 async/await ===
        Console.WriteLine("--- 1. 基础 async/await ---");
        await BasicAsync();

        // === 2. Task.WhenAll / WhenAny ===
        Console.WriteLine("\n--- 2. 并发控制 ---");

        // WhenAll: 等待所有任务完成
        var tasks = new[] { DelayAndReturn(1, "A"), DelayAndReturn(2, "B"), DelayAndReturn(1.5, "C") };
        var results = await Task.WhenAll(tasks);
        Console.WriteLine($"WhenAll 结果: {string.Join(", ", results)}");

        // WhenAny: 只要一个完成就继续
        var first = await Task.WhenAny(tasks);
        Console.WriteLine($"WhenAny 首先完成: {first.Result}");

        // === 3. 错误处理 ===
        Console.WriteLine("\n--- 3. 异步错误处理 ---");

        try
        {
            await FaultyTask();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"捕获异常: {ex.Message}");
        }

        // WhenAll 中的多个异常会聚合为 AggregateException
        try
        {
            var errorTasks = new[] { FailTask("X"), FailTask("Y") };
            await Task.WhenAll(errorTasks);
        }
        catch (AggregateException ae) // 需要显式等待 Task 才会抛 AggregateException
        {
            Console.WriteLine($"AggregateException: {ae.InnerExceptions.Count} 个异常");
        }
        catch (Exception ex) // await 会解包第一个异常
        {
            Console.WriteLine($"await 解包后: {ex.Message}");
        }

        // === 4. CancellationToken 模式 ===
        Console.WriteLine("\n--- 4. 取消模式 ---");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        try
        {
            await CancellableWork(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("任务已取消 (OperationCanceledException)");
        }

        // === 5. ConfigureAwait(false) ===
        Console.WriteLine("\n--- 5. ConfigureAwait(false) ---");
        Console.WriteLine("  ConfigureAwait(false) 告诉 await 不需要回到原 SynchronizationContext");
        Console.WriteLine("  在类库代码中推荐使用，避免死锁；在 UI 代码中需要恢复上下文时才用 true\n");

        await ConfigureAwaitExample();

        // === 6. IAsyncEnumerable 流式处理 ===
        Console.WriteLine("\n--- 6. IAsyncEnumerable 流式异步迭代 ---");

        await foreach (var item in GenerateSequence())
        {
            Console.WriteLine($"  收到: {item}");
        }

        // === 7. async void 的危险 ===
        Console.WriteLine("\n--- 7. async void 的危险 ---");

        // 异步事件处理器必须用 async void
        // 但普通方法绝对不要用 async void！
        // 下面的调用不会被 await，异常会直接崩溃进程
        Console.WriteLine("  启动 FireAndForget (async void)...");
        try
        {
            // 这将无法被 await，异常无法被捕获
            // FireAndForget(); // 取消注释会导致进程崩溃
        }
        catch { }

        // ✅ 正确做法：返回 Task
        Console.WriteLine("  启动 AsyncTask (async Task)...");
        try
        {
            await SafeFireAndForget();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  安全捕获: {ex.Message}");
        }

        Console.WriteLine("\n  ⚠️  async void 仅用于事件处理器，其他场景一律用 async Task");
    }

    // === Demo 实现 ===

    private static async Task BasicAsync()
    {
        // await 会释放线程，不像 .Result 会阻塞
        var result = await Task.Run(() =>
        {
            Task.Delay(50).Wait(); // 模拟工作
            return 42;
        });
        Console.WriteLine($"  Basic async result: {result}");
    }

    private static async Task<string> DelayAndReturn(double seconds, string name)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        return $"{name}({seconds}s)";
    }

    private static async Task FaultyTask()
    {
        await Task.Delay(10);
        throw new InvalidOperationException("模拟异常");
    }

    private static async Task<string> FailTask(string name)
    {
        await Task.Delay(10);
        throw new InvalidOperationException($"失败: {name}");
    }

    private static async Task CancellableWork(CancellationToken ct)
    {
        for (int i = 0; i < 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(50, ct);
        }
    }

    private static async Task ConfigureAwaitExample()
    {
        // 在类库中，ConfigureAwait(false) 避免死锁
        await Task.Delay(10).ConfigureAwait(false);
        // 这里不会再回到原始上下文
        Console.WriteLine("  ConfigureAwait(false) 执行完毕");
    }

    private static async IAsyncEnumerable<int> GenerateSequence()
    {
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(50);
            yield return i;
        }
    }

    private static async void FireAndForget() // ⚠️ 危险！异常无法被捕获
    {
        await Task.Delay(10);
        throw new InvalidOperationException("async void 异常 → 进程崩溃！");
    }

    private static async Task SafeFireAndForget()
    {
        await Task.Delay(10);
        throw new InvalidOperationException("async Task 异常 → 可以被 await 捕获");
    }
}
