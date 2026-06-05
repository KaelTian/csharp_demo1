using System.Timers;
using S7.Net;
using WPFTutorial.Models;
using Timer = System.Timers.Timer;

namespace WPFTutorial.Services;

public class PlcDataService : IDisposable
{
    private Plc? _plc;
    private Timer? _pollTimer;
    private PlcConnectionConfig _config = new();
    private readonly List<PlcTagDefinition> _tags = new();
    private readonly List<PlcTagValue> _tagValues = new();

    public IReadOnlyList<PlcTagValue> TagValues => _tagValues.AsReadOnly();
    public bool IsConnected => _plc?.IsConnected ?? false;
    public string LastError { get; private set; } = "";

    public event Action? DataUpdated;
    public event Action<bool>? ConnectionChanged;

    public void Configure(IEnumerable<PlcTagDefinition> tags, PlcConnectionConfig config)
    {
        _tags.Clear();
        _tags.AddRange(tags);
        _tagValues.Clear();
        foreach (var tag in _tags)
        {
            _tagValues.Add(new PlcTagValue
            {
                Name = tag.Name,
                Address = tag.S7Address,
                IsConnected = false,
            });
        }
        _config = config;
    }

    public async Task ConnectAsync()
    {
        try
        {
            Disconnect();

            _plc = new Plc(_config.CpuType, _config.IpAddress, (short)_config.Rack, (short)_config.Slot);
            _plc.ReadTimeout = _config.TimeoutMs;

            await _plc.OpenAsync(CancellationToken.None);

            if (_plc.IsConnected)
            {
                LastError = "";
                StartPolling();
                ConnectionChanged?.Invoke(true);

                // Initial read
                await ReadAllTagsAsync();
            }
            else
            {
                LastError = "连接失败: 未知错误";
                ConnectionChanged?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            LastError = $"连接错误: {ex.Message}";
            ConnectionChanged?.Invoke(false);
        }
    }

    public void Disconnect()
    {
        StopPolling();

        if (_plc is { IsConnected: true })
        {
            try { _plc.Close(); } catch { /* ignore */ }
        }
        _plc = null;

        foreach (var tv in _tagValues)
            tv.IsConnected = false;

        ConnectionChanged?.Invoke(false);
    }

    private void StartPolling()
    {
        StopPolling();
        _pollTimer = new Timer(_config.PollIntervalMs);
        _pollTimer.Elapsed += async (_, _) => await ReadAllTagsAsync();
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private async Task ReadAllTagsAsync()
    {
        if (_plc is not { IsConnected: true }) return;

        try
        {
            foreach (var tag in _tags)
            {
                if (_plc == null || !_plc.IsConnected) break;

                var value = await ReadTagAsync(tag);
                var tagValue = _tagValues.Find(tv => tv.Name == tag.Name);
                if (tagValue != null)
                {
                    tagValue.Value = value;
                    tagValue.IsConnected = true;
                }
            }

            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            LastError = $"读取错误: {ex.Message}";

            foreach (var tv in _tagValues)
                tv.IsConnected = false;

            ConnectionChanged?.Invoke(false);
        }
    }

    private async Task<object?> ReadTagAsync(PlcTagDefinition tag)
    {
        if (_plc == null || !_plc.IsConnected) return null;

        try
        {
            return tag.PlcType switch
            {
                PlcTagDataType.Real => S7.Net.Types.Real.FromByteArray(
                    await _plc.ReadBytesAsync(DataType.DataBlock, tag.DbNumber, tag.ByteOffset, 4)),

                PlcTagDataType.Int => await _plc.ReadAsync($"DB{tag.DbNumber}.DBW{tag.ByteOffset}"),
                PlcTagDataType.DInt => await _plc.ReadAsync($"DB{tag.DbNumber}.DBD{tag.ByteOffset}"),
                PlcTagDataType.Bool => await _plc.ReadAsync($"DB{tag.DbNumber}.DBX{tag.ByteOffset}.{tag.BitOffset}"),
                _ => await _plc.ReadAsync(tag.S7Address),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get a specific tag value by name, cast to double (for REAL tags)
    /// </summary>
    public double GetDoubleValue(string name)
    {
        var tv = _tagValues.Find(x => x.Name == name);
        if (tv?.Value is float f) return f;
        if (tv?.Value is double d) return d;
        if (tv?.Value is int i) return i;
        return 0;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
