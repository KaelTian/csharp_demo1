using StackExchange.Redis;

namespace RedisStringDemo;

/// <summary>
/// Redis 连接管理（单例模式）
/// </summary>
public static class RedisConnection
{
    private static ConnectionMultiplexer? _conn;
    private static readonly object LockObj = new();

    /// <summary>
    /// 获取 ConnectionMultiplexer 实例
    /// </summary>
    /// <param name="connectionString">Redis 连接字符串，默认 localhost:6379</param>
    public static ConnectionMultiplexer GetConnection(string connectionString = "localhost:6379")
    {
        if (_conn is { IsConnected: true })
            return _conn;

        lock (LockObj)
        {
            if (_conn is { IsConnected: true })
                return _conn;

            _conn?.Dispose();
            _conn = ConnectionMultiplexer.Connect(connectionString);
        }

        return _conn;
    }

    /// <summary>
    /// 便捷获取 IDatabase
    /// </summary>
    public static IDatabase GetDatabase(int db = 0)
    {
        return GetConnection().GetDatabase(db);
    }

    /// <summary>
    /// 清空当前数据库的所有 key（仅供 Demo 演示用）
    /// </summary>
    public static void FlushDatabase(int db = 0)
    {
        var server = GetConnection().GetServer(GetConnection().GetEndPoints()[0]);
        server.FlushDatabase(db);
    }
}
