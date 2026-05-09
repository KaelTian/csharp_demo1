using System.Threading.Channels;

namespace ChannelDemo;

public sealed class Producer
{
    private readonly Channel<ChannelItem> _channel;
    private readonly int _maxWrites;
    private int _totalWritten;
    private bool _completed;

    public Producer(Channel<ChannelItem> channel, int maxWrites = 1000)
    {
        _channel = channel;
        _maxWrites = maxWrites;
    }

    public int TotalWritten => _totalWritten;
    public int MaxWrites => _maxWrites;
    public int Remaining => _maxWrites - _totalWritten;
    public bool IsCompleted => _completed;

    public async Task<int> WriteBatchAsync(int count, CancellationToken ct)
    {
        if (_completed) return 0;

        var actualCount = Math.Min(count, _maxWrites - _totalWritten);
        if (actualCount <= 0)
        {
            await CompleteAsync(ct);
            return 0;
        }

        for (int i = 0; i < actualCount; i++)
        {
            var item = new ChannelItem(
                Id: Interlocked.Increment(ref _totalWritten),
                Timestamp: DateTime.UtcNow,
                Payload: $"data-{Guid.NewGuid():N}"[..16]);

            await _channel.Writer.WriteAsync(item, ct);
        }

        if (_totalWritten >= _maxWrites)
            await CompleteAsync(ct);

        return actualCount;
    }

    private Task CompleteAsync(CancellationToken ct)
    {
        _completed = true;
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}

public sealed record ChannelItem(int Id, DateTime Timestamp, string Payload);
