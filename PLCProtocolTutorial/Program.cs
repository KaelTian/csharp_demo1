namespace PLCProtocolTutorial
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 根据现场 PLC 型号调整 rack / slot
            var plc = new S7NativeClient(rack: 0, slot: 1);

            try
            {
                plc.Connect("192.168.0.241");
                Console.WriteLine("=== PLC 原生协议已连接 ===\n");

                // 1. 读写 DB72.DBD2（Real）
                float f = plc.ReadReal(72, 2);
                Console.WriteLine($"[读] DB72.DBD2 = {f}");

                plc.WriteReal(72, 2, 123.45f);
                Console.WriteLine("[写] DB72.DBD2 = 123.45");

                f = plc.ReadReal(72, 2);
                Console.WriteLine($"[读] DB72.DBD2 = {f}（验证）\n");

                // 2. 读写 DB72.DBX0.0（Bool）
                bool b = plc.ReadBool(72, 0, 0);
                Console.WriteLine($"[读] DB72.DBX0.0 = {b}");

                plc.WriteBool(72, 0, 0, true);
                Console.WriteLine("[写] DB72.DBX0.0 = true");

                b = plc.ReadBool(72, 0, 0);
                Console.WriteLine($"[读] DB72.DBX0.0 = {b}（验证）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"通信异常: {ex.Message}");
            }
            finally
            {
                plc.Disconnect();
                Console.WriteLine("\n连接已断开");
            }
        }
    }
}
