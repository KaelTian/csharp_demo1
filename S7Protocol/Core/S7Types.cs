namespace S7Protocol.Core;

/// <summary>PLC 存储区域</summary>
public enum Area : byte
{
    ProcessInputs = 0x81,   // I
    ProcessOutputs = 0x82,  // Q
    Merkers = 0x83,         // M
    DataBlock = 0x84,       // DB
    Counters = 0x1C,        // C
    Timers = 0x1D,          // T
}

/// <summary>S7 传输大小/数据类型编码</summary>
public enum TransportSize : byte
{
    Bit = 0x01,
    Byte = 0x02,
    Char = 0x03,
    Word = 0x04,
    Int = 0x05,
    DWord = 0x06,
    DInt = 0x07,
    Real = 0x08,
    Counter = 0x1C,
    Timer = 0x1D,
}

/// <summary>解析后的 S7 地址信息</summary>
public readonly struct S7AddressInfo
{
    public Area Area { get; }
    public int DbNumber { get; }
    public int ByteAddress { get; }
    public int BitNumber { get; }
    public int ByteLength { get; }
    public TransportSize TransportSize { get; }

    public S7AddressInfo(Area area, int dbNumber, int byteAddress, int bitNumber,
        int byteLength, TransportSize transportSize)
    {
        Area = area;
        DbNumber = dbNumber;
        ByteAddress = byteAddress;
        BitNumber = bitNumber;
        ByteLength = byteLength;
        TransportSize = transportSize;
    }

    public bool IsBit => TransportSize == TransportSize.Bit;
}

/// <summary>读取结果</summary>
public readonly struct S7ReadResult
{
    public string Address { get; }
    public object? Value { get; }
    public bool Success { get; }
    public string? Error { get; }

    public S7ReadResult(string address, object? value, bool success, string? error = null)
    {
        Address = address;
        Value = value;
        Success = success;
        Error = error;
    }
}
