#!/bin/bash
# 启动 C# gRPC Server
echo "=== 启动 C# gRPC Server (端口 5001) ==="
cd "$(dirname "$0")/GrpcServer"
dotnet run
