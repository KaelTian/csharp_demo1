using S7.Net;

namespace S7Protocol.Core;

/// <summary>S7 客户端（基于 S7netplus 库实现）</summary>
public sealed class S7Client : IDisposable
{
    private Plc? _plc;

    /// <summary>是否已连接</summary>
    public bool IsConnected => _plc?.IsConnected == true;

    /// <summary>连接到西门子 PLC</summary>
    public void Connect(string ip, int port = 102, int rack = 0, int slot = 1,
        CpuType cpuType = CpuType.S71200)
    {
        Dispose();
        _plc = new Plc(cpuType, ip, (short)rack, (short)slot);
        _plc.Open();
    }

    /// <summary>断开连接</summary>
    public void Disconnect() => Dispose();

    /// <summary>读取原始字节</summary>
    public byte[] ReadBytes(DataType dataType, int db, int startByte, int length)
    {
        EnsureConnected();
        return _plc!.ReadBytes(dataType, db, startByte, length);
    }

    /// <summary>写入原始字节</summary>
    public void WriteBytes(DataType dataType, int db, int startByte, byte[] data)
    {
        EnsureConnected();
        _plc!.WriteBytes(dataType, db, startByte, data);
    }

    /// <summary>按地址字符串读取（如 "DB72.DBD2", "DB72.DBX0.0"）</summary>
    public object? Read(string address)
    {
        EnsureConnected();
        return _plc!.Read(address);
    }

    /// <summary>按地址字符串写入（如 "DB72.DBD2", "DB72.DBX0.0"）</summary>
    public void Write(string address, object value)
    {
        EnsureConnected();
        _plc!.Write(address, value);
    }

    /// <summary>
    /// 批量读取多个地址。
    /// 地址格式：["DB72.DBD2", "DB72.DBX0.0"]
    /// </summary>
    public Dictionary<string, S7ReadResult> ReadMultiple(params string[] addresses)
    {
        EnsureConnected();
        var results = new Dictionary<string, S7ReadResult>(addresses.Length);
        foreach (var addr in addresses)
        {
            try
            {
                var val = _plc!.Read(addr);
                results[addr] = new(addr, val, true);
            }
            catch (Exception ex)
            {
                results[addr] = new(addr, null, false, ex.Message);
            }
        }
        return results;
    }

    public void Dispose()
    {
        if (_plc != null)
        {
            try { _plc.Close(); } catch { }
            _plc = null;
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("PLC 未连接。请先调用 Connect()");
    }
}
