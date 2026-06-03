using S7.Net;
using S7Protocol.Core;

// ============================================================
// S7 客户端 Demo（基于 S7netplus 库）
// ============================================================
// 修改以下 PLC 连接参数以匹配你的环境。
// ============================================================

const string plcIp = "192.168.0.241";
const int plcPort = 102;
const int rack = 0;
const int slot = 1;

Console.WriteLine("=== S7 客户端 Demo ===\n");
Console.WriteLine($"目标 PLC: {plcIp}:{plcPort}  Rack={rack} Slot={slot}\n");

try
{
    using var client = new S7Client();
    client.Connect(plcIp, plcPort, rack, slot,cpuType: CpuType.S71500);
    Console.WriteLine("[OK] 已连接到 PLC\n");

    // DB72 实际点位:
    //   DB72.DBD2   → 双字(4字节), 偏移2
    //   DB72.DBX0.0 → 位, 字节0 位0

    // ---- 1. 读取原始字节 ----
    Console.WriteLine("--- 1. ReadBytes ---");
    var raw = client.ReadBytes(DataType.DataBlock, 72, 2, 4);
    Console.WriteLine($"DB72.DBD2 = {BitConverter.ToString(raw)}\n");

    // ---- 2. 按地址读取 ----
    Console.WriteLine("--- 2. Read ---");
    var val = client.Read("DB72.DBD2");
    Console.WriteLine($"DB72.DBD2 = {val}  ({val?.GetType().Name})");

    var bit = client.Read("DB72.DBX0.0");
    Console.WriteLine($"DB72.DBX0.0 = {bit}  ({bit?.GetType().Name})\n");

    // ---- 3. 批量读取 ----
    Console.WriteLine("--- 3. ReadMultiple ---");
    var multi = client.ReadMultiple("DB72.DBD2", "DB72.DBX0.0");
    foreach (var kv in multi)
    {
        var r = kv.Value;
        Console.WriteLine($"  {kv.Key} => " +
            (r.Success ? r.Value?.ToString() ?? "null" : $"ERROR: {r.Error}"));
    }

    // ---- 4. 写入 ----
    Console.WriteLine("\n--- 4. Write ---");
    client.Write("DB72.DBX0.0", true);
    Console.WriteLine("DB72.DBX0.0 := true");

    client.Write("DB72.DBD2", 999);
    Console.WriteLine("DB72.DBD2 := 999");

    // ---- 5. 写入原始字节 ----
    Console.WriteLine("\n--- 5. WriteBytes ---");
    client.WriteBytes(DataType.DataBlock, 72, 2, [0x00, 0x00, 0x03, 0xE8]);
    Console.WriteLine("DB72.DBD2 := [00 00 03 E8]\n");

    Console.WriteLine("=== Demo 结束 ===");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[错误] {ex.GetType().Name}: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine("\n提示: 请检查 PLC IP/端口/机架/槽位是否正确，以及通信链路是否畅通。");
}
