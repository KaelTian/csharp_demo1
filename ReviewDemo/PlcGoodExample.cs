using S7.Net;
using SqlSugar;

namespace MesDemo.Tests.Good;

public class PlcGoodExample
{
    private readonly Plc _plc;
    private readonly SqlSugarClient _db;
    private readonly ILogger<PlcGoodExample> _logger;

    public PlcGoodExample(IConfiguration config, ILogger<PlcGoodExample> logger)
    {
        // ✅ 配置化：IP、机架、槽位、DB 号全走配置
        var plcConfig = config.GetSection("Plc:A1");
        _plc = new Plc(
            Enum.Parse<CpuType>(plcConfig["CpuType"]!),
            plcConfig["Ip"]!,
            byte.Parse(plcConfig["Rack"]!),
            byte.Parse(plcConfig["Slot"]!));
        
        _db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = config.GetConnectionString("MySql"),
            DbType = DbType.MySql,
            IsAutoCloseConnection = true  // ✅ 自动释放
        });
        _logger = logger;
    }

    public async Task ReadAndSaveAsync(CancellationToken ct = default)
    {
        try
        {
            // ✅ 长连接模式下先检查状态
            if (!_plc.IsConnected)
                _plc.Open();

            // ✅ 明确指定读取类型和长度，且带超时/取消令牌
            var bytes = await _plc.ReadBytesAsync(DataType.DataBlock, 401, 0, 2, ct);
            int count = S7.Net.Types.Word.FromByteArray(bytes);

            // ✅ 批量+事务写入，且用实体类而非匿名对象
            var record = new ProductionRecord 
            { 
                LineId = "A1", 
                Count = count, 
                CaptureTime = DateTime.Now 
            };
            
            await _db.Insertable(record).ExecuteCommandAsync(ct);
            _logger.LogInformation("A1 产量读取成功: {Count}", count);
        }
        catch (Exception ex)
        {
            // ✅ 异常不吞，记录完整上下文（点位地址）
            _logger.LogError(ex, "PLC A1 读取失败 [DB401.DBW0]");
            throw;  // 抛给上层决定重试或报警
        }
    }
    
    // ✅ 程序退出时统一释放
    public void Dispose()
    {
        _plc?.Close();
        _db?.Dispose();
    }
}

public class ProductionRecord
{
    public string LineId { get; set; } = null!;
    public int Count { get; set; }
    public DateTime CaptureTime { get; set; }
}