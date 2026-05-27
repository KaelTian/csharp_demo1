// ============================================================
// 核心知识点：Dispose 模式、并发与线程安全
// ============================================================
// 核心问题：
// 1. IDisposable 的正确实现模式？
// 2. IAsyncDisposable 何时使用？
// 3. lock 的陷阱和 Monitor 关系？
// 4. SemaphoreSlim / ReaderWriterLock 区别？
// 5. ConcurrentDictionary 用法？
// 6. Interlocked 原子操作？
// 7. volatile 关键字？
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class ConcurrencyDemo
{
    public static async Task Run()
    {
        Console.WriteLine("═══ Dispose 模式与并发 ═══\n");

        // === 1. using 语句 ===
        Console.WriteLine("--- 1. using 语句 (同步释放) ---");

        using (var resource = new ManagedResource("Resource1"))
        {
            resource.DoWork();
        } // 自动调用 Dispose
        Console.WriteLine();

        // using 声明（C# 8+）
        Console.WriteLine("  using 声明语法 (C# 8+):");
        using var simpleResource = new ManagedResource("SimpleResource");
        simpleResource.DoWork();
        Console.WriteLine();

        // === 2. await using (IAsyncDisposable) ===
        Console.WriteLine("--- 2. await using (异步释放) ---");

        await using (var asyncRes = new AsyncManagedResource("AsyncResource"))
        {
            await asyncRes.DoWorkAsync();
        }
        Console.WriteLine();

        // === 3. lock 语句和 Monitor ===
        Console.WriteLine("--- 3. lock 语句 ---");

        var counter = new Counter();
        var lockTasks = new Task[10];

        for (int i = 0; i < lockTasks.Length; i++)
        {
            lockTasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                    counter.Increment();
            });
        }

        await Task.WhenAll(lockTasks);
        Console.WriteLine($"  lock 保护后计数: {counter.Value} (预期: 1000)\n");

        // === 4. 没有锁的竞争 ===
        Console.WriteLine("--- 4. 没有锁的竞争 ---");

        var unsafeCounter = 0;
        var unsafeTasks = new Task[10];

        for (int i = 0; i < unsafeTasks.Length; i++)
        {
            unsafeTasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                    unsafeCounter++; // 非原子操作！
            });
        }

        await Task.WhenAll(unsafeTasks);
        Console.WriteLine($"  无锁计数 (可能 < 1000): {unsafeCounter}\n");

        // === 5. Interlocked 原子操作 ===
        Console.WriteLine("--- 5. Interlocked 原子操作 ---");

        var atomicCounter = 0;
        var atomicTasks = new Task[10];

        for (int i = 0; i < atomicTasks.Length; i++)
        {
            atomicTasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                    Interlocked.Increment(ref atomicCounter);
            });
        }

        await Task.WhenAll(atomicTasks);
        Console.WriteLine($"  Interlocked 计数: {atomicCounter} (预期: 1000)\n");

        // === 6. SemaphoreSlim 限流 ===
        Console.WriteLine("--- 6. SemaphoreSlim 并发控制 ---");

        var semaphore = new SemaphoreSlim(3); // 最多 3 个并发
        var semTasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var id = i;
            semTasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    Console.WriteLine($"  任务 {id} 开始 (并发 ≈3)");
                    await Task.Delay(200);
                    Console.WriteLine($"  任务 {id} 结束");
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(semTasks);
        Console.WriteLine();

        // === 7. ConcurrentDictionary ===
        Console.WriteLine("--- 7. ConcurrentDictionary ---");

        var dict = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
        var dictTasks = new Task[10];

        for (int i = 0; i < dictTasks.Length; i++)
        {
            var key = i;
            dictTasks[i] = Task.Run(() =>
            {
                dict.TryAdd(key, $"值{key}");
            });
        }

        await Task.WhenAll(dictTasks);
        Console.WriteLine($"  ConcurrentDictionary 条目数: {dict.Count}");

        // GetOrAdd 的原子操作
        var cached = dict.GetOrAdd(0, k => $"计算{k}");
        Console.WriteLine($"  GetOrAdd: {cached}\n");

        // === 8. ReaderWriterLockSlim ===
        Console.WriteLine("--- 8. ReaderWriterLockSlim ---");

        var rwLock = new ReaderWriterLockSlim();
        var cache = new Dictionary<string, string>();

        // 模拟：读多写少的场景
        var readTasks = new Task[5];
        for (int i = 0; i < readTasks.Length; i++)
        {
            readTasks[i] = Task.Run(() =>
            {
                rwLock.EnterReadLock();
                try
                {
                    // 多个读线程可以同时进入
                    Console.WriteLine($"  读取线程 {Task.CurrentId} 进入");
                    Task.Delay(50).Wait();
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            });
        }

        var writeTask = Task.Run(() =>
        {
            rwLock.EnterWriteLock();
            try
            {
                // 写锁独占
                Console.WriteLine("  写线程进入（独占）");
                cache["key"] = "value";
                Task.Delay(100).Wait();
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        });

        await Task.WhenAll(readTasks.Concat(new[] { writeTask }));
        Console.WriteLine();

        // === 9. lock 的常见错误 ===
        Console.WriteLine("--- 9. lock 常见错误 ---");
        Console.WriteLine("  ❌ lock(this)       → 外部也可能 lock 这个实例");
        Console.WriteLine("  ❌ lock(typeof(T))  → 类型对象是全局共享的");
        Console.WriteLine("  ❌ lock(string)     → 字符串驻留导致意外共享");
        Console.WriteLine("  ✅ lock(_lockObj)   → 私有只读对象\n");

        // === 10. volatile ===
        Console.WriteLine("--- 10. volatile ---");
        Console.WriteLine("  volatile 确保字段的读写不会被编译器/CPU 重排序");
        Console.WriteLine("  常用于标志位模式（如 _isRunning 循环标志）");
        Console.WriteLine("  ⚠️  volatile 不保证原子性，只是防止重排序\n");
    }
}

// === IDisposable 正确模式 ===

public class ManagedResource : IDisposable
{
    private readonly string _name;
    private bool _disposed;

    public ManagedResource(string name)
    {
        _name = name;
        Console.WriteLine($"  [{_name}] 构造函数: 分配资源");
    }

    public void DoWork()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Console.WriteLine($"  [{_name}] 工作中...");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this); // 不需要终结器了
    }

    // 标准 Dispose 模式（虚方法供继承）
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // 释放托管资源
            Console.WriteLine($"  [{_name}] Dispose: 释放托管资源");
        }

        // 释放非托管资源
        Console.WriteLine($"  [{_name}] Dispose: 释放非托管资源");
        _disposed = true;
    }

    // 终结器（有非托管资源时才需要）
    ~ManagedResource()
    {
        Console.WriteLine($"  [{_name}] 终结器调用");
        Dispose(false);
    }
}

// === IAsyncDisposable ===

public class AsyncManagedResource : IAsyncDisposable
{
    private readonly string _name;

    public AsyncManagedResource(string name)
    {
        _name = name;
        Console.WriteLine($"  [{_name}] 异步构造函数: 分配资源");
    }

    public async Task DoWorkAsync()
    {
        await Task.Delay(10);
        Console.WriteLine($"  [{_name}] 异步工作中...");
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(10);
        Console.WriteLine($"  [{_name}] 异步释放资源完成");
    }
}

// === 线程安全计数器 ===

public class Counter
{
    private int _count;
    private readonly object _lock = new();

    public int Value => _count;

    public void Increment()
    {
        lock (_lock)
        {
            _count++;
        }
    }
}
