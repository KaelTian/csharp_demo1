"""
Python Worker Service - 跨语言 gRPC 质量检测服务

工作模式:
  1. 启动 gRPC Server (DataProcessing service)
  2. 向 C# gRPC Server 注册
  3. 接收处理任务，使用 numpy 进行质检分析
  4. 发送心跳保持连接
"""

import uuid
import threading
from concurrent import futures
from datetime import datetime, timezone

import grpc
from quality_engine import QualityEngine

# 桩代码由 protoc 从 qualityinsight.proto 生成，预提交到仓库
# 重新生成: python -m grpc_tools.protoc -I../protos --python_out=. --grpc_python_out=. ../protos/qualityinsight.proto
from qualityinsight_pb2 import (
    QualityResult, AlertEvent, ProcessResponse,
    WorkerInfo, HeartbeatRequest,
)
from qualityinsight_pb2_grpc import (
    DataProcessingServicer as DataProcessingServicerBase,
    add_DataProcessingServicer_to_server,
    WorkerRegistryStub,
)


class DataProcessingServicer(DataProcessingServicerBase):
    """DataProcessing gRPC 服务端 - 由 C# Server 调用"""

    def __init__(self):
        super().__init__()
        self.engine = QualityEngine()
        self.worker_id = f"python-worker-{uuid.uuid4().hex[:8]}"

    def ProcessData(self, request, context):
        """处理质检任务 (C# Server -> Python Worker)"""
        print(f"\n[任务] {request.task_id} | 类型: {request.analysis_type} | 数据: {len(request.products)}条")

        # 提取产品数据
        products = [
            {
                'product_id': p.product_id,
                'production_line': p.production_line,
                'timestamp': p.timestamp,
                'temperature': p.temperature,
                'pressure': p.pressure,
                'dimension_x': p.dimension_x,
                'dimension_y': p.dimension_y,
                'dimension_z': p.dimension_z,
                'weight': p.weight,
                'station_id': p.station_id,
            }
            for p in request.products
        ]

        # 调用质检引擎
        engine_map = {
            'quality_check': self.engine.quality_check,
            'anomaly_detection': self.engine.anomaly_detection,
            'statistics': self.engine.statistics,
        }
        raw_results = engine_map.get(request.analysis_type, self.engine.quality_check)(products)

        # 组装响应
        passed = sum(1 for r in raw_results if r['passed'])
        avg_score = sum(r['quality_score'] for r in raw_results) / len(raw_results) if raw_results else 0
        response = ProcessResponse(
            task_id=request.task_id,
            worker_id=self.worker_id,
            success=True,
            summary=f"Python 处理 {len(raw_results)} 条, 通过 {passed} 条, 平均分 {avg_score:.1f}"
        )

        for r in raw_results:
            qr = QualityResult(
                product_id=r['product_id'],
                passed=r['passed'],
                defects=r['defects'],
                quality_score=r['quality_score'],
                inspected_at=r['inspected_at'],
                inspector=r['inspector'],
                production_line=r['production_line'],
                anomaly_index=r['anomaly_index'],
            )
            for k, v in r['measurements'].items():
                qr.measurements[k] = v
            response.results.append(qr)

            if not r['passed']:
                response.alerts.append(AlertEvent(
                    alert_id=uuid.uuid4().hex[:12],
                    product_id=r['product_id'],
                    production_line=r['production_line'],
                    severity="CRITICAL" if r['anomaly_index'] > 0.8 else "WARNING",
                    alert_type=r['defects'][0] if r['defects'] else "unknown",
                    message=f"Python 检测: {'; '.join(r['defects'])}",
                    threshold=0.8 if r['anomaly_index'] > 0.8 else 0.5,
                    actual_value=r['quality_score'],
                    created_at=int(datetime.now(timezone.utc).timestamp()),
                ))

        print(f"  → {response.summary}")
        return response


def run_worker():
    """启动 Worker 服务"""
    print("=" * 50)
    print(" Python Quality Worker Service")
    print("=" * 50)

    # 启动 gRPC 服务端 (DataProcessing service)
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    servicer = DataProcessingServicer()
    add_DataProcessingServicer_to_server(servicer, server)
    server.add_insecure_port("[::]:5002")
    server.start()
    print(f"Python Worker gRPC Server 启动: 端口 5002")
    print(f"Worker ID: {servicer.worker_id}")

    # 注册到 C# Server
    print(f"\n注册到 C# Server (localhost:5001)...")
    try:
        channel = grpc.insecure_channel("localhost:5001")
        stub = WorkerRegistryStub(channel)

        info = WorkerInfo(
            worker_id=servicer.worker_id,
            language="python",
            host="localhost",
            port=5002,
            capabilities=["quality_check", "anomaly_detection", "statistics"],
            last_heartbeat=int(datetime.now(timezone.utc).timestamp()),
        )
        resp = stub.RegisterWorker(info)
        print(f"  注册成功: server_id={resp.server_id}, 心跳={resp.heartbeat_interval_seconds}s")

        # 心跳线程
        def heartbeat():
            while True:
                try:
                    stub.Heartbeat(HeartbeatRequest(worker_id=servicer.worker_id))
                except Exception as e:
                    print(f"  心跳失败: {e}")
                import time
                time.sleep(resp.heartbeat_interval_seconds)

        threading.Thread(target=heartbeat, daemon=True).start()
        print("  心跳线程已启动")
    except Exception as e:
        print(f"  注册失败: {e}（不影响本地服务运行）")

    print(f"\n{'=' * 50}")
    print(" Worker 就绪，等待处理任务...")
    print(f"{'=' * 50}")

    try:
        server.wait_for_termination()
    except KeyboardInterrupt:
        print("\nWorker 关闭中...")


if __name__ == "__main__":
    run_worker()
