using Grpc.Core;
using Grpc.Net.Client;
using QualityInsight;

namespace GrpcServer.Services;

public class WorkerCoordinatorService : WorkerRegistry.WorkerRegistryBase
{
    private readonly DataStore _store;
    private readonly ILogger<WorkerCoordinatorService> _logger;

    public WorkerCoordinatorService(DataStore store, ILogger<WorkerCoordinatorService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public override Task<RegistrationResponse> RegisterWorker(WorkerInfo request, ServerCallContext context)
    {
        _store.RegisterWorker(request);
        _logger.LogInformation(
            "Worker 注册: {Id} ({Lang}@{Host}:{Port}), 能力: {Caps}",
            request.WorkerId, request.Language, request.Host, request.Port,
            string.Join(", ", request.Capabilities));

        return Task.FromResult(new RegistrationResponse
        {
            Accepted = true,
            ServerId = "quality-server-01",
            HeartbeatIntervalSeconds = 30
        });
    }

    public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        _store.UpdateHeartbeat(request.WorkerId);
        return Task.FromResult(new HeartbeatResponse
        {
            Acknowledged = true,
            HasPendingTasks = _store.GetUnprocessedProducts(1).Count > 0
        });
    }

    /// <summary>将数据派发给 Python Worker 处理</summary>
    public async Task<ProcessResponse?> DispatchToPythonAsync(
        List<ProductData> products, string analysisType, CancellationToken ct = default)
    {
        var workers = _store.GetRegisteredWorkers();
        var pythonWorker = workers.FirstOrDefault(w =>
            w.Language == "python" && w.Capabilities.Contains(analysisType));

        if (pythonWorker == null)
        {
            _logger.LogWarning("没有可用的 Python Worker 处理 {Type}", analysisType);
            return null;
        }

        try
        {
            var address = $"http://{pythonWorker.Host}:{pythonWorker.Port}";
            using var channel = GrpcChannel.ForAddress(address);
            var client = new DataProcessing.DataProcessingClient(channel);

            var request = new ProcessRequest
            {
                TaskId = Guid.NewGuid().ToString("N")[..12],
                WorkerId = pythonWorker.WorkerId,
                AnalysisType = analysisType
            };
            request.Products.AddRange(products);

            var response = await client.ProcessDataAsync(request, deadline: DateTime.UtcNow.AddSeconds(30));

            if (response.Success)
            {
                foreach (var result in response.Results)
                    _store.AddResult(result);
                foreach (var alert in response.Alerts)
                    _store.AddAlert(alert);

                _logger.LogInformation(
                    "Python 处理完成: {Summary}", response.Summary);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用 Python Worker 失败");
            return null;
        }
    }
}
