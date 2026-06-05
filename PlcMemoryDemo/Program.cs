using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PlcMemoryDemo;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // ========= 场景参数（模拟真实 PLC 监控系统）=========
        const int Lines = 10;                 // 产线数
        const double Hz = 50;                // 每线每秒产生事件数
        const int RunSec = 12;               // 每方案运行时间
        const int BlockMs = 30_000;          // OPC UA 阻塞时间（模拟卡死）
        const int TagsPerEvent = 500;        // 每次变更的 PLC 标签数

        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.WriteLine("  PLC 点位变更 · 内存暴涨模拟（更真实数据规模）");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  产线={Lines} × {Hz}Hz = {Lines * Hz}/秒事件");
        Console.WriteLine($"  每次变更 {TagsPerEvent} 个标签（含值+时间戳+质量戳）≈ ~25KB/事件");
        Console.WriteLine($"  下游阻塞 {BlockMs / 1000}s | 运行 {RunSec}s");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════\n");

        // ============== 方案 0：问题版本 ==============
        RunScenario("【问题版】无限 Task.Run", () =>
        {
            var sim = new ProblemSimulator(BlockMs);
            RunContinuous(sim, Lines, Hz, RunSec);
            return sim.Stats;
        });
        ForceGc();

        // ============== 方案 1：SemaphoreSlim ==============
        RunScenario("【优化1】SemaphoreSlim (并发=15, 满则丢弃)", () =>
        {
            var sim = new SemaphoreSimulator(BlockMs, 15);
            RunContinuous(sim, Lines, Hz, RunSec);
            return sim.Stats;
        });
        ForceGc();

        // ============== 方案 2：Channel ==============
        RunScenario("【优化2】Channel 背压 (容量=200, 消费者=4)", () =>
        {
            var sim = new ChannelSimulator(BlockMs, 200, 4);
            RunContinuous(sim, Lines, Hz, RunSec);
            return sim.Stats;
        });
        ForceGc();

        // ============== 方案 3：超时熔断 ==============
        RunScenario("【优化3】超时熔断 (pending≤30, 超时5s取消)", () =>
        {
            var sim = new TimeoutSimulator(BlockMs, 30, 5);
            RunContinuous(sim, Lines, Hz, RunSec);
            return sim.Stats;
        });

        Console.WriteLine("\n══════════════════════════════════════════════════════════════════════");
        Console.WriteLine("  GC 分配量 = 所有 Task + 闭包 + 大块 PLC 标签数据的实际代价");
        Console.WriteLine("  问题版本每 12s 分配数 GB → 持续数分钟 → OOM");
        Console.WriteLine("  优化版本丢弃过载事件，分配量极少 → 安全");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.ReadLine();
    }

    static void RunScenario(string title, Func<SimStats> action)
    {
        var proc = Process.GetCurrentProcess();
        var startAlloc = GC.GetTotalAllocatedBytes();
        var startMem = proc.WorkingSet64;
        var startHandles = proc.HandleCount;
        ThreadPool.GetMinThreads(out var mn, out _);
        ThreadPool.GetMaxThreads(out var mx, out _);

        Console.WriteLine($"\n┌─ {title}");
        Console.WriteLine($"│  池[min={mn}..max={mx}]  工作集={startMem / 1024 / 1024}MB  句柄={startHandles}");

        var sw = Stopwatch.StartNew();
        var st = action();
        sw.Stop();

        var allocMB = (GC.GetTotalAllocatedBytes() - startAlloc) / 1024 / 1024;
        long finalMem = 0;
        for (int i = 0; i < 3; i++) { proc.Refresh(); finalMem = proc.WorkingSet64; Thread.Sleep(200); }

        var memΔ = (finalMem - startMem) / 1024 / 1024;
        Console.WriteLine($"│  ─────────────────────────────────────────────────────────");
        Console.WriteLine($"│  耗时={sw.Elapsed.TotalSeconds:F0}s  工作集+{memΔ}MB  句柄+{proc.HandleCount - startHandles}");
        Console.WriteLine($"│  GC 分配={allocMB}MB  pending={st.Pending}  dropped={st.Dropped}");

        var alarm = allocMB > 2000 ? $"⚠️ 爆炸! {allocMB}MB/12s, 持续=> OOM" :
                    allocMB > 500  ? $"⚠️ 高危 {allocMB}MB" :
                    allocMB > 100  ? "⚠️ 偏高" : "✅ 安全";
        Console.WriteLine($"│  {alarm}");
        Console.WriteLine($"└─");
    }

    static void RunContinuous(object sim, int lines, double hz, int runSec)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(runSec));
        var ct = cts.Token;
        var tickMs = (int)(1000.0 / hz);

        var producers = new Task[lines];
        for (int l = 0; l < lines; l++)
        {
            var lineId = l;
            producers[l] = Task.Run(() =>
            {
                var rng = new Random(lineId);
                int seq = 0;
                while (!ct.IsCancellationRequested)
                {
                    seq++;
                    // 更真实的 PLC 标签数据: 500 个标签, 每个含值+质量戳
                    var changed = new List<PlcTag>(TagsPerEvent);
                    for (int t = 0; t < TagsPerEvent; t++)
                    {
                        changed.Add(new PlcTag
                        {
                            Id = $"Line{lineId}.Tag{seq}.{t}",
                            Value = rng.NextDouble() * 100,
                            Quality = 192,  // 良好
                            Timestamp = DateTime.UtcNow,
                            DataType = "REAL"
                        });
                    }

                    var simDyn = sim as dynamic;
                    simDyn.PushEvent(changed);

                    try { Task.Delay(tickMs, ct).Wait(); } catch { return; }
                }
            });
        }

        try { Task.WaitAll(producers); } catch { }
        Thread.Sleep(2000);
    }

    static void ForceGc()
    {
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        Thread.Sleep(1000);
    }

    const int TagsPerEvent = 500;
}

/// <summary>模拟 PLC 标签数据（增加每个事件的数据量）</summary>
public struct PlcTag
{
    public string Id { get; set; }
    public double Value { get; set; }
    public int Quality { get; set; }
    public DateTime Timestamp { get; set; }
    public string DataType { get; set; }
}

public record SimStats(long Pending, long Dropped);

// ═══════════════════════════════════════════════════════════════
// 【问题】无限 Task.Run — Task + 大闭包 + 线程堆积
// ═══════════════════════════════════════════════════════════════
public class ProblemSimulator
{
    readonly int _blockMs;
    long _pend;
    public SimStats Stats => new(Interlocked.Read(ref _pend), 0);

    public ProblemSimulator(int blockMs) => _blockMs = blockMs;

    public void PushEvent(List<PlcTag> changed)
    {
        Interlocked.Increment(ref _pend);
        _ = Task.Run(() =>
        {
            try { Thread.Sleep(_blockMs); Process(changed); }
            catch { }
            finally { Interlocked.Decrement(ref _pend); }
        });
    }
    static void Process(List<PlcTag> d) { }
}

// ═══════════════════════════════════════════════════════════════
// 【优化1】SemaphoreSlim 限流
// ═══════════════════════════════════════════════════════════════
public class SemaphoreSimulator
{
    readonly int _blockMs;
    readonly SemaphoreSlim _sem;
    long _drop;
    public SimStats Stats => new(0, Interlocked.Read(ref _drop));

    public SemaphoreSimulator(int blockMs, int max)
    {
        _blockMs = blockMs;
        _sem = new SemaphoreSlim(max, max);
    }

    public void PushEvent(List<PlcTag> changed)
    {
        if (!_sem.Wait(0)) { Interlocked.Increment(ref _drop); return; }
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(_blockMs); }
            catch { }
            finally { _sem.Release(); }
        });
    }
}

// ═══════════════════════════════════════════════════════════════
// 【优化2】Channel 背压 — 有界队列 + 固定消费者
// ═══════════════════════════════════════════════════════════════
public class ChannelSimulator
{
    readonly int _blockMs;
    readonly Channel<List<PlcTag>> _ch;
    readonly CancellationTokenSource _cts = new();
    long _drop;
    public SimStats Stats => new(0, Interlocked.Read(ref _drop));

    public ChannelSimulator(int blockMs, int cap, int workers)
    {
        _blockMs = blockMs;
        _ch = Channel.CreateBounded<List<PlcTag>>(
            new BoundedChannelOptions(cap) { FullMode = BoundedChannelFullMode.DropOldest });
        for (int i = 0; i < workers; i++)
            _ = Task.Run(() => Consume(_cts.Token));
    }

    public void PushEvent(List<PlcTag> d)
    {
        if (!_ch.Writer.TryWrite(d))
            Interlocked.Increment(ref _drop);
    }

    async Task Consume(CancellationToken ct)
    {
        await foreach (var _ in _ch.Reader.ReadAllAsync(ct))
            try { await Task.Delay(_blockMs, ct); }
            catch { break; }
    }
}

// ═══════════════════════════════════════════════════════════════
// 【优化3】超时熔断 — pending 阈值 + 超时取消
// ═══════════════════════════════════════════════════════════════
public class TimeoutSimulator
{
    readonly int _blockMs, _maxPend, _timeoutMs;
    long _pend, _drop;
    public SimStats Stats => new(Interlocked.Read(ref _pend), Interlocked.Read(ref _drop));

    public TimeoutSimulator(int blockMs, int maxPend, int timeoutSec)
    {
        _blockMs = blockMs; _maxPend = maxPend;
        _timeoutMs = timeoutSec * 1000;
    }

    public void PushEvent(List<PlcTag> data)
    {
        if (Interlocked.Read(ref _pend) >= _maxPend) { Interlocked.Increment(ref _drop); return; }
        Interlocked.Increment(ref _pend);
        _ = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(_timeoutMs);
            try { await Task.Delay(_blockMs, cts.Token); }
            catch (OperationCanceledException) { }
            catch { }
            finally { Interlocked.Decrement(ref _pend); }
        });
    }
}
