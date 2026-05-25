"""
质量检测引擎 - 使用 numpy 进行数据分析
提供: quality_check / anomaly_detection / statistics 三种分析能力
"""

import numpy as np
from datetime import datetime, timezone


class QualityEngine:
    """基于 numpy 的质量检测引擎"""

    @staticmethod
    def quality_check(products: list) -> list:
        """全面质检"""
        results = []
        for p in products:
            defects = []
            anomaly = 0.0

            # 温度检测 (规格: 15-35°C)
            if p['temperature'] < 15:
                defects.append("低温异常")
                anomaly += 0.3
            elif p['temperature'] > 35:
                defects.append("高温异常")
                anomaly += 0.3

            # 压力检测 (规格: ≤10MPa)
            if p['pressure'] > 10:
                defects.append("压力超限")
                anomaly += 0.3

            # 重量检测 (规格: 50-500g)
            if p['weight'] < 50:
                defects.append("重量偏轻")
                anomaly += 0.25
            elif p['weight'] > 500:
                defects.append("重量超重")
                anomaly += 0.25

            # 尺寸偏差检测
            dims = {'dimension_x': p['dimension_x'], 'dimension_y': p['dimension_y'],
                    'dimension_z': p['dimension_z']}
            dim_specs = {'dimension_x': (50, 150), 'dimension_y': (30, 100),
                         'dimension_z': (10, 50)}

            for dim_name, (lo, hi) in dim_specs.items():
                val = dims[dim_name]
                if val < lo or val > hi:
                    defects.append(f"{dim_name}偏差")
                    anomaly += 0.2

            anomaly = min(anomaly, 1.0)
            passed = len(defects) == 0
            score = 100.0 if passed else max(0, 100 - anomaly * 100)

            results.append({
                'product_id': p['product_id'],
                'passed': passed,
                'defects': defects,
                'quality_score': round(score, 1),
                'inspected_at': int(datetime.now(timezone.utc).timestamp()),
                'inspector': 'python-quality-engine',
                'production_line': p.get('production_line', ''),
                'anomaly_index': round(anomaly, 3),
                'measurements': {
                    'temperature_deviation': round(abs(p['temperature'] - 25), 2),
                    'pressure_deviation': round(abs(p['pressure'] - 5), 2),
                    'weight_deviation': round(abs(p['weight'] - 250) / 250, 3),
                }
            })
        return results

    @staticmethod
    def anomaly_detection(products: list) -> list:
        """基于统计的异常检测 - 使用 IQR (四分位距法)"""
        if not products:
            return []

        # 提取数值数组
        temps = np.array([p['temperature'] for p in products])
        pressures = np.array([p['pressure'] for p in products])
        weights = np.array([p['weight'] for p in products])

        def detect_outliers(values, threshold=1.5):
            """IQR 方法检测异常值"""
            q1, q3 = np.percentile(values, [25, 75])
            iqr = q3 - q1
            lower = q1 - threshold * iqr
            upper = q3 + threshold * iqr
            return (values < lower) | (values > upper)

        temp_outliers = detect_outliers(temps)
        press_outliers = detect_outliers(pressures)
        weight_outliers = detect_outliers(weights)

        results = []
        for i, p in enumerate(products):
            defects = []
            anomaly = 0.0

            if temp_outliers[i]:
                z_temp = abs(p['temperature'] - np.mean(temps)) / (np.std(temps) + 1e-8)
                defects.append(f"温度异常(z={z_temp:.1f})")
                anomaly += 0.35

            if press_outliers[i]:
                z_press = abs(p['pressure'] - np.mean(pressures)) / (np.std(pressures) + 1e-8)
                defects.append(f"压力异常(z={z_press:.1f})")
                anomaly += 0.35

            if weight_outliers[i]:
                defects.append("重量异常(IQR)")
                anomaly += 0.3

            anomaly = min(anomaly, 1.0)
            passed = len(defects) == 0
            score = 100.0 if passed else max(0, 100 - anomaly * 100)

            results.append({
                'product_id': p['product_id'],
                'passed': passed,
                'defects': defects,
                'quality_score': round(score, 1),
                'inspected_at': int(datetime.now(timezone.utc).timestamp()),
                'inspector': 'python-iqr-detector',
                'production_line': p.get('production_line', ''),
                'anomaly_index': round(anomaly, 3),
                'measurements': {}
            })
        return results

    @staticmethod
    def statistics(products: list) -> list:
        """统计分析 - 计算 Z-Score 偏离度"""
        if not products:
            return []

        temps = np.array([p['temperature'] for p in products])
        pressures = np.array([p['pressure'] for p in products])
        weights = np.array([p['weight'] for p in products])

        results = []
        for i, p in enumerate(products):
            defects = []
            anomaly = 0.0

            # Z-Score 计算
            for name, values, spec_mean, spec_std in [
                ('temperature', temps, 25, 5),
                ('pressure', pressures, 5, 3),
                ('weight', weights, 250, 80)
            ]:
                val = p[name]
                # 基于规格的 Z-Score (不是基于样本)
                z_score = abs(val - spec_mean) / (spec_std + 1e-8)
                if z_score > 3:
                    defects.append(f"{name}严重异常(>{'3σ'})")
                    anomaly += 0.4
                elif z_score > 2:
                    defects.append(f"{name}偏离警戒(>{'2σ'})")
                    anomaly += 0.2

            anomaly = min(anomaly, 1.0)
            passed = len(defects) == 0
            score = 100.0 if passed else max(0, 100 - anomaly * 100)

            results.append({
                'product_id': p['product_id'],
                'passed': passed,
                'defects': defects,
                'quality_score': round(score, 1),
                'inspected_at': int(datetime.now(timezone.utc).timestamp()),
                'inspector': 'python-statistics-engine',
                'production_line': p.get('production_line', ''),
                'anomaly_index': round(anomaly, 3),
                'measurements': {}
            })
        return results
