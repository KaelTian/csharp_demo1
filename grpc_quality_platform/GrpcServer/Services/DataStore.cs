using System.Collections.Concurrent;
using QualityInsight;

namespace GrpcServer.Services;

public class DataStore
{
    private readonly ConcurrentQueue<ProductData> _products = new();
    private readonly ConcurrentQueue<QualityResult> _results = new();
    private readonly ConcurrentQueue<AlertEvent> _alerts = new();
    private readonly ConcurrentDictionary<string, WorkerInfo> _workers = new();

    private readonly object _alertLock = new();
    private readonly List<AlertEvent> _alertList = new();

    public void AddProduct(ProductData p) => _products.Enqueue(p);

    public void AddResult(QualityResult r) => _results.Enqueue(r);

    public void AddAlert(AlertEvent a)
    {
        _alerts.Enqueue(a);
        lock (_alertLock) { _alertList.Add(a); }
    }

    public void RegisterWorker(WorkerInfo w) => _workers[w.WorkerId] = w;

    public List<WorkerInfo> GetRegisteredWorkers()
        => _workers.Values.ToList();

    public void UpdateHeartbeat(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var w))
            w.LastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public List<ProductData> GetRecentProducts(int count = 50)
        => _products.Reverse().Take(count).ToList();

    public List<ProductData> GetUnprocessedProducts(int count = 20)
        => _products.TakeLast(count).ToList();

    public List<QualityResult> GetRecentResults(int count = 50)
        => _results.Reverse().Take(count).ToList();

    public List<AlertEvent> GetRecentAlerts(int count = 20)
        => _alerts.Reverse().Take(count).ToList();

    public List<AlertEvent> GetAlerts(string? line = null, bool unackOnly = false)
    {
        lock (_alertLock)
        {
            var query = _alertList.AsEnumerable().Reverse();
            if (!string.IsNullOrEmpty(line))
                query = query.Where(a => a.ProductionLine == line);
            if (unackOnly)
                query = query.Where(a => !a.Acknowledged);
            return query.Take(50).ToList();
        }
    }

    public DashboardSummary BuildSummary(string? line = null)
    {
        var allResults = _results.Where(r => line == null || r.ProductionLine == line).ToList();
        var total = allResults.Count;
        var passed = allResults.Count(r => r.Passed);
        var failed = total - passed;

        var defectGroups = allResults.Where(r => !r.Passed)
            .SelectMany(r => r.Defects)
            .GroupBy(d => d)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var summary = new DashboardSummary
        {
            TotalProducts = total,
            PassedCount = passed,
            FailedCount = failed,
            PassRate = total > 0 ? Math.Round((double)passed / total * 100, 1) : 0,
            AvgQualityScore = total > 0 ? Math.Round(allResults.Average(r => r.QualityScore), 1) : 0,
            TopDefectType = defectGroups.Count > 0 ? defectGroups.MaxBy(kv => kv.Value).Key : "",
            ActiveAlerts = _alertList.Count(a => !a.Acknowledged),
            ReportTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        // 各产线通过率
        foreach (var g in _results.GroupBy(r => r.ProductionLine))
        {
            var gTotal = g.Count();
            summary.LineStats[g.Key] = gTotal > 0
                ? Math.Round((double)g.Count(r => r.Passed) / gTotal * 100, 1) : 0;
        }

        // 缺陷分布
        foreach (var kv in defectGroups)
            summary.DefectDistribution[kv.Key] = kv.Value;

        // 质量趋势
        var trend = _results.Reverse().Take(20).ToList();
        for (int i = 0; i < trend.Count; i++)
            summary.ScoreTrend[$"{trend[i].ProductId?[..Math.Min(8, trend[i].ProductId?.Length ?? 0)] ?? $"p{i}"}"] = trend[i].QualityScore;

        return summary;
    }

    public List<AlertEvent> GetUnreadAlertsSince(int lastCount)
        => _alertList.Skip(lastCount).ToList();
}
