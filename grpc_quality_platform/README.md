# 智能质量检测平台 - gRPC 跨语言示例

## 架构设计

```
┌──────────────────────────────────────────────────────────┐
│              Web Dashboard (localhost:5100)               │
│           Blazor/HTML + Chart.js + SSE 实时流             │
└──────────────────────────┬───────────────────────────────┘
                           │ HTTP REST / SSE
                           ▼
┌──────────────────────────────────────────────────────────┐
│              C# gRPC Server (localhost:5001)               │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ DataIngestion   → 接收产品检测数据                     │ │
│  │ WorkerRegistry  → Python Worker 注册/心跳             │ │
│  │ QualityQuery    → 仪表盘查询 + 流式告警               │ │
│  └──────────────────────────────────────────────────────┘ │
└──────┬──────────────────────────────┬───────────────────┘
       │ gRPC stream/bidi              │ gRPC (客户端调用)
       ▼                               ▼
┌──────────────────┐    ┌──────────────────────────────┐
│  C# Client        │    │  Python Worker (port 5002)   │
│  (传感器模拟器)   │    │  ├─ quality_check   → 规则质检 │
│  温度/压力/尺寸   │    │  ├─ anomaly_detection → IQR  │
│  批量流式提交     │    │  └─ statistics      → Z-Score │
└──────────────────┘    └──────────────────────────────┘
```

### 业务场景

智能工厂产线质量检测系统，模拟三条产线（A线/B线/C线）的产品检测数据流：

1. **C# 客户端** 模拟传感器，生成产品温度、压力、尺寸、重量等数据
2. **C# 服务端** 接收数据，做规则初检，并派发给 Python Worker
3. **Python Worker** 使用 numpy 做 IQR 异常检测、Z-Score 统计分析
4. **Web 仪表盘** 实时展示质量趋势、缺陷分布、产线通过率、告警流

### 跨语言通信

| 通信方向 | 协议 | 方式 |
|---------|------|------|
| C# Client → C# Server | gRPC | 双向流 / 单向 |
| C# Server → Python Worker | gRPC | 单向调用 |
| Python Worker → C# Server | gRPC | 注册/心跳 |
| Web Dashboard → C# Server | REST / SSE | HTTP 代理 gRPC |

## 技术栈

- **C#** (.NET 8.0) - gRPC Server/Client + Web Dashboard
- **Python** (3.12+) - numpy 数据处理引擎
- **Proto** - gRPC 协议定义
- **Chart.js** - 前端可视化
- **SSE** - 实时告警推送

## 快速启动

### 1. 安装依赖

```bash
# Python 依赖
pip install grpcio grpcio-tools numpy

# .NET 依赖 (NuGet 自动还原)
dotnet restore GrpcServer/GrpcServer.csproj
dotnet restore GrpcClient/GrpcClient.csproj
dotnet restore WebDashboard/WebDashboard.csproj
```

### 2. 启动服务（依次启动）

**终端 1 - C# gRPC Server:**
```bash
cd GrpcServer
dotnet run
# 监听: http://localhost:5001
```

**终端 2 - Python Worker:**
```bash
cd PythonWorker
python worker.py
# 监听: http://localhost:5002
# 自动注册到 C# Server
```

**终端 3 - Web Dashboard:**
```bash
cd WebDashboard
dotnet run
# 访问: http://localhost:5100
```

**终端 4 - C# 数据模拟客户端:**
```bash
cd GrpcClient
dotnet run
# 发送 26 条产品数据（含异常数据）
```

### 3. 一键启动

```bash
bash run_all.sh
```

## 项目结构

```
grpc_quality_platform/
├── protos/
│   └── qualityinsight.proto    # gRPC 协议定义（跨语言契约）
├── GrpcServer/                  # C# gRPC 服务端
│   ├── Services/
│   │   ├── DataIngestionService.cs    # 数据接入服务
│   │   ├── WorkerCoordinatorService.cs # Worker 注册 & 派发
│   │   ├── QueryService.cs            # 仪表盘查询服务
│   │   └── DataStore.cs               # 内存数据存储
│   └── Program.cs
├── GrpcClient/                  # C# 数据模拟客户端
│   └── Program.cs               # 传感器数据生成 & 提交
├── PythonWorker/                # Python 数据处理服务
│   ├── worker.py                # gRPC Worker 主程序
│   ├── quality_engine.py        # 质量检测引擎 (numpy)
│   └── requirements.txt
├── WebDashboard/                # Web 仪表盘
│   ├── Program.cs               # REST API + SSE 代理
│   └── wwwroot/index.html       # 前端 Dashboard
├── start_server.sh / start_worker.sh / start_dashboard.sh / start_client.sh
└── run_all.sh                   # 一键启动脚本
```

## Proto 协议

```protobuf
service DataIngestion {
  rpc SubmitProductData (stream ProductData) returns (IngestionSummary);  // 双向流
  rpc SubmitSingleProduct (ProductData) returns (IngestionResponse);      // 单向
}

service WorkerRegistry {
  rpc RegisterWorker (WorkerInfo) returns (RegistrationResponse);  // Worker 注册
  rpc Heartbeat (HeartbeatRequest) returns (HeartbeatResponse);    // 心跳
}

service DataProcessing {
  rpc ProcessData (ProcessRequest) returns (ProcessResponse);      // 数据处理
}

service QualityQuery {
  rpc GetDashboardSummary (DashboardQuery) returns (DashboardSummary);  // 查询
  rpc StreamAlerts (AlertFilter) returns (stream AlertEvent);           // 流式告警
}
```

## 质量检测能力

| 引擎 | 方法 | 技术 |
|------|------|------|
| 规则引擎 (C#) | 基础阈值检测 | 温度15-35°C, 压力≤10MPa, 重量50-500g |
| Python quality_check | 全维度质检 | 温度/压力/重量/尺寸偏差检测 |
| Python anomaly_detection | IQR 异常检测 | 四分位距法识别离群值 |
| Python statistics | Z-Score 分析 | 基于规格的统计偏离度分析 |
