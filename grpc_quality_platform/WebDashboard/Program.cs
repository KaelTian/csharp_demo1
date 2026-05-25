using Grpc.Core;
using Grpc.Net.Client;
using QualityInsight;

var builder = WebApplication.CreateBuilder(args);

// gRPC 通道
var channel = GrpcChannel.ForAddress("http://localhost:5001");
var queryClient = new QualityQuery.QualityQueryClient(channel);

builder.Services.AddSingleton(queryClient);
builder.Services.AddCors();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

// REST API - 代理 gRPC 查询给前端
app.MapGet("/api/dashboard", async (string? line = null) =>
{
    var summary = await queryClient.GetDashboardSummaryAsync(new DashboardQuery
    {
        ProductionLine = line ?? "",
        TimeRange = "all",
        IncludeAlerts = true
    });
    return Results.Json(summary);
});

// REST API - 流式告警 (SSE)
app.MapGet("/api/alerts/stream", async (HttpContext ctx, string? line = null) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    using var alertsStream = queryClient.StreamAlerts(new AlertFilter
    {
        ProductionLine = line ?? "",
        UnacknowledgedOnly = false
    });

    await foreach (var alert in alertsStream.ResponseStream.ReadAllAsync())
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            alert.AlertId,
            alert.ProductId,
            alert.ProductionLine,
            alert.Severity,
            alert.AlertType,
            alert.Message,
            alert.Threshold,
            alert.ActualValue,
            alert.CreatedAt,
        });
        await ctx.Response.WriteAsync($"data: {json}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
});

app.Run("http://localhost:5100");
