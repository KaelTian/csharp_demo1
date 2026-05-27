using S7.Net;

namespace MesDemo.Tests.Bad;

public class PlcBadExample
{
    // ❌ 反面：硬编码 IP 和 DB 号，无配置化
    private readonly string _plcIp = "192.168.1.10";
    private readonly Plc _plc = new Plc(CpuType.S71200, "192.168.1.10", 0, 1);

    public void ReadProductionCount()
    {
        // ❌ 反面：循环内反复 Open/Close，性能极差
        // ❌ 反面：无异常捕获，PLC 断线时线程直接崩
        _plc.Open();
        var count = _plc.Read("DB401.DBW0");
        _plc.Close();
        
        // ❌ 反面：魔法数字，且直接强转不校验类型
        int result = (int)count;
        
        // ❌ 反面：直接写数据库，无事务无批量
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Server=localhost;Database=mes;Uid=root;Pwd=123456;",
            DbType = DbType.MySql
        });
        db.Insertable(new { Count = result, Time = DateTime.Now }).ExecuteCommand();
    }
}