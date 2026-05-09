using Microsoft.AspNetCore.SignalR.Client;

const string HubUrl = "http://localhost:5060/hubs/demo";

Console.WriteLine("=== Client1 (消息发送端) ===");
Console.WriteLine("正在连接 SignalR Server...");

var connection = new HubConnectionBuilder()
    .WithUrl(HubUrl)
    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
    .Build();

// 接收来自 Client2 的消息
connection.On<string, string, string>("ReceiveMessageFromClient2", (clientName, message, source) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[Client2 → 我] {clientName}: {message}");
    Console.ResetColor();
});

// 接收来自自身的消息回显
connection.On<string, string, string>("ReceiveMessage", (clientName, message, source) =>
{
    if (source == "client1")
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[我 → Server] {clientName}: {message}");
        Console.ResetColor();
    }
});

connection.Reconnecting += _ =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[重连中] {DateTime.Now:HH:mm:ss} — 正在尝试重新连接...");
    Console.ResetColor();
    return Task.CompletedTask;
};

connection.Reconnected += _ =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[重连成功] {DateTime.Now:HH:mm:ss} — 已重新连接, ConnectionId: {connection.ConnectionId}");
    Console.ResetColor();
    return Task.CompletedTask;
};

connection.Closed += async (error) =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[连接断开] {DateTime.Now:HH:mm:ss} — 原因: {error?.Message ?? "未知"}");
    Console.WriteLine("[重试中] 5 秒后尝试重新连接...");
    Console.ResetColor();

    await Task.Delay(5000);

    int retryCount = 0;
    while (true)
    {
        try
        {
            await connection.StartAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[重连成功] {DateTime.Now:HH:mm:ss}");
            Console.ResetColor();
            break;
        }
        catch (Exception ex)
        {
            retryCount++;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[重连失败 #{retryCount}] {DateTime.Now:HH:mm:ss} — {ex.Message}, 5 秒后重试...");
            Console.ResetColor();
            await Task.Delay(5000);
        }
    }
};

try
{
    await connection.StartAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"连接成功！ConnectionId: {connection.ConnectionId}");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"初始连接失败: {ex.Message}");
    Console.ResetColor();
    return;
}

Console.WriteLine("\n按 Enter 手动发送消息，按 Q 退出");
Console.WriteLine("每 3 秒自动发送消息...\n");

var cts = new CancellationTokenSource();

// 自动发送
_ = Task.Run(async () =>
{
    int count = 0;
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(3000, cts.Token);
            count++;
            var message = $"自动消息 #{count} — {DateTime.Now:HH:mm:ss.fff}";
            await connection.InvokeAsync("SendMessageFromClient1", "Client1", message);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"发送失败: {ex.Message}");
            Console.ResetColor();
        }
    }
}, cts.Token);

// 手动发送
while (true)
{
    var input = Console.ReadLine();
    if (input?.ToUpper() == "Q")
    {
        cts.Cancel();
        break;
    }

    if (!string.IsNullOrWhiteSpace(input))
    {
        try
        {
            await connection.InvokeAsync("SendMessageFromClient1", "Client1", input);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"发送失败: {ex.Message}");
            Console.ResetColor();
        }
    }
}

await connection.DisposeAsync();
Console.WriteLine("Client1 已退出。");
