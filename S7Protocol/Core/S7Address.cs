using System.Text.RegularExpressions;

namespace S7Protocol.Core;

/// <summary>解析 S7 DB 区地址字符串（遵循西门子原生寻址规范）</summary>
public static partial class S7Address
{
    // 标准原生格式: DB{number}.{DBX|DBB|DBW|DBD}{offset}[.{bit}]
    // 拓展简写:     DB{number}.{DINT|REAL}{offset}  → 底层映射为 DBD(4字节)
    [GeneratedRegex(
        @"^DB(\d+)\.(DBX|DBB|DBW|DBD|DINT|REAL)\s*(\d+)(?:\.(\d+))?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex DbAddressPattern();

    /// <summary>
    /// 解析 S7 DB 区标准地址字符串（遵循西门子 S7 原生寻址规范，兼容 Snap7/S7.NET）。
    ///
    /// 标准原生格式：
    ///   DB72.DBD2   → DB72, 双字(DWord), 起始字节偏移2(占4字节，DINT/REAL均使用DBD寻址)
    ///   DB72.DBW0   → DB72, 字(Word),   起始字节偏移0(占2字节)
    ///   DB72.DBB10  → DB72, 字节(Byte), 字节偏移10(占1字节)
    ///   DB72.DBX0.0 → DB72, 位(Bit),    字节偏移0, 位编号0(0~7)
    ///
    /// 【拓展自定义简写（上层映射，底层协议转为DBD）】
    ///   DB72.DINT4  → 自定义整型简写，底层等效 DB72.DBD4
    ///   DB72.REAL8  → 自定义浮点简写，底层等效 DB72.DBD8
    ///
    /// 说明：DINT、REAL 是变量数据类型，S7 协议无专属地址符号，
    ///       统一占用 DBD(4字节) 地址。
    /// </summary>
    public static S7AddressInfo Parse(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var match = DbAddressPattern().Match(address.Trim());
        if (!match.Success)
            throw new FormatException($"无法解析 S7 地址: '{address}'。" +
                "标准格式: DB72.DBD2, DB72.DBW0, DB72.DBB10, DB72.DBX0.0\n" +
                "拓展简写: DB72.DINT4 (→DBD), DB72.REAL8 (→DBD)");

        var db = int.Parse(match.Groups[1].Value);
        var typeToken = match.Groups[2].Value.ToUpperInvariant();
        var byteAddr = int.Parse(match.Groups[3].Value);
        var bitStr = match.Groups[4].Value;

        return typeToken switch
        {
            // 标准原生格式
            "DBX"  => ParseBit(db, byteAddr, bitStr),
            "DBB"  => new(Area.DataBlock, db, byteAddr, 0, 1, TransportSize.Byte),
            "DBW"  => new(Area.DataBlock, db, byteAddr, 0, 2, TransportSize.Int),
            "DBD"  => new(Area.DataBlock, db, byteAddr, 0, 4, TransportSize.DWord),
            // 拓展简写 → 底层映射为 DBD(4字节)
            "DINT" => new(Area.DataBlock, db, byteAddr, 0, 4, TransportSize.DInt),
            "REAL" => new(Area.DataBlock, db, byteAddr, 0, 4, TransportSize.Real),
            _ => throw new FormatException($"不支持的数据类型: '{typeToken}'"),
        };
    }

    private static S7AddressInfo ParseBit(int db, int byteAddr, string bitStr)
    {
        var bit = string.IsNullOrEmpty(bitStr) ? 0 : int.Parse(bitStr);
        if (bit is < 0 or > 7)
            throw new FormatException($"位地址必须在 0-7 之间: {bit}");
        return new(Area.DataBlock, db, byteAddr, bit, 1, TransportSize.Bit);
    }
}
