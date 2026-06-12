namespace RedisStringDemo;

/// <summary>
/// 模拟 MySQL 数据库。
/// 用内存字典模拟数据存储，并通过延迟模拟数据库查询耗时。
/// </summary>
public class FakeDatabase
{
    private readonly Dictionary<int, Product> _products = new()
    {
        { 1, new Product(1, "MacBook Pro 14", 14999, "高性能笔记本") },
        { 2, new Product(2, "iPhone 16 Pro", 9999, "旗舰手机") },
        { 3, new Product(3, "AirPods Pro 2", 1899, "降噪耳机") },
        { 4, new Product(4, "iPad Air", 4799, "平板电脑") },
    };

    private readonly Dictionary<string, int> _articleLikes = new()
    {
        { "article:1001", 0 },
        { "article:1002", 0 },
    };

    /// <summary>
    /// 模拟数据库查询——根据ID获取商品
    /// </summary>
    public async Task<Product?> GetProductAsync(int id)
    {
        // 模拟 MySQL 查询延迟（约 80-150ms）
        await Task.Delay(Random.Shared.Next(80, 150));
        return _products.TryGetValue(id, out var p) ? p : null;
    }

    /// <summary>
    /// 模拟数据库更新——修改商品名称
    /// </summary>
    public async Task UpdateProductNameAsync(int id, string newName)
    {
        await Task.Delay(Random.Shared.Next(50, 100));
        if (_products.TryGetValue(id, out var p))
        {
            _products[id] = p with { Name = newName };
        }
    }

    /// <summary>
    /// 模拟数据库——获取文章点赞数
    /// </summary>
    public int GetArticleLikes(string articleId)
    {
        return _articleLikes.GetValueOrDefault(articleId, 0);
    }

    /// <summary>
    /// 模拟数据库——更新点赞数
    /// </summary>
    public void SetArticleLikes(string articleId, int count)
    {
        _articleLikes[articleId] = count;
    }
}

/// <summary>
/// 商品模型（使用 record 确保不可变性）
/// </summary>
public record Product(int Id, string Name, decimal Price, string Description);
