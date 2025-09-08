using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OpenTK;
using System.Windows.Forms;

namespace LoadPCDtest.Analysis
{
    /// <summary>
    /// 立面管理器 - 负责大楼四个面的识别、分组和显示控制
    /// </summary>
    public class FacadeManager
    {
        /// <summary>
    /// 立面类型枚举 - 使用相对方向而非绝对地理方向
        /// </summary>
        public enum FacadeType
        {
            MainPositive,   // 主方向正面 (建筑主轴正方向)
            MainNegative,   // 主方向负面 (建筑主轴负方向)
            PerpPositive,   // 垂直方向正面 (建筑副轴正方向)
            PerpNegative    // 垂直方向负面 (建筑副轴负方向)
        }

        /// <summary>
        /// 立面显示状态
        /// </summary>
        public class FacadeState
        {
            public bool IsVisible { get; set; } = true;
            public Vector3 Color { get; set; }
            public HashSet<int> PointIndices { get; set; } = new HashSet<int>(); // 原始点云索引（用于分析）
            public List<Vector3> GeneratedPoints { get; set; } = new List<Vector3>(); // 生成的规律立面点云
            public string Name { get; set; }
            public float TrueAzimuth { get; set; } = 0.0f; // 真实方位角（度）
            public string CompassDirection { get; set; } = ""; // 指南针方向描述
        }

        private Dictionary<FacadeType, FacadeState> facadeStates;
        private bool isInitialized = false;
        private int lastAnalyzedPointCount = 0;
        private List<Vector3> generatedFacadePoints = new List<Vector3>(); // 所有生成的立面点的集合
        
        private Vector2 buildingMainDirection = Vector2.Zero; // 建筑主方向向量（归一化）
        private Vector2 buildingPerpDirection = Vector2.Zero; // 建筑垂直方向向量（归一化）

        /// <summary>
        /// 获取是否已初始化
        /// </summary>
        public bool IsInitialized => isInitialized;

        public FacadeManager()
        {
            InitializeFacadeStates();
        }

        /// <summary>
        /// 初始化立面状态
        /// </summary>
        private void InitializeFacadeStates()
        {
            facadeStates = new Dictionary<FacadeType, FacadeState>
            {
                [FacadeType.MainPositive] = new FacadeState 
                { 
                    Color = new Vector3(1.0f, 0.0f, 0.0f), // 红色
                    Name = "主方向正面",
                    IsVisible = true
                },
                [FacadeType.MainNegative] = new FacadeState 
                { 
                    Color = new Vector3(0.0f, 0.0f, 1.0f), // 蓝色
                    Name = "主方向负面",
                    IsVisible = true
                },
                [FacadeType.PerpPositive] = new FacadeState 
                { 
                    Color = new Vector3(0.0f, 1.0f, 0.0f), // 绿色
                    Name = "垂直方向正面",
                    IsVisible = true
                },
                [FacadeType.PerpNegative] = new FacadeState 
                { 
                    Color = new Vector3(1.0f, 1.0f, 0.0f), // 黄色
                    Name = "垂直方向负面",
                    IsVisible = true
                }
            };
        }

        /// <summary>
        /// 分析点云并识别立面
        /// </summary>
        public void AnalyzeFacades(List<Vector3> points)
        {
            if (points == null || points.Count == 0) return;

            // 如果点云数量没变且已分析过，跳过重复分析
            if (isInitialized && lastAnalyzedPointCount == points.Count)
            {
                System.Diagnostics.Debug.WriteLine($"立面分析: 使用缓存结果 ({points.Count} 个点)");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"优化立面分析: 开始处理 {points.Count} 个点...");
            var startTime = DateTime.Now;

            // 清空之前的分组
            foreach (var state in facadeStates.Values)
            {
                state.PointIndices.Clear();
            }

            // 计算点云的边界框
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            
            foreach (var point in points)
            {
                min = Vector3.ComponentMin(min, point);
                max = Vector3.ComponentMax(max, point);
            }

            var center = (min + max) * 0.5f;
            var size = max - min;
            
            System.Diagnostics.Debug.WriteLine($"立面分析: 中心=({center.X:F2}, {center.Y:F2}), 尺寸=({size.X:F2}, {size.Y:F2})");
            // 步骤1: 使用鲁棒PCA计算建筑主方向（基于全部点进行剪裁后统计）
            System.Diagnostics.Debug.WriteLine("开始计算建筑主方向(鲁棒PCA)...");

            // 1.1 计算全部点的中心（仅XY）
            double meanX_all = 0, meanY_all = 0;
            for (int i_all = 0; i_all < points.Count; i_all++)
            {
                meanX_all += points[i_all].X;
                meanY_all += points[i_all].Y;
            }
            meanX_all /= points.Count;
            meanY_all /= points.Count;

            // 1.2 计算到中心的半径平方并做剪裁(去除远端离群点，默认保留95%内点)
            var radiiSq = new List<double>(points.Count);
            for (int i_r = 0; i_r < points.Count; i_r++)
            {
                double dxr = points[i_r].X - meanX_all;
                double dyr = points[i_r].Y - meanY_all;
                radiiSq.Add(dxr * dxr + dyr * dyr);
            }
            radiiSq.Sort();
            int keepIndex = (int)(radiiSq.Count * 0.95);
            if (keepIndex < 0) keepIndex = 0;
            if (keepIndex >= radiiSq.Count) keepIndex = radiiSq.Count - 1;
            double radiusSqThreshold = radiiSq[keepIndex];

            // 1.3 过滤出剪裁后的点集
            var robustPoints = new List<Vector3>(Math.Max(1000, points.Count / 2));
            for (int i_f = 0; i_f < points.Count; i_f++)
            {
                double dxr = points[i_f].X - meanX_all;
                double dyr = points[i_f].Y - meanY_all;
                if (dxr * dxr + dyr * dyr <= radiusSqThreshold)
                {
                    robustPoints.Add(points[i_f]);
                }
            }
            System.Diagnostics.Debug.WriteLine($"鲁棒PCA: 剪裁后参与统计的点数 = {robustPoints.Count}/{points.Count}");

            // 1.4 基于robustPoints计算均值
            double sumX = 0, sumY = 0;
            foreach (var p in robustPoints)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            double meanX = sumX / robustPoints.Count;
            double meanY = sumY / robustPoints.Count;
            
            // 1.5 协方差
            double cov_xx = 0, cov_xy = 0, cov_yy = 0;
            foreach (var p in robustPoints)
            {
                double dx = p.X - meanX;
                double dy = p.Y - meanY;
                cov_xx += dx * dx;
                cov_xy += dx * dy;
                cov_yy += dy * dy;
            }
            cov_xx /= robustPoints.Count;
            cov_xy /= robustPoints.Count;
            cov_yy /= robustPoints.Count;

            // 1.6 求主方向
            double trace = cov_xx + cov_yy;
            double det = cov_xx * cov_yy - cov_xy * cov_xy;
            double lambda1 = (trace + Math.Sqrt(Math.Max(0, trace * trace - 4 * det))) / 2;
            double mainDirX, mainDirY;
            if (Math.Abs(cov_xy) > 1e-10)
            {
                mainDirX = lambda1 - cov_yy;
                mainDirY = cov_xy;
            }
            else
            {
                mainDirX = 1.0;
                mainDirY = 0.0;
            }
            
            // 1.7 归一化与象限一致性
            double length = Math.Sqrt(mainDirX * mainDirX + mainDirY * mainDirY);
            mainDirX /= length;
            mainDirY /= length;
            if (mainDirX < 0)
            {
                mainDirX = -mainDirX;
                mainDirY = -mainDirY;
            }
            double perpDirX = -mainDirY;
            double perpDirY = mainDirX;
            
            // 1.8 保存方位与调试输出
            var computedMainAzimuth = CalculateTrueAzimuth((float)mainDirX, (float)mainDirY);
            buildingMainDirection = new Vector2((float)mainDirX, (float)mainDirY);
            buildingPerpDirection = new Vector2((float)perpDirX, (float)perpDirY);
            System.Diagnostics.Debug.WriteLine($"鲁棒PCA结果: main=({mainDirX:F4},{mainDirY:F4}) perp=({perpDirX:F4},{perpDirY:F4}) azimuth={computedMainAzimuth:F1}°");

            // 步骤2: 生成外轮廓挤出的立面点云
            System.Diagnostics.Debug.WriteLine("开始生成立面点云(外轮廓挤出)...");
            GenerateFacadesByExtrudedHull(points, (float)meanX, (float)meanY, min.Z, max.Z);

            // 步骤3: 计算每个立面的真实方位信息
            System.Diagnostics.Debug.WriteLine("开始计算立面真实方位信息...");
            UpdateFacadeAzimuthInfo();
            
            // 步骤4: 验证立面一致性
            System.Diagnostics.Debug.WriteLine("验证立面点云一致性...");
            VerifyFacadeConsistency();

            // 输出分析结果
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;
            System.Diagnostics.Debug.WriteLine($"优化立面分析完成，耗时: {duration:F1} ms");
            System.Diagnostics.Debug.WriteLine($"生成的规律立面点总数: {generatedFacadePoints.Count}");
            System.Diagnostics.Debug.WriteLine($"建筑方位信息:");
            foreach (var kvp in facadeStates)
            {
                System.Diagnostics.Debug.WriteLine($"  {kvp.Value.Name}: {kvp.Value.CompassDirection} (方位角: {kvp.Value.TrueAzimuth:F1}°)");
            }

            isInitialized = true;
            lastAnalyzedPointCount = points.Count;
        }

        /// <summary>
        /// 基于XY外轮廓挤出生成立面点云
        /// </summary>
        private void GenerateFacadesByExtrudedHull(List<Vector3> originalPoints, float centerX, float centerY, float zMin, float zMax)
        {
            // 1) 求XY平面的凸包（外轮廓）
            var hull2D = ComputeConvexHull2D(originalPoints);
            if (hull2D.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine("凸包点不足，无法生成挤出立面");
                return;
            }

            // 2) 生成Z层
            float pointSpacing = 8f;
            float zSpacing = 4f;
            int zLayers = Math.Max(3, (int)((zMax - zMin) / zSpacing)) + 1;

            // 3) 沿凸包边按等距采样并挤出到各Z层
            for (int e = 0; e < hull2D.Count; e++)
            {
                var a = hull2D[e];
                var b = hull2D[(e + 1) % hull2D.Count];
                var edge = b - a;
                float edgeLen = edge.Length;
                if (edgeLen < 1e-4f) continue;

                int samples = Math.Max(2, (int)(edgeLen / pointSpacing)) + 1;

                // 计算边外法线（面朝外侧）。使用顺时针凸包时，外法线可取 (dy, -dx)
                var tangent = edge / edgeLen;
                var normal = new Vector2(tangent.Y, -tangent.X);

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / (samples - 1);
                    var p2 = a + t * edge; // XY
                    for (int j = 0; j < zLayers; j++)
                    {
                        float z = zMin + (zMax - zMin) * j / (zLayers - 1);
                        var p3 = new Vector3(p2.X, p2.Y, z);
                        generatedFacadePoints.Add(p3);
                    }
                }

                // 将该边的点分配到对应立面：根据外法线与主/副方向夹角
                AssignEdgeSamplesToFacade(a, b, normal, pointSpacing, zMin, zMax, zSpacing);
            }

            System.Diagnostics.Debug.WriteLine($"凸包挤出生成完成: {generatedFacadePoints.Count} 个点");
        }

        /// <summary>
        /// 将一条凸包边的样本点分配给四个立面之一
        /// </summary>
        private void AssignEdgeSamplesToFacade(Vector2 a, Vector2 b, Vector2 outwardNormal, float pointSpacing, float zMin, float zMax, float zSpacing)
        {
            var edge = b - a;
            float edgeLen = edge.Length;
            int samples = Math.Max(2, (int)(edgeLen / pointSpacing)) + 1;
            int zLayers = Math.Max(3, (int)((zMax - zMin) / zSpacing)) + 1; // 使用传入的 zSpacing

            // 选择立面：取与 outwardNormal 夹角最小的方向（Main± 或 Perp±）
            FacadeType target;
            var dirs = new List<(FacadeType type, Vector2 dir)>
            {
                (FacadeType.MainPositive, new Vector2(buildingMainDirection.X, buildingMainDirection.Y)),
                (FacadeType.MainNegative, -new Vector2(buildingMainDirection.X, buildingMainDirection.Y)),
                (FacadeType.PerpPositive, new Vector2(buildingPerpDirection.X, buildingPerpDirection.Y)),
                (FacadeType.PerpNegative, -new Vector2(buildingPerpDirection.X, buildingPerpDirection.Y))
            };
            float best = float.MaxValue; target = FacadeType.MainPositive;
            var n = outwardNormal; n.Normalize();
            foreach (var d in dirs)
            {
                var v = d.dir; v.Normalize();
                float dot = Vector2.Dot(n, v);
                if (dot < -1f) dot = -1f; else if (dot > 1f) dot = 1f; // 兼容旧版 .NET 无 Math.Clamp
                // 兼容旧版 .NET：使用 System.Math.Acos 返回 double，再转 float
                float angle = (float)System.Math.Acos(dot);
                if (angle < best) { best = angle; target = d.type; }
            }

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                var p2 = a + t * edge; // XY
                for (int j = 0; j < zLayers; j++)
                {
                    float z = zMin + (zMax - zMin) * j / (zLayers - 1);
                    var p3 = new Vector3(p2.X, p2.Y, z);
                    facadeStates[target].GeneratedPoints.Add(p3);
                }
            }
        }

        /// <summary>
        /// 计算XY平面的凸包（单调链算法），按顺时针返回
        /// </summary>
        private List<Vector2> ComputeConvexHull2D(List<Vector3> points)
        {
            var pts = new List<Vector2>(points.Count);
            foreach (var p in points) pts.Add(new Vector2(p.X, p.Y));
            pts = pts.Distinct().ToList();
            if (pts.Count <= 1) return pts;

            pts.Sort((p1, p2) => p1.X == p2.X ? p1.Y.CompareTo(p2.Y) : p1.X.CompareTo(p2.X));

            var lower = new List<Vector2>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0) lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            var upper = new List<Vector2>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0) upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            var hull = new List<Vector2>(lower.Count + upper.Count);
            hull.AddRange(lower);
            hull.AddRange(upper);
            // hull 逆时针；我们按需要可反转为顺时针以匹配外法线方向
            hull.Reverse(); // 变为顺时针
            return hull;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
        /// <summary>
        /// 切换立面显示状态
        /// </summary>
        public bool ToggleFacadeVisibility(FacadeType facade)
        {
            if (facadeStates.ContainsKey(facade))
            {
                facadeStates[facade].IsVisible = !facadeStates[facade].IsVisible;
                System.Diagnostics.Debug.WriteLine($"{facadeStates[facade].Name}: {(facadeStates[facade].IsVisible ? "显示" : "隐藏")}");
                return facadeStates[facade].IsVisible;
            }
            return false;
        }

        /// <summary>
        /// 获取立面显示状态
        /// </summary>
        public bool IsFacadeVisible(FacadeType facade)
        {
            return facadeStates.ContainsKey(facade) && facadeStates[facade].IsVisible;
        }

        /// <summary>
        /// 获取立面颜色
        /// </summary>
        public Vector3 GetFacadeColor(FacadeType facade)
        {
            return facadeStates.ContainsKey(facade) ? facadeStates[facade].Color : Vector3.One;
        }

        /// <summary>
        /// 获取立面名称
        /// </summary>
        public string GetFacadeName(FacadeType facade)
        {
            return facadeStates.ContainsKey(facade) ? facadeStates[facade].Name : "未知";
        }

        /// <summary>
        /// 检查点是否应该被渲染（原始点云模式 - 已弃用）
        /// </summary>
        public bool ShouldRenderPoint(int pointIndex, out Vector3 color)
        {
            color = Vector3.One; // 默认白色

            if (!isInitialized) return true;

            // 检查该点属于哪个立面，并返回对应的显示状态和颜色
            foreach (var kvp in facadeStates)
            {
                if (kvp.Value.PointIndices.Contains(pointIndex))
                {
                    color = kvp.Value.Color;
                    return kvp.Value.IsVisible;
                }
            }

            return true; // 不属于任何立面的点默认显示
        }

        

        /// <summary>
        /// 获取特定立面的生成点云
        /// </summary>
        public List<Vector3> GetFacadePoints(FacadeType facade)
        {
            if (facadeStates.ContainsKey(facade))
            {
                return facadeStates[facade].GeneratedPoints;
            }
            return new List<Vector3>();
        }

        

        /// <summary>
        /// 获取当前显示状态摘要
        /// </summary>
        public string GetDisplaySummary()
        {
            if (!isInitialized) return "立面未分析";

            var visibleFacades = facadeStates.Where(kvp => kvp.Value.IsVisible)
                                            .Select(kvp => kvp.Value.Name);
            
            return $"显示: {string.Join(", ", visibleFacades)}";
        }

        /// <summary>
        /// 重置所有立面为可见状态
        /// </summary>
        public void ShowAllFacades()
        {
            foreach (var state in facadeStates.Values)
            {
                state.IsVisible = true;
            }
            System.Diagnostics.Debug.WriteLine("显示所有立面");
        }

        /// <summary>
        /// 计算真实方位角（相对于北方，顺时针为正）
        /// </summary>
        /// <param name="dirX">方向向量X分量</param>
        /// <param name="dirY">方向向量Y分量</param>
        /// <returns>方位角（度，0-360）</returns>
        private float CalculateTrueAzimuth(float dirX, float dirY)
        {
            // 计算相对于北方（Y轴正方向）的角度
            // 注意：在标准地理坐标系中，北方是Y轴正方向，东方是X轴正方向
            // 方位角从北方开始，顺时针为正
            
            double angleRad = Math.Atan2(dirX, dirY); // 注意参数顺序：X是sin，Y是cos
            double angleDeg = angleRad * 180.0 / Math.PI;
            
            // 将角度转换为0-360度范围
            if (angleDeg < 0)
                angleDeg += 360.0;
                
            return (float)angleDeg;
        }

        /// <summary>
        /// 根据方位角获取指南针方向描述
        /// </summary>
        /// <param name="azimuth">方位角（度）</param>
        /// <returns>指南针方向字符串</returns>
        private string GetCompassDirection(float azimuth)
        {
            // 标准化角度到0-360范围
            while (azimuth < 0) azimuth += 360;
            while (azimuth >= 360) azimuth -= 360;
            
            // 16方位划分，每个方位22.5度
            string[] directions = {
                "北", "北东北", "东北", "东东北",
                "东", "东东南", "东南", "南东南",
                "南", "南西南", "西南", "西西南",
                "西", "西西北", "西北", "北西北"
            };
            
            int index = (int)Math.Round(azimuth / 22.5) % 16;
            return directions[index];
        }

        /// <summary>
        /// 更新所有立面的真实方位信息
        /// </summary>
        private void UpdateFacadeAzimuthInfo()
        {            
            // 计算主方向和垂直方向的方位角
            float mainAzimuth = CalculateTrueAzimuth(buildingMainDirection.X, buildingMainDirection.Y);
            float perpAzimuth = CalculateTrueAzimuth(buildingPerpDirection.X, buildingPerpDirection.Y);
            
            // 更新每个立面的方位信息
            facadeStates[FacadeType.MainPositive].TrueAzimuth = mainAzimuth;
            facadeStates[FacadeType.MainPositive].CompassDirection = GetCompassDirection(mainAzimuth);
            facadeStates[FacadeType.MainPositive].Name = $"主方向正面 (朝向{GetCompassDirection(mainAzimuth)})";
            
            float mainNegativeAzimuth = (mainAzimuth + 180) % 360;
            facadeStates[FacadeType.MainNegative].TrueAzimuth = mainNegativeAzimuth;
            facadeStates[FacadeType.MainNegative].CompassDirection = GetCompassDirection(mainNegativeAzimuth);
            facadeStates[FacadeType.MainNegative].Name = $"主方向负面 (朝向{GetCompassDirection(mainNegativeAzimuth)})";
            
            facadeStates[FacadeType.PerpPositive].TrueAzimuth = perpAzimuth;
            facadeStates[FacadeType.PerpPositive].CompassDirection = GetCompassDirection(perpAzimuth);
            facadeStates[FacadeType.PerpPositive].Name = $"垂直方向正面 (朝向{GetCompassDirection(perpAzimuth)})";
            
            float perpNegativeAzimuth = (perpAzimuth + 180) % 360;
            facadeStates[FacadeType.PerpNegative].TrueAzimuth = perpNegativeAzimuth;
            facadeStates[FacadeType.PerpNegative].CompassDirection = GetCompassDirection(perpNegativeAzimuth);
            facadeStates[FacadeType.PerpNegative].Name = $"垂直方向负面 (朝向{GetCompassDirection(perpNegativeAzimuth)})";
            
            System.Diagnostics.Debug.WriteLine($"方位角计算完成:");
            System.Diagnostics.Debug.WriteLine($"  建筑主方向: {mainAzimuth:F1}° ({GetCompassDirection(mainAzimuth)})");
            System.Diagnostics.Debug.WriteLine($"  建筑垂直方向: {perpAzimuth:F1}° ({GetCompassDirection(perpAzimuth)})");
        }

        

        /// <summary>
        /// 将指定立面的点按“从上到下的蛇形”排序
        /// </summary>
        private List<Vector3> GetSnakeOrderedPoints(FacadeType facade, float layerMergeEpsilon)
        {
            var pts = GetFacadePoints(facade);
            if (pts == null || pts.Count == 0) return new List<Vector3>();

            // 1) 以 Z 从高到低分层（允许 layerMergeEpsilon 合并近似层）
            var layers = new List<List<Vector3>>();
            var sorted = pts.OrderByDescending(p => p.Z).ToList();
            foreach (var p in sorted)
            {
                if (layers.Count == 0)
                {
                    layers.Add(new List<Vector3> { p });
                    continue;
                }
                var currentLayer = layers[layers.Count - 1];
                if (System.Math.Abs(currentLayer[0].Z - p.Z) <= layerMergeEpsilon)
                {
                    currentLayer.Add(p);
                }
                else
                {
                    layers.Add(new List<Vector3> { p });
                }
            }

            // 2) 每层按世界坐标 X 从左到右排序，并奇偶层交替反转形成蛇形
            var result = new List<Vector3>(pts.Count);
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var ordered = layer.OrderBy(p => p.X).ToList();
                if (i % 2 == 1) ordered.Reverse();
                result.AddRange(ordered);
            }
            return result;
        }

        /// <summary>
        /// 导出所有立面为 QGC WPL 110 航点文件（每个立面一个文件）
        /// 注意：此处使用 X->lat, Y->lon, Z->alt 的坐标映射。
        /// </summary>
        public void ExportFacadesToQgc(string baseFilePathWithoutExt, float layerMergeEpsilon = 0.6f)
        {
            if (!isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("立面未分析，无法导出航点");
                return;
            }

            foreach (var kv in facadeStates)
            {
                var facade = kv.Key;
                var name = kv.Value.Name + DateTime.Now.ToString("hhmmss");
                var ordered = GetSnakeOrderedPoints(facade, layerMergeEpsilon);
                if (ordered.Count == 0) continue;

                var path = baseFilePathWithoutExt + "_" + name + ".waypoints";
                using (var sw = new StreamWriter(path, false))
                {
                    sw.WriteLine("QGC WPL 110");
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var p = ordered[i];
                        int seq = i;
                        int current = (i == 0) ? 1 : 0;
                        int frame = 0;     // MAV_FRAME_GLOBAL（按需调整）
                        int command = 16;  // NAV_WAYPOINT
                        double p1 = 0, p2 = 0, p3 = 0, p4 = 0; // 预留参数
                        double lat = p.X;
                        double lon = p.Z;
                        double alt = p.Z;
                        int autocontinue = 1;
                        sw.WriteLine($"{seq}\t{current}\t{frame}\t{command}\t{p1:F8}\t{p2:F8}\t{p3:F8}\t{p4:F8}\t{lat:F8}\t{lon:F8}\t{alt:F6}\t{autocontinue}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"已导出航点: {path} ({ordered.Count} 个)");
            }
        }

        /// <summary>
        /// 验证立面点云一致性
        /// </summary>
        private void VerifyFacadeConsistency()
        {
            System.Diagnostics.Debug.WriteLine($"=== 立面一致性验证 ===");
            
            foreach (var kvp in facadeStates)
            {
                var facadeType = kvp.Key;
                var state = kvp.Value;
                
                System.Diagnostics.Debug.WriteLine($"{state.Name}:");
                System.Diagnostics.Debug.WriteLine($"  原始点数: {state.PointIndices.Count}");
                System.Diagnostics.Debug.WriteLine($"  生成点数: {state.GeneratedPoints.Count}");
                System.Diagnostics.Debug.WriteLine($"  方位角: {state.TrueAzimuth:F1}°");
                System.Diagnostics.Debug.WriteLine($"  指南针方向: {state.CompassDirection}");
                System.Diagnostics.Debug.WriteLine($"  可见性: {state.IsVisible}");
                
                if (state.GeneratedPoints.Count > 0)
                {
                    var firstPoint = state.GeneratedPoints[0];
                    var lastPoint = state.GeneratedPoints[state.GeneratedPoints.Count - 1];
                    System.Diagnostics.Debug.WriteLine($"  生成点范围: ({firstPoint.X:F2}, {firstPoint.Y:F2}) 到 ({lastPoint.X:F2}, {lastPoint.Y:F2})");
                }
                System.Diagnostics.Debug.WriteLine("");
            }
            
            int totalGenerated = generatedFacadePoints.Count;
            int expectedTotal = facadeStates.Values.Sum(s => s.GeneratedPoints.Count);
            System.Diagnostics.Debug.WriteLine($"总生成点数验证: 实际={totalGenerated}, 预期={expectedTotal}, 一致性={(totalGenerated == expectedTotal ? "正确" : "错误")}");
            System.Diagnostics.Debug.WriteLine($"========================");
        }
    }
}