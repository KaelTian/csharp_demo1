#!/bin/bash
# 启动 Web Dashboard (端口 5100)
echo "=== 启动 Web Dashboard (端口 5100) ==="
cd "$(dirname "$0")/WebDashboard"
dotnet run
