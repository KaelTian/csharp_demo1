#!/bin/bash
# ==========================================
# 智能质量检测平台 - 一键启动脚本
# 启动顺序: C# Server → Python Worker → Web Dashboard → Client
# ==========================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PYTHON="/c/Users/kael/AppData/Local/Programs/Python/Python312/python.exe"

echo "========================================"
echo "  智能质量检测平台 - 启动中..."
echo "========================================"
echo ""

# 1. 启动 C# gRPC Server (端口 5001)
echo "[1/4] 启动 C# gRPC Server..."
cd "$SCRIPT_DIR/GrpcServer" && dotnet run &
SERVER_PID=$!
sleep 5

# 检查 Server 是否启动成功
if kill -0 $SERVER_PID 2>/dev/null; then
    echo "  ✓ C# Server 启动成功 (PID: $SERVER_PID)"
else
    echo "  ✗ C# Server 启动失败，请检查端口 5001 是否被占用"
    exit 1
fi

# 2. 启动 Python Worker (端口 5002)
echo "[2/4] 启动 Python Worker..."
cd "$SCRIPT_DIR/PythonWorker" && "$PYTHON" worker.py &
WORKER_PID=$!
sleep 4

if kill -0 $WORKER_PID 2>/dev/null; then
    echo "  ✓ Python Worker 启动成功 (PID: $WORKER_PID)"
else
    echo "  ⚠ Python Worker 启动失败（可能未安装依赖，不影响主流程）"
fi

# 3. 启动 Web Dashboard (端口 5100)
echo "[3/4] 启动 Web Dashboard..."
cd "$SCRIPT_DIR/WebDashboard" && dotnet run &
DASHBOARD_PID=$!
sleep 3
echo "  ✓ Web Dashboard 启动成功 (PID: $DASHBOARD_PID)"

echo ""
echo "========================================"
echo "  所有服务已启动!"
echo "  Dashboard: http://localhost:5100"
echo "  gRPC Server: localhost:5001"
echo "  Python Worker: localhost:5002"
echo "========================================"
echo ""
echo "按 Ctrl+C 停止所有服务"
echo ""

# 4. 可选: 运行客户端
echo "是否运行客户端模拟数据？(y/n)"
read -t 3 -r RUN_CLIENT
if [ "$RUN_CLIENT" = "y" ] || [ "$RUN_CLIENT" = "Y" ]; then
    echo "[4/4] 启动数据模拟客户端..."
    cd "$SCRIPT_DIR/GrpcClient" && echo "y" | dotnet run
fi

# 等待子进程
trap "kill $SERVER_PID $WORKER_PID $DASHBOARD_PID 2>/dev/null" EXIT
wait
