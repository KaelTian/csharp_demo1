using RedisStringDemo.Scenarios;

namespace RedisStringDemo;

/// <summary>
/// Redis String 类型典型场景演示程序
///
/// 使用前请确保 Redis 已启动（默认 localhost:6379）
/// 安装 Redis: https://redis.io/download 或使用 Docker:
///   docker run --name redis -p 6379:6379 -d redis
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Redis String 典型场景演示";

        // 启动时检查 Redis 连接
        try
        {
            var conn = RedisConnection.GetConnection();
            Console.WriteLine("✅ Redis 连接成功: {0}", conn.GetEndPoints()[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Redis 连接失败: {0}", ex.Message);
            Console.WriteLine("   请确保 Redis 已启动：");
            Console.WriteLine("   docker run --name redis -p 6379:6379 -d redis");
            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
            return;
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════╗");
            Console.WriteLine("║           Redis 数据类型 · 典型场景演示      ║");
            Console.WriteLine("╠═══════════════════════════════════════════════╣");
            Console.WriteLine("║  STRING · HASH · LIST · SET · SORTED SET     ║");
            Console.WriteLine("╚═══════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  [1] Cache-Aside 缓存穿透保护");
            Console.WriteLine("      └─ Redis + 模拟数据库，GET/SET/SETEX/EXISTS/DEL");
            Console.WriteLine();
            Console.WriteLine("  [2] 原子计数器（播放量 / 点赞）");
            Console.WriteLine("      └─ 并发 INCR/DECR/INCRBY/GETSET");
            Console.WriteLine();
            Console.WriteLine("  [3] 分布式全局ID生成器");
            Console.WriteLine("      └─ INCR 生成订单号/用户ID");
            Console.WriteLine();
            Console.WriteLine("  [4] 对象缓存（Session / 配置）");
            Console.WriteLine("      └─ JSON 序列化 + SETEX/TTL");
            Console.WriteLine();
            Console.WriteLine("  [5] 接口限流（固定窗口）");
            Console.WriteLine("      └─ INCR + EXPIRE 控制请求频率");
            Console.WriteLine();
            Console.WriteLine("  [6] 位图应用（签到 / 活跃统计）");
            Console.WriteLine("      └─ SETBIT/GETBIT/BITCOUNT/BITOP");
            Console.WriteLine();
            Console.WriteLine("  [H] Hash 类型");
            Console.WriteLine("      └─ 用户信息/购物车/文章缓存/对象映射对比");
            Console.WriteLine();
            Console.WriteLine("  [L] List 类型");
            Console.WriteLine("      └─ 栈/队列/有限集合(LTRIM)/分页/阻塞消息队列");
            Console.WriteLine();
            Console.WriteLine("  [S] Set 类型（集合）");
            Console.WriteLine("      └─ 标签系统/社交关系/抽奖去重/UV统计/集合运算");
            Console.WriteLine();
            Console.WriteLine("  [Z] Sorted Set 类型（有序集合）");
            Console.WriteLine("      └─ 排行榜/热榜/延迟队列/滑动窗口限流");
            Console.WriteLine();
            Console.WriteLine("  [Q] 退出");
            Console.WriteLine();
            Console.Write("请选择 [1-6/H/L/S/Z/Q]: ");

            var choice = Console.ReadKey(true).KeyChar;

            try
            {
                switch (choice)
                {
                    case '1': await CacheAsideDemo.RunAsync(); break;
                    case '2': await AtomicCounterDemo.RunAsync(); break;
                    case '3': await DistributedIdDemo.RunAsync(); break;
                    case '4': await ObjectCacheDemo.RunAsync(); break;
                    case '5': await RateLimiterDemo.RunAsync(); break;
                    case '6': await BitmapDemo.RunAsync(); break;
                    case 'h':
                    case 'H': await HashDemo.RunAsync(); break;
                    case 'l':
                    case 'L': await ListDemo.RunAsync(); break;
                    case 's':
                    case 'S': await SetDemo.RunAsync(); break;
                    case 'z':
                    case 'Z': await SortedSetDemo.RunAsync(); break;
                    case 'q':
                    case 'Q': return;
                    default:
                        Console.WriteLine("无效选择，请重试");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行出错: {ex.Message}");
            }

            if (choice is (>= '1' and <= '6') or 'h' or 'H' or 'l' or 'L' or 's' or 'S' or 'z' or 'Z')
            {
                Console.WriteLine("按任意键返回主菜单...");
                Console.ReadKey(true);
            }
        }
    }
}
