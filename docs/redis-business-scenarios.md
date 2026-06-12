# Redis 业务场景实战手册

> 缓存只是 Redis 最表象的能力。选 Redis 不是因为"它能缓存"，而是因为它能**以特定的数据结构 + 原子操作 + 内存级速度**解决特定业务问题。
> 本文档基于 RedisStringDemo 项目的实际代码示例展开，涵盖所有 5 种核心数据类型。

---

## 目录

1. [Redis 的核心竞争力](#1-redis-的核心竞争力)
2. [String 场景](#2-string-场景)
3. [Hash 场景](#3-hash-场景)
4. [List 场景](#4-list-场景)
5. [Set 场景](#5-set-场景)
6. [Sorted Set 场景](#6-sorted-set-场景)
7. [综合模式：多类型配合](#7-综合模式多类型配合)
8. [选型决策：什么时候用 Redis](#8-选型决策什么时候用-redis)

---

## 1. Redis 的核心竞争力

和 Memcached、本地内存缓存、数据库相比，Redis 不可替代的能力：

| 能力 | 为什么重要 | 对手做不到的 |
|------|-----------|-------------|
| **丰富的数据结构** | 不只是存字符串，List/Set/Sorted Set/Hash 每种结构对应一种业务模式 | Memcached 只有 KV |
| **原子操作** | INCR/DECR 单线程执行，天然无并发问题 | DB 需要行锁/事务 |
| **阻塞原语** | BLPOP/BRPOP 实现生产-消费者模式 | 轮询方案浪费资源 |
| **TTL 精准控制** | 每个 key 独立过期，自动清理 | 内存缓存需要手动清理 |
| **单线程 + 多路复用** | 无锁竞争，IO 吞吐极高 | DB 连接池是瓶颈 |
| **内存速度** | 微秒级响应（~0.1ms）vs DB（~10ms） | 差 2 个数量级 |

**核心结论：** Redis 解决的从来不是"存数据"的问题，而是"在特定数据结构上用原子操作实时处理数据"的问题。

---

## 2. String 场景

### 2.1 Cache-Aside 缓存穿透保护

**业务问题：** 数据库查询太慢（~100ms），高并发下扛不住，需要保护 DB。

**为什么用 Redis 而不是 Memcached：**
- TTL 精准控制：每个 key 独立过期时间
- 数据结构灵活：value 可以是 JSON、数字、二进制
- 序列化自由：存什么应用层自己决定

**模式：**
```
请求 → GET key
  ├─ 命中 → 直接返回（~0.1ms）
  └─ 未命中 → 查 DB → SETEX key value TTL → 返回
更新 → 写 DB → DEL key（下次查询重新加载）
```

**适用场景：** 商品详情页、用户信息、配置信息、接口聚合数据

### 2.2 原子计数器

**业务问题：** 视频播放量、文章点赞，高并发下 DB 行锁造成性能瓶颈。

**为什么用 Redis 而不是 DB：**
- INCR 是原子操作，QPS 10万+ 不丢计数
- DB 的 UPDATE counter = counter + 1 需要行锁，高并发下锁竞争严重
- DB 写放大：一行计数更新会引起 B+ 树索引变更、binlog 写入

**模式：**
```
视频播放：INCR video:{id}:views       → 原子 +1
文章点赞：INCR article:{id}:likes      → 原子 +1
取消点赞：DECR article:{id}:likes      → 原子 -1
批量加：  INCRBY live:{id}:heat 100    → 原子 +100
定时持久化：GET count → 写 DB → GETSET count 0  → 原子重置
```

**典型业务：** 视频播放量、文章点赞/收藏、直播热度、库存扣减、签到积分

### 2.3 分布式全局 ID

**业务问题：** 分库分表后 DB 自增 ID 不可用，需要全局唯一 ID。

**为什么用 Redis 而不是雪花算法：**
- 实现极简：INCR 一行命令
- ID 按业务前缀分组，可读性强
- 不依赖机器时钟（雪花算法有时钟回拨问题）

**模式：**
```
订单号：INCR id:seq:order:{yyyyMMdd} → ORD2026061200001
用户ID：INCR global:uid:seq          → 10001, 10002, ...
```

### 2.4 位图应用

**业务问题：** 用户签到/活跃统计，传统 DB 存几十亿条记录不可行。

**为什么用 Redis 而不是 DB：**
- 1 个位 = 1 个状态，365 天只需 46 字节
- BITOP 支持位运算（AND/OR/XOR），直接在 Redis 内完成留存分析
- 如果用 DB 存签到记录：每天一条，一亿用户就是 365 亿条

**模式：**
```
签到：     SETBIT checkin:user:{id}:{yyyMM} {day-1} 1
统计天数： BITCOUNT checkin:user:{id}:{yyyMM}
留存分析： BITOP AND retain dau:0601 dau:0602
DAU 统计： BITCOUNT dau:{yyyyMMdd}
```

**典型业务：** 用户签到、日活统计、留存分析、在线状态

### 2.5 分布式锁

**业务问题：** 多个实例抢同一资源，需要分布式互斥。

**模式：**
```
SET lock:{resource} {uuid} NX EX 30
  → NX：不存在时才设置（互斥）
  → EX 30：自动释放（防死锁）
释放：if GET lock:{resource} == {uuid} → DEL lock:{resource}
```

**典型业务：** 定时任务防重复执行、秒杀库存、分布式调度

---

## 3. Hash 场景

### 3.1 对象字段级读写

**业务问题：** 用户信息/商品信息有几十个字段，修改一个字段不需要读写全部。

**为什么用 Redis String 做不到：**
- String JSON：修改一个字段需要 GET → 反序列化 → 修改 → 序列化 → SET
- Hash：HSET user:1001 score 16000 → 一次网络 IO 改一个字段
- 网络传输量：JSON 全量 vs 单个 field

**模式：**
```
写入：HSET user:{id} name "张三" age "28" city "北京"
读取：HGET user:{id} name           → 单个字段
批量：HMGET user:{id} name city     → 多个字段
全部：HGETALL user:{id}             → 所有字段
递增：HINCRBY user:{id} score 1000  → 原子操作单字段
初始化：HSETNX user:{id} name "李四" → 不存在才写入（防覆盖）
```

**适用场景：** 用户资料、商品信息、配置项、游戏角色属性

### 3.2 购物车

**业务问题：** 每个用户的购物车，商品ID → 数量，频繁增删改。

**为什么用 Hash 而不是 List：**
- HSET cart:{uid} {skuId} {count} → 同一商品自动覆盖，无需去重
- HINCRBY cart:{uid} {skuId} 1 → 原子修改数量
- HDEL cart:{uid} {skuId} → 删除指定商品
- HLEN cart:{uid} → 商品种类数

---

## 4. List 场景

### 4.1 消息队列（生产者-消费者）

**业务问题：** 异步处理任务（发邮件、生成图片、处理订单），需要解耦。

**为什么用 Redis 而不是 Kafka/RabbitMQ：**
- 零运维：Redis 已经在用，不需要额外搭建消息队列集群
- 阻塞原生支持：BLPOP 空队列时不占 CPU
- 适合轻量级任务队列，不需要消息堆积到百万级

> **适用边界：** 每日几万到几十万消息，不需要消息回溯、死信队列、持久化到磁盘等高级特性时，Redis List 是最轻量的选择。

**模式：**
```
生产者：RPUSH queue:tasks {taskData}
消费者：BLPOP queue:tasks timeout
  → 有消息立即返回，没消息阻塞等待
可靠处理：RPOPLPUSH queue:processing queue:backup
  → 取出消息同时备份，处理失败可从 backup 恢复
```

### 4.2 有限集合

**业务问题：** 只保留最新的 N 条记录（最近浏览、操作日志、通知）。

**为什么用 Redis 而不是 DB：**
- LTRIM 直接在 Redis 内裁剪，无需应用程序处理淘汰逻辑
- 内存级操作，毫秒级完成

**模式：**
```
每一次操作：
  LPUSH list:{key} {value}
  LTRIM list:{key} 0 99    → 只保留最新 100 条
  // 两步加起来 ~0.2ms，不需要任何后台清理任务
```

**适用场景：** 最近浏览、操作日志、消息通知、实时监控数据

### 4.3 栈与队列

**业务问题：** 需要后进先出（撤销操作）或先进先出（任务调度）。

**模式：**
```
栈（LIFO）：RPUSH + RPOP  → 浏览器回退历史、撤销操作
队列（FIFO）：RPUSH + LPOP → 任务调度、请求排队
```

---

## 5. Set 场景

### 5.1 标签系统

**业务问题：** 给内容打标签，通过标签筛选内容，找共同标签。

**为什么用 Redis Set 而不是 DB 的 IN 查询：**
- SINTER 计算多个集合的交集，直接在 Redis 内完成，不需要多次 SQL JOIN
- 一个标签一个 Set，百万级成员的集合运算也在毫秒级

**模式：**
```
为文章打标签：SADD article:5001:tags "C#" ".NET" "Redis"
通过标签找文章：SADD tag:C#:articles 5001 5002
共同标签：SINTER article:5001:tags article:5002:tags
标签合并：SUNION article:5001:tags article:5002:tags
差异标签：SDIFF article:5001:tags article:5002:tags
```

### 5.2 社交关系

**业务问题：** 关注/粉丝、共同关注、你可能认识的人。

**为什么用 Redis Set 而不是 DB：**
- SINTER（共同关注）直接出结果，DB 需要 JOIN 两张大表
- 关系链数据读写频繁且数据结构简单，DB 的 ACID 优势用不上

**模式：**
```
关注：  SADD user:{id}:following {targetId}
粉丝：  SADD user:{id}:followers {followerId}
共同关注：SINTER user:1001:following user:1002:following
推荐：  SDIFF user:1002:following user:1001:following
关注数：SCARD user:1001:following
取关：  SREM user:1001:following {targetId}
```

### 5.3 抽奖去重

**业务问题：** 一人只能参与一次，可重复抽但不可重复中奖。

**为什么用 Set：**
- SADD 天然去重，同一个人登记多次只算一次
- SRANDMEMBER 随机抽（不中奖不走）
- SPOP 随机抽并移除（中奖者离开奖池）
- 全原子操作，无需业务层加锁

### 5.4 UV 统计

**业务问题：** 统计独立访客，同一个人一天只算一次。

**模式：**
```
访问：SADD uv:{pageId}:{yyyMMdd} {ip/userId}
统计：SCARD uv:{pageId}:{yyyMMdd}
```

> **注：** 亿级 UV 用 HyperLogLog（误差 ~0.81%，12KB 内存），百万级以下 UV 用 Set 更简单准确。

---

## 6. Sorted Set 场景

### 6.1 排行榜

**业务问题：** 积分榜、热销榜、热搜榜，需要实时更新实时排序。

**为什么用 Redis 而不是 DB：**
- DB 的 ORDER BY score LIMIT 10，每次全表排序，O(NlogN)
- Sorted Set 的 skiplist 结构，Top N 查询 O(logN)
- ZINCRBY 原子更新分值，不需要事务保护

**模式：**
```
添加/更新：ZADD leaderboard:weekly {playerId} {score}
原子加分：ZINCRBY leaderboard:weekly {playerId} {increment}
Top N：  ZREVRANGE leaderboard:weekly 0 9 WITHSCORES
查排名： ZREVRANK leaderboard:weekly {playerId}  → 第几名
查积分： ZSCORE  leaderboard:weekly {playerId}   → 多少分
```

### 6.2 延迟队列

**业务问题：** 订单 30 分钟未支付自动取消、定时任务调度。

**为什么用 Redis 而不是定时任务轮询 DB：**
- 定时任务扫 DB 表：`SELECT * FROM orders WHERE status='pending' AND create_time < NOW() - 30min` → 扫全表，频率高时 IO 压力大
- Sorted Set：用时间戳做 score，ZRANGEBYSCORE 0 {currentTimestamp} 取到期任务 → O(logN)

**模式：**
```
入队：ZADD delay:orders {timestamp} {orderId}
              ↑ 未来的时间戳作为 score

轮询：ZRANGEBYSCORE delay:orders 0 {currentTimestamp}
       → 拿到所有到期任务
       ZREMRANGEBYSCORE delay:orders 0 {currentTimestamp}
       → 移除已处理的任务
```

### 6.3 滑动窗口限流

**业务问题：** 限制 1 分钟内最多 5 次请求，窗口滑动精准控制。

**为什么用固定窗口（INCR + EXPIRE）不够：**
- 固定窗口有边界问题：59 秒和第 61 秒各发 5 次请求，都能通过
- 滑动窗口用时间戳做 score，精确到毫秒

**模式：**
```
每次请求：
  1. ZREMRANGEBYSCORE key 0 {当前时间戳 - 窗口大小}
     → 清理窗口外的过期记录
  2. ZCARD key → 统计窗口内请求数
  3. if count < 阈值 → ZADD key {当前时间戳} {唯一标识}
     else → 拒绝请求
```

---

## 7. 综合模式：多类型配合

实际业务中往往是多类型配合使用，例如一个电商系统的商品详情页：

```
商品信息     → Hash   → product:{id}               字段级读写
商品浏览量   → String → product:{id}:views         原子计数
商品标签     → Set    → product:{id}:tags           标签系统
最近浏览     → List   → recent:views:{userId}       LTRIM 保留最近 100 条
热销排行     → ZSet   → hot:products                热度排序
商品评论排队  → List   → queue:comments:{id}         BLPOP 异步处理
用户签到     → String → checkin:user:{id}:{yyyMM}   位图
```

---

## 8. 选型决策：什么时候用 Redis

### 应该用 Redis 的场景

| 特征 | 原因 | 示例 |
|------|------|------|
| 数据结构匹配 | Redis 有现成的数据结构 | List → 队列，ZSet → 排行 |
| 需要原子操作 | INCR/SINTER 等直接在服务端完成 | 计数器、集合运算 |
| 高吞吐低延迟 | 微秒级响应 | 实时排行、秒杀 |
| 数据有过期时间 | TTL 自动清理 | Session、验证码 |
| 轻量级消息传递 | 不需要 Kafka 级别的可靠性 | 任务队列、通知 |

### 不应该用 Redis 的场景

| 场景 | 原因 | 替代方案 |
|------|------|---------|
| 复杂查询/多条件筛选 | Redis 没有查询引擎，只能按 key 查 | Elasticsearch、MySQL |
| 强事务/多行关联 | Redis 事务是乐观的，没有回滚 | MySQL/PostgreSQL |
| 数据量远超内存 | Redis 是内存数据库，成本高 | SSD 缓存 + DB |
| 消息堆积百万级 | List 接近上限时性能下降 | Kafka/Pulsar |
| 需要复杂聚合查询 | GROUP BY/HAVING 等 SQL 能力 | ClickHouse/Doris |

### Redis 的正确使用方式

```
    对单个 key 的原子操作 + 微秒响应 → 这就是 Redis 的甜区
    ─────────────────────────────────────
    读写分离的复杂查询                  → 用 ES
    多行事务的强一致性                  → 用 MySQL
    海量日志的离线分析                  → 用 ClickHouse
    百万级消息堆积                      → 用 Kafka
    它们各司其职，Redis 不解决所有问题
```

---

## 附录：数据结构速查

| 类型 | 底层实现 | 读写复杂度 | 核心能力 | 一句话总结 |
|------|---------|-----------|---------|-----------|
| String | 动态字符串/SDS | O(1) | 原子计数/位图/缓存 | 最基础，但位图和 INCR 是独有能力 |
| Hash | ziplist / hashtable | O(1) field 级 | 对象字段级读写 | 改一个字段不用传整个 JSON |
| List | quicklist | O(1) 头尾 | 两端操作/阻塞 | 唯一支持阻塞弹出的结构 |
| Set | intset / hashtable | O(1) 增删查 | 唯一性/集合运算 | 交并差直接计算 |
| Sorted Set | skiplist + hashtable | O(logN) | 排序/范围查询 | 排行榜的唯一选择 |
