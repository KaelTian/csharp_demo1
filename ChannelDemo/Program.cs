using System.Threading.Channels;
using ChannelDemo;

// ── Configuration ──────────────────────────────────────────────────────────
const int MaxWrites = 1000;
const int ChannelCapacity = 256;

// ── Setup Channel & Components ────────────────────────────────────────────
var channel = Channel.CreateBounded<ChannelItem>(new BoundedChannelOptions(ChannelCapacity)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleWriter = false,
    SingleReader = true,
});

var consumer = new LoggingConsumer(channel);
var producer = new Producer(channel, MaxWrites);

// ── Start Consumer ────────────────────────────────────────────────────────
consumer.Start();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n[!] Shutdown requested...");
};

// ── Interactive Loop ──────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║    Channel<T> Producer-Consumer Demo             ║");
Console.WriteLine("╠══════════════════════════════════════════════════╣");
Console.WriteLine("║  [Enter] Write   10 items  [A] Write 100 items  ║");
Console.WriteLine("║  [S]     Status              [Q] Quit           ║");
Console.WriteLine($"║  Max writes: {MaxWrites,-5}  Capacity: {ChannelCapacity,-5}              ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");

while (true)
{
    Console.Write($"\n[{producer.TotalWritten}/{MaxWrites}] > ");
    
    // 读取一行输入（兼容 VS Code 调试）
    string input = Console.ReadLine()?.Trim().ToUpper() ?? "";

    try
    {
        // 直接判断字符串，比 ConsoleKey 稳定 100 倍
        switch (input)
        {
            case "W":
            case "": // 回车 = 空字符串，也触发 W 功能
                await WriteItems(10);
                break;

            case "A":
                await WriteItems(100);
                break;

            case "S":
                PrintStatus();
                break;

            case "Q":
                Console.WriteLine("Quitting...");
                await ShutdownAsync();
                return;
        }
    }
    catch (ChannelClosedException)
    {
        Console.WriteLine("[!] Channel closed. No more writes allowed.");
    }
}
// ── Helper Methods ────────────────────────────────────────────────────────

async Task WriteItems(int count)
{
    var written = await producer.WriteBatchAsync(count, CancellationToken.None);
    Console.WriteLine(written > 0
        ? $"  → Wrote {written} items"
        : "  → Max writes reached, channel completed.");
}

void PrintStatus()
{
    Console.WriteLine();
    Console.WriteLine($"  Produced:  {producer.TotalWritten,-5} / {producer.MaxWrites}");
    Console.WriteLine($"  Consumed:  {consumer.ProcessedCount,-5}");
    Console.WriteLine($"  Remaining: {producer.Remaining,-5}");
    Console.WriteLine($"  Queue:     ~{channel.Reader.Count,-5} items in buffer");
    Console.WriteLine($"  Completed: {producer.IsCompleted}");
}

async Task ShutdownAsync()
{
    // Signal no more writes, then wait for consumer to drain the channel
    channel.Writer.TryComplete();
    await consumer.StopAsync();
    consumer.Dispose();
}
