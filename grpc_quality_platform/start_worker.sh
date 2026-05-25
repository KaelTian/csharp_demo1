#!/bin/bash
# 启动 Python Worker (端口 5002)
echo "=== 启动 Python Worker (端口 5002) ==="
cd "$(dirname "$0")/PythonWorker"
/c/Users/kael/AppData/Local/Programs/Python/Python312/python.exe worker.py
