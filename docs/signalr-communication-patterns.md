# SignalR 双向实时通信 — 核心模式说明

## 一、架构概览

```
Client1 (.NET Console)          Client2 (Vue Browser)
       │                                │
       │ ① 发送消息                      │
       │──────►                         │
       │    Server (Hub)                │
       │       │                        │
       │       │ ② 广播给所有客户端      │
       │       │──────────────────────► │
       │       │                        │
       │       │ ③ Client2 反向发送      │
       │       │◄────────────────────── │
       │ ④ 转发给 Client1              │
       │◄──────                        │
```

## 二、Server 端 — Hub（消息中枢）

### 2.1 Hub 定义

继承 `Hub` 基类，每个公开 `async Task` 方法自动暴露为客户端可调用的 API。

```csharp
// SignalRServer/Hubs/DemoHub.cs
public class DemoHub : Hub { ... }
```

### 2.2 注册接收函数（Hub 方法）

Hub 中的公开方法 = 客户端可以 `InvokeAsync` 调用的端点：

```csharp
// Client1 调用的入口
public async Task SendMessageFromClient1(string clientName, string message)
{
    // 处理逻辑...
    await Clients.All.SendAsync("ReceiveMessage", clientName, message, "client1");
}

// Client2 调用的入口
public async Task SendMessageFromClient2(string clientName, string message)
{
    await Clients.All.SendAsync("ReceiveMessageFromClient2", clientName, message, "client2");
}
```

| 要素 | 说明 |
|------|------|
| **方法名** | 客户端通过 `connection.InvokeAsync("方法名", 参数...)` 调用 |
| **参数** | 由 SignalR 自动 JSON 序列化/反序列化，类型必须匹配 |
| **返回类型** | `Task` 即可，也可返回 `Task<T>` 让客户端获取返回值 |

### 2.3 广播消息

通过 `Clients` 属性向客户端推送消息：

| API | 作用 |
|-----|------|
| `Clients.All.SendAsync("方法名", 参数...)` | 广播给所有已连接客户端 |
| `Clients.Caller.SendAsync(...)` | 只发给调用者自己 |
| `Clients.Others.SendAsync(...)` | 发给除调用者外的所有人 |
| `Clients.Client(connectionId).SendAsync(...)` | 发给指定连接 |
| `Clients.Group("组名").SendAsync(...)` | 发给指定组 |

**关键点**：Server 没有 "注册监听" 的概念 — 它只有暴露的方法（供客户端调用）和 `Clients.SendAsync`（主动推给客户端）。

### 2.4 生命周期钩子

```csharp
public override async Task OnConnectedAsync()
{
    // 新客户端连入时触发
    await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
}

public override async Task OnDisconnectedAsync(Exception? exception)
{
    // 客户端断开时触发
}
```

### 2.5 启动配置

```csharp
// SignalRServer/Program.cs
builder.Services.AddSignalR();          // 注册 SignalR 服务
builder.Services.AddCors(...);           // 配置跨域（仅开发时需要）

app.MapHub<DemoHub>("/hubs/demo");       // 映射 Hub 端点
```

---

## 三、Client 端 — .NET Console（C#）

### 3.1 建立连接

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5060/hubs/demo")
    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), ... })
    .Build();

await connection.StartAsync();   // 手动启动（必须）
```

### 3.2 注册监听函数（接收 Server 推送）

使用 `connection.On<T1, T2, ...>("方法名", handler)`：

```csharp
// 监听 Server 推送的 ReceiveMessageFromClient2
connection.On<string, string, string>("ReceiveMessageFromClient2",
    (clientName, message, source) =>
{
    Console.WriteLine($"[{clientName}] {message}");
});

// 泛型参数个数必须与 Server 端 SendAsync 的参数个数一致
// 最后一个参数是回调委托（lambda）
```

| 要素 | 说明 |
|------|------|
| **"方法名"** | 必须匹配 Server 端 `SendAsync` 的第一个参数 |
| **泛型参数** | 声明每个参数的类型，顺序一一对应 |
| **回调** | 收到推送时执行，在 SignalR 内部线程上调用 |

### 3.3 发送消息给 Server

```csharp
await connection.InvokeAsync("SendMessageFromClient1", "Client1", message);
```

- 第一个参数 = Server Hub 中的方法名
- 后续参数 = 传给 Hub 方法的参数（自动 JSON 序列化）

### 3.4 重连机制（双层保障）

```csharp
// 第一层：内置自动重连（尝试 4 次后触发 Closed）
.WithAutomaticReconnect(new[] {
    TimeSpan.Zero,       // 立即重试
    TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(30)
});

// 第二层：手动无限重试（Closed 事件兜底）
connection.Closed += async (error) =>
{
    while (true)
    {
        await Task.Delay(5000);
        await connection.StartAsync();  // 重试启动
    }
};

// 状态通知
connection.Reconnecting  += _ => { /* 正在重连 */ };
connection.Reconnected   += _ => { /* 重连成功 */ };
connection.Closed        += _ => { /* 最终断开 */ };
```

---

## 四、Client 端 — Vue/JS（浏览器）

### 4.1 建立连接

```typescript
import * as signalR from '@microsoft/signalr'

const conn = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/demo', { withCredentials: false })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build()

await conn.start()
```

### 4.2 注册监听函数

```typescript
conn.on('ReceiveMessage', (clientName: string, message: string, source: string) => {
  // 处理收到的消息
})

conn.on('Connected', (connectionId: string) => {
  // 获取服务端分配的连接 ID
})
```

### 4.3 发送消息给 Server

```typescript
await connection.invoke('SendMessageFromClient2', 'Client2', inputMessage)
```

### 4.4 重连事件

```typescript
conn.onreconnecting(() => { /* UI 显示重连中 */ })
conn.onreconnected((id?) => { /* UI 显示已连接 */ })
conn.onclose(() => { /* UI 显示已断开 */ })
```

---

## 五、开发环境跨域（CORS）处理

### 方式一：Vite 代理（推荐）

```typescript
// vite.config.ts — 避免 CORS，浏览器请求走同源
server: {
  proxy: {
    '/hubs': {
      target: 'http://localhost:5060',
      ws: true        // 必须，支持 WebSocket 升级
    }
  }
}
```

客户端用相对路径：`/hubs/demo`

### 方式二：Server 端 CORS（直连时使用）

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

**注意**：`AllowCredentials()` 与浏览器跨域无凭据请求会冲突。不需要认证时不要加。

---

## 六、消息流完整链路示例

```
Client1 → Server → Client2（正向）
─────────────────────────────────────
Client1:  connection.InvokeAsync("SendMessageFromClient1", "Client1", "hello")
Server:   public async Task SendMessageFromClient1(string name, string msg)
              => Clients.All.SendAsync("ReceiveMessage", name, msg, "client1")
Client2:  conn.on("ReceiveMessage", (name, msg, source) => { ... })

Client2 → Server → Client1（反向）
─────────────────────────────────────
Client2:  connection.invoke("SendMessageFromClient2", "Client2", "hi")
Server:   public async Task SendMessageFromClient2(string name, string msg)
              => Clients.All.SendAsync("ReceiveMessageFromClient2", name, msg, "client2")
Client1:  connection.On<string,string,string>("ReceiveMessageFromClient2", ...)
```

## 七、注意事项

1. **方法名大小写**：Server Hub 方法名使用 PascalCase，客户端 Invoke 时大小写不敏感（SignalR 默认），但建议保持一致
2. **参数类型匹配**：`On<T>` 的泛型参数顺序/个数必须与 `SendAsync` 完全一致
3. **Hub 是瞬态的**：每次连接调用都会创建新的 Hub 实例，不要在 Hub 中存状态（应通过 Singleton 服务）
4. **重连后连接 ID 会变**：Server 端需要注意，旧 `ConnectionId` 已失效
5. **`connection.On` 要在 `StartAsync` 之前注册**：否则可能在注册前丢失消息
6. **Vue 中使用 `onUnmounted` 停止连接**：组件销毁时必须 `connection.stop()` 防止内存泄漏
