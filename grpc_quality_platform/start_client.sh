#!/bin/bash
# 启动 C# 模拟客户端
echo "=== 启动 C# 数据模拟客户端 ==="
cd "$(dirname "$0")/GrpcClient"
dotnet run
