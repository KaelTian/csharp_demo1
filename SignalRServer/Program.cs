using SignalRServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.MapHub<DemoHub>("/hubs/demo");

Console.WriteLine("SignalR Server running on http://localhost:5060");
app.Run("http://0.0.0.0:5060");
