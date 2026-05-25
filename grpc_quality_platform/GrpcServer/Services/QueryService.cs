using Grpc.Core;
using QualityInsight;

namespace GrpcServer.Services;

public class QueryService : QualityQuery.QualityQueryBase
{
    private readonly DataStore _store;
    private readonly ILogger<QueryService> _logger;

    public QueryService(DataStore store, ILogger<QueryService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public override Task<DashboardSummary> GetDashboardSummary(
        DashboardQuery request, ServerCallContext context)
    {
        var line = string.IsNullOrEmpty(request.ProductionLine) ? null : request.ProductionLine;
        var summary = _store.BuildSummary(line);

        // 添加近期告警
        summary.RecentAlerts.AddRange(
            _store.GetAlerts(request.ProductionLine, false).Take(10));

        _logger.LogInformation(
            "仪表盘查询: 产线={Line}, 范围={Range}, 产品总数={Total}",
            request.ProductionLine, request.TimeRange, summary.TotalProducts);

        return Task.FromResult(summary);
    }

    public override async Task StreamAlerts(
        AlertFilter request,
        IServerStreamWriter<AlertEvent> responseStream,
        ServerCallContext context)
    {
        var lastAlertCount = 0;
        _logger.LogInformation("告警流订阅开始: 产线={Line}", request.ProductionLine);

        // 先发送已有告警
        var existingAlerts = _store.GetRecentAlerts(5);
        foreach (var alert in existingAlerts)
            await responseStream.WriteAsync(alert);

        // 持续推送新告警 (300秒超时)
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(300));

            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(2000, cts.Token);

                var currentCount = _store.GetRecentAlerts(100).Count;
                if (currentCount > lastAlertCount)
                {
                    var newAlerts = _store.GetAlerts(request.ProductionLine, false)
                        .Take(currentCount - lastAlertCount);

                    foreach (var alert in newAlerts)
                    {
                        await responseStream.WriteAsync(alert);
                    }
                    lastAlertCount = currentCount;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("告警流订阅结束(超时)");
        }
    }
}
