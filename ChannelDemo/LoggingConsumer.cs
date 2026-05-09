using System.Globalization;
using System.Threading.Channels;

namespace ChannelDemo;

public sealed class LoggingConsumer : IDisposable
{
    private readonly Channel<ChannelItem> _channel;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _cts;
    private Task? _task;
    private long _processedCount;

    public long ProcessedCount => _processedCount;

    public LoggingConsumer(Channel<ChannelItem> channel, string? logDirectory = null)
    {
        _channel = channel;

        var dir = logDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(dir);

        var logPath = Path.Combine(dir, $"channel-demo-{DateTime.UtcNow:yyyyMMdd}.log");
        _writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
        _cts = new CancellationTokenSource();
    }

    public void Start()
    {
        _task = RunAsync(_cts.Token);
    }

    /// <summary>
    /// Wait for consumer to finish processing after the channel writer is completed.
    /// </summary>
    public async Task StopAsync()
    {
        if (_task is not null)
            await _task;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await _writer.WriteLineAsync(Format("Consumer started, waiting for items..."));

        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(ct))
            {
                await _writer.WriteLineAsync(
                    Format($"Processed Id={item.Id,-4} | Payload={item.Payload} | ProducedAt={item.Timestamp:HH:mm:ss.fff}"));

                Interlocked.Increment(ref _processedCount);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown via cancellation
        }

        await _writer.WriteLineAsync(Format($"Consumer stopped. Total processed: {_processedCount}"));
    }

    public void Dispose()
    {
        _cts.Dispose();
        _writer.Dispose();
    }

    private static string Format(string message) =>
        $"[{Timestamp()}] {message}";

    private static string Timestamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
}
