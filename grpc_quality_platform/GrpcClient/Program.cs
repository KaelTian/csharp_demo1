using Grpc.Net.Client;
using QualityInsight;

var rng = new Random(42);
var productionLines = new[] { "A线", "B线", "C线" };
var stations = new[] { "ST01", "ST02", "ST03", "ST04" };

var channel = GrpcChannel.ForAddress("http://localhost:5001");
var ingestionClient = new DataIngestion.DataIngestionClient(channel);

Console.WriteLine("=== 质量数据模拟客户端 ===");
Console.WriteLine("连接服务器 localhost:5001");
Console.WriteLine();

// ===== 1. 单条提交 =====
Console.WriteLine(">>> [1/3] 单条产品提交测试");
for (int i = 0; i < 3; i++)
{
    var product = GenerateProduct(rng, productionLines[i % 3], stations[i % 4]);
    var response = await ingestionClient.SubmitSingleProductAsync(product);
    Console.WriteLine($"    {product.ProductId}: {response.Message}");
}
Console.WriteLine();

// ===== 2. 批量流式提交 =====
Console.WriteLine(">>> [2/3] 批量流式提交测试 (20条)");
var productBatch = Enumerable.Range(0, 20)
    .Select(i => GenerateProduct(rng, productionLines[i % 3], stations[i % 4]))
    .ToList();

using var call = ingestionClient.SubmitProductData();
foreach (var product in productBatch)
    await call.RequestStream.WriteAsync(product);

await call.RequestStream.CompleteAsync();
var summary = await call.ResponseAsync;
Console.WriteLine($"    {summary.Message} (失败: {summary.FailedCount})");
Console.WriteLine();

// ===== 3. 异常数据模拟 =====
Console.WriteLine(">>> [3/3] 异常数据模拟提交");
var abnormalProducts = new List<ProductData>
{
    new()
    {
        ProductId = $"ABN-{rng.Next(1000, 9999)}", ProductionLine = "A线",
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Temperature = 48.5, Pressure = 3.2, Weight = 250,
        DimensionX = 100, DimensionY = 60, DimensionZ = 30, StationId = "ST01"
    },
    new()
    {
        ProductId = $"ABN-{rng.Next(1000, 9999)}", ProductionLine = "B线",
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Temperature = 25, Pressure = 15.8, Weight = 250,
        DimensionX = 100, DimensionY = 60, DimensionZ = 30, StationId = "ST02"
    },
    new()
    {
        ProductId = $"ABN-{rng.Next(1000, 9999)}", ProductionLine = "C线",
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Temperature = 22, Pressure = 4.0, Weight = 12.3,
        DimensionX = 200, DimensionY = 60, DimensionZ = 5, StationId = "ST03"
    }
};

foreach (var product in abnormalProducts)
{
    var response = await ingestionClient.SubmitSingleProductAsync(product);
    Console.WriteLine($"    {product.ProductId}: {response.Message}");
}

// ===== 查询仪表盘摘要 =====
var queryClient = new QualityQuery.QualityQueryClient(channel);
var dashboard = await queryClient.GetDashboardSummaryAsync(new DashboardQuery
{
    TimeRange = "all",
    IncludeAlerts = true
});

Console.WriteLine();
Console.WriteLine("=== 仪表盘摘要 ===");
Console.WriteLine($"总产品: {dashboard.TotalProducts} | 通过: {dashboard.PassedCount} | 失败: {dashboard.FailedCount}");
Console.WriteLine($"通过率: {dashboard.PassRate}% | 平均质量分: {dashboard.AvgQualityScore}");
Console.WriteLine($"主要缺陷: {dashboard.TopDefectType} | 活跃告警: {dashboard.ActiveAlerts}");

Console.WriteLine();
Console.WriteLine("按 Enter 退出...");
Console.ReadLine();

static ProductData GenerateProduct(Random rng, string line, string station)
{
    var isNormal = rng.NextDouble() > 0.1;
    return new ProductData
    {
        ProductId = $"P{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{rng.Next(100, 999)}",
        ProductionLine = line,
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Temperature = isNormal ? 25 + rng.NextDouble() * 8 - 4 : 40 + rng.NextDouble() * 10,
        Pressure = isNormal ? 3 + rng.NextDouble() * 4 : 11 + rng.NextDouble() * 5,
        DimensionX = 100 + rng.NextDouble() * 10 - 5,
        DimensionY = 60 + rng.NextDouble() * 6 - 3,
        DimensionZ = 30 + rng.NextDouble() * 4 - 2,
        Weight = isNormal ? 200 + rng.NextDouble() * 100 : 30 + rng.NextDouble() * 20,
        StationId = station
    };
}
