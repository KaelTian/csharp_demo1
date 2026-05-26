namespace MQTTTutorial
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var broker = new SimpleBroker(1883);
            _ = broker.StartAsync();
            await Task.Delay(500);

            var clientA = new SimpleClient("ClientA-产线A5");
            var clientB = new SimpleClient("ClientB-监控");
            var clientC = new SimpleClient("ClientC-总控");

            // 挂接消息接收事件
            clientA.OnMessageReceived += (t, p) => Console.WriteLine($"[ClientA] 接收 [{t}]: {p}");
            clientB.OnMessageReceived += (t, p) => Console.WriteLine($"[ClientB] 接收 [{t}]: {p}");
            clientC.OnMessageReceived += (t, p) => Console.WriteLine($"[ClientC] 接收 [{t}]: {p}");

            await clientA.ConnectAsync("127.0.0.1", 1883);
            await clientB.ConnectAsync("127.0.0.1", 1883);
            await clientC.ConnectAsync("127.0.0.1", 1883);

            await clientA.SubscribeAsync("factory/lineA5/status");
            await clientB.SubscribeAsync("factory/+/alarm");
            await clientC.SubscribeAsync("factory/#");

            await Task.Delay(300);

            await clientA.PublishAsync("factory/lineA5/status", "运行中");
            await clientA.PublishAsync("factory/lineA5/alarm", "温度超限");

            Console.ReadLine();
        }
    }
}
