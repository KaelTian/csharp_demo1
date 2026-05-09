using Microsoft.AspNetCore.SignalR;

namespace SignalRServer.Hubs;

public class DemoHub : Hub
{
    private readonly ILogger<DemoHub> _logger;

    public DemoHub(ILogger<DemoHub> logger)
    {
        _logger = logger;
    }

    public async Task SendMessageFromClient1(string clientName, string message)
    {
        _logger.LogInformation("[Client1 -> All] {Name}: {Message}", clientName, message);
        await Clients.All.SendAsync("ReceiveMessage", clientName, message, "client1");
    }

    public async Task SendMessageFromClient2(string clientName, string message)
    {
        _logger.LogInformation("[Client2 -> All] {Name}: {Message}", clientName, message);
        await Clients.All.SendAsync("ReceiveMessageFromClient2", clientName, message, "client2");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Error: {Error}",
            Context.ConnectionId, exception?.Message ?? "none");
        await base.OnDisconnectedAsync(exception);
    }
}
