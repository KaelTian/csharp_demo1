using Grpc.Core;
using QualityInsight;

namespace GrpcServer.Services;

public class DataIngestionService : DataIngestion.DataIngestionBase
{
    private readonly DataStore _store;
    private readonly WorkerCoordinatorService _workerCoordinator;
    private readonly ILogger<DataIngestionService> _logger;

    public DataIngestionService(
        DataStore store,
        WorkerCoordinatorService workerCoordinator,
        ILogger<DataIngestionService> logger)
    {
        _store = store;
        _workerCoordinator = workerCoordinator;
        _logger = logger;
    }

    public override async Task<IngestionSummary> SubmitProductData(
        IAsyncStreamReader<ProductData> requestStream,
        ServerCallContext context)
    {
        var products = new List<ProductData>();
        await foreach (var product in requestStream.ReadAllAsync())
        {
            _store.AddProduct(product);
            products.Add(product);
            _logger.LogDebug("收到产品数据: {Id} 产线={Line}", product.ProductId, product.ProductionLine);
        }

        _logger.LogInformation("批量接收完成: 共{Count}条", products.Count);

        // 尝试派发给 Python Worker 做深度分析
        _ = Task.Run(async () =>
        {
            var result = await _workerCoordinator.DispatchToPythonAsync(products, "quality_check");
            if (result == null)
                _logger.LogInformation("Python Worker 不可用，使用规则引擎结果");
        });

        var failedCount = _store.GetRecentResults(products.Count).Count(r => !r.Passed);
        return new IngestionSummary
        {
            Success = true,
            ProductsReceived = products.Count,
            FailedCount = failedCount,
            Message = $"成功接收 {products.Count} 条产品数据"
        };
    }

    public override Task<IngestionResponse> SubmitSingleProduct(
        ProductData request,
        ServerCallContext context)
    {
        _store.AddProduct(request);
        _logger.LogInformation("单条产品数据: {Id}", request.ProductId);

        // 规则引擎初检
        var defects = new List<string>();
        if (request.Temperature is < 15 or > 35) defects.Add("温度异常");
        if (request.Pressure > 10) defects.Add("压力超限");
        if (request.Weight < 50 || request.Weight > 500) defects.Add("重量异常");

        var passed = defects.Count == 0;
        var result = new QualityResult
        {
            ProductId = request.ProductId,
            Passed = passed,
            QualityScore = passed ? 80 : Math.Max(0, 100 - defects.Count * 30),
            InspectedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Inspector = "rule-engine",
            ProductionLine = request.ProductionLine,
            AnomalyIndex = passed ? 0 : 0.5 + defects.Count * 0.2
        };
        result.Defects.AddRange(defects);
        _store.AddResult(result);

        foreach (var defect in defects)
        {
            _store.AddAlert(new AlertEvent
            {
                AlertId = Guid.NewGuid().ToString("N")[..12],
                ProductId = request.ProductId,
                ProductionLine = request.ProductionLine,
                Severity = "WARNING",
                AlertType = defect.Contains("温度") ? "temperature" :
                            defect.Contains("压力") ? "pressure" : "weight",
                Message = $"产品 {request.ProductId} {defect}: 当前值超出规格",
                Threshold = defect.Contains("温度") ? 35 :
                           defect.Contains("压力") ? 10 : 500,
                ActualValue = defect.Contains("温度") ? request.Temperature :
                             defect.Contains("压力") ? request.Pressure : request.Weight,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // 异步派发给 Python Worker 复核
        _ = Task.Run(async () =>
        {
            await _workerCoordinator.DispatchToPythonAsync(
                new List<ProductData> { request }, "anomaly_detection");
        });

        return Task.FromResult(new IngestionResponse
        {
            Success = true,
            Message = passed ? "规则初检通过" : $"发现缺陷: {string.Join(", ", defects)}"
        });
    }
}
