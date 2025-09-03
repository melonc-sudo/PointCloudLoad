using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;

namespace LoadPCDtest.Analysis
{
    /// <summary>
    /// 立面管理器 - 负责大楼四个面的识别、分组和显示控制
    /// </summary>
    public class FacadeManager
    {
        /// <summary>
        /// 立面生成策略
        /// </summary>
        public enum FacadeGenerationMode
        {
            RotatedBox,     // 旧：基于旋转坐标系的矩形四面
            ExtrudedHull    // 新：基于XY外轮廓挤出的立面
        }
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
        private float buildingAzimuth = 0.0f; // 建筑主方向的真实方位角（度，北方为0度，顺时针为正）
        private Vector2 buildingMainDirection = Vector2.Zero; // 建筑主方向向量（归一化）
        private Vector2 buildingPerpDirection = Vector2.Zero; // 建筑垂直方向向量（归一化）
        private FacadeGenerationMode generationMode = FacadeGenerationMode.ExtrudedHull; // 默认采用外轮廓挤出

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
            System.Diagnostics.Debug.WriteLine($"分割策略: 外围立面提取法");
            
            // 步骤1(弃用作方向估计): 提取建筑外围点（仅作为参考，不再用于PCA方向估计）
            System.Diagnostics.Debug.WriteLine("开始提取建筑外围(仅供参考)...");
            var outerPoints = ExtractOuterPerimeter(points);
            System.Diagnostics.Debug.WriteLine($"外围点提取: {points.Count} → {outerPoints.Count} 个点");

            // 步骤2: 使用鲁棒PCA计算建筑主方向（基于全部点进行剪裁后统计）
            System.Diagnostics.Debug.WriteLine("开始计算建筑主方向(鲁棒PCA)...");

            // 2.1 计算全部点的中心（仅XY）
            double meanX_all = 0, meanY_all = 0;
            for (int i_all = 0; i_all < points.Count; i_all++)
            {
                meanX_all += points[i_all].X;
                meanY_all += points[i_all].Y;
            }
            meanX_all /= points.Count;
            meanY_all /= points.Count;

            // 2.2 计算到中心的半径平方并做剪裁(去除远端离群点，默认保留95%内点)
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

            // 2.3 过滤出剪裁后的点集
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

            // 2.4 基于robustPoints计算均值
            double sumX = 0, sumY = 0;
            foreach (var p in robustPoints)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            double meanX = sumX / robustPoints.Count;
            double meanY = sumY / robustPoints.Count;
            
            // 2.5 协方差
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

            // 2.6 求主方向
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

            // 2.7 归一化与象限一致性
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

            // 2.8 保存方位与调试输出
            buildingAzimuth = CalculateTrueAzimuth((float)mainDirX, (float)mainDirY);
            buildingMainDirection = new Vector2((float)mainDirX, (float)mainDirY);
            buildingPerpDirection = new Vector2((float)perpDirX, (float)perpDirY);
            System.Diagnostics.Debug.WriteLine($"鲁棒PCA结果: main=({mainDirX:F4},{mainDirY:F4}) perp=({perpDirX:F4},{perpDirY:F4}) azimuth={buildingAzimuth:F1}°");

            // 步骤3: 将所有点投影到主方向坐标系（使用鲁棒PCA的均值meanX/meanY）
            double[] projMain = new double[points.Count];  // 主方向投影
            double[] projPerp = new double[points.Count];  // 垂直方向投影
            for (int i = 0; i < points.Count; i++)
            {
                double localX = points[i].X - meanX;
                double localY = points[i].Y - meanY;
                projMain[i] = localX * mainDirX + localY * mainDirY;
                projPerp[i] = localX * perpDirX + localY * perpDirY;
            }

            // 步骤4: 在旋转坐标系中计算边界
            double minMain = projMain.Min();
            double maxMain = projMain.Max();
            double minPerp = projPerp.Min();
            double maxPerp = projPerp.Max();
            
            double mainSize = maxMain - minMain;
            double perpSize = maxPerp - minPerp;
            
            // 优化厚度计算：为每个方向使用固定的统一厚度
            double uniformThickness = Math.Min(mainSize, perpSize) * 0.15; // 统一厚度为较短边的15%
            
            // 计算四个立面的精确边界（确保厚度一致）
            double mainThickness = uniformThickness;  // 主方向（东西面）厚度
            double perpThickness = uniformThickness;  // 垂直方向（南北面）厚度
            
            System.Diagnostics.Debug.WriteLine($"旋转坐标系边界: 主方向[{minMain:F2}, {maxMain:F2}], 垂直方向[{minPerp:F2}, {maxPerp:F2}]");
            System.Diagnostics.Debug.WriteLine($"主方向尺寸: {mainSize:F2}, 垂直方向尺寸: {perpSize:F2}");
            System.Diagnostics.Debug.WriteLine($"统一厚度: {uniformThickness:F2}");
            System.Diagnostics.Debug.WriteLine($"分割策略: 基于到边界距离的内向外识别法");
            
            // 步骤5: 在旋转坐标系中进行立面分割
            int mainPositiveCount = 0, mainNegativeCount = 0, perpPositiveCount = 0, perpNegativeCount = 0;
            
            for (int i = 0; i < points.Count; i++)
            {
                // 使用预计算的投影坐标
                double mainProj = projMain[i];
                double perpProj = projPerp[i];
                
                // 在旋转坐标系中进行分割
                bool assigned = false;
                
                // 改进的立面识别算法 - 基于到边界距离的内向外识别
                // 判断点更靠近哪个方向的边界
                double mainBoundaryDistance = Math.Min(Math.Abs(mainProj - maxMain), Math.Abs(mainProj - minMain));
                double perpBoundaryDistance = Math.Min(Math.Abs(perpProj - maxPerp), Math.Abs(perpProj - minPerp));
                
                // 基于到边界的距离进行分组（而不是从边界向内）
                if (mainBoundaryDistance <= mainThickness && mainBoundaryDistance <= perpBoundaryDistance)
                {
                    // 更靠近主方向边界 - 分配给主方向正面或负面
                    if (Math.Abs(mainProj - maxMain) < Math.Abs(mainProj - minMain))
                    {
                        // 更靠近主方向最大值 - 主方向正面
                        facadeStates[FacadeType.MainPositive].PointIndices.Add(i);
                        mainPositiveCount++;
                        assigned = true;
                    }
                    else
                    {
                        // 更靠近主方向最小值 - 主方向负面
                        facadeStates[FacadeType.MainNegative].PointIndices.Add(i);
                        mainNegativeCount++;
                        assigned = true;
                    }
                }
                else if (perpBoundaryDistance <= perpThickness)
                {
                    // 更靠近垂直方向边界 - 分配给垂直方向正面或负面
                    if (Math.Abs(perpProj - maxPerp) < Math.Abs(perpProj - minPerp))
                    {
                        // 更靠近垂直方向最大值 - 垂直方向正面
                        facadeStates[FacadeType.PerpPositive].PointIndices.Add(i);
                        perpPositiveCount++;
                        assigned = true;
                    }
                    else
                    {
                        // 更靠近垂直方向最小值 - 垂直方向负面
                        facadeStates[FacadeType.PerpNegative].PointIndices.Add(i);
                        perpNegativeCount++;
                        assigned = true;
                    }
                }
                
                // 每处理1万个点输出一次进度
                if ((i + 1) % 10000 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"立面分析进度: {i + 1}/{points.Count} ({(float)(i + 1) / points.Count * 100:F1}%)");
                }
            }
            
            int totalAssigned = mainPositiveCount + mainNegativeCount + perpPositiveCount + perpNegativeCount;
            int unassigned = points.Count - totalAssigned;
            
            System.Diagnostics.Debug.WriteLine($"外围立面分割完成:");
            System.Diagnostics.Debug.WriteLine($"  主方向正面: {mainPositiveCount} 个点 (厚度: {mainThickness:F2})");
            System.Diagnostics.Debug.WriteLine($"  主方向负面: {mainNegativeCount} 个点 (厚度: {mainThickness:F2})"); 
            System.Diagnostics.Debug.WriteLine($"  垂直方向正面: {perpPositiveCount} 个点 (厚度: {perpThickness:F2})");
            System.Diagnostics.Debug.WriteLine($"  垂直方向负面: {perpNegativeCount} 个点 (厚度: {perpThickness:F2})");
            System.Diagnostics.Debug.WriteLine($"  内部区域: {unassigned} 个点 (未分配)");
            System.Diagnostics.Debug.WriteLine($"  立面覆盖率: {(float)totalAssigned / points.Count * 100:F1}%");
            
            // 厚度均匀性检查
            int mainDirectionTotal = mainPositiveCount + mainNegativeCount;
            int perpDirectionTotal = perpPositiveCount + perpNegativeCount;
            System.Diagnostics.Debug.WriteLine($"厚度均匀性: 主方向总计={mainDirectionTotal}, 垂直方向总计={perpDirectionTotal}");

            // 步骤6: 生成规律的立面点云
            System.Diagnostics.Debug.WriteLine("开始生成规律立面点云...");
            GenerateRegularFacades(points, 
                minX: (float)minMain, maxX: (float)maxMain, 
                minY: (float)minPerp, maxY: (float)maxPerp,
                mainDirX: (float)mainDirX, mainDirY: (float)mainDirY,
                perpDirX: (float)perpDirX, perpDirY: (float)perpDirY,
                centerX: (float)meanX, centerY: (float)meanY,
                zMin: min.Z, zMax: max.Z);

            // 步骤7: 计算每个立面的真实方位信息
            System.Diagnostics.Debug.WriteLine("开始计算立面真实方位信息...");
            UpdateFacadeAzimuthInfo();
            
            // 步骤8: 验证立面一致性
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
        /// 提取建筑外围点（排除内部结构）- 优化版
        /// </summary>
        private List<Vector3> ExtractOuterPerimeter(List<Vector3> points)
        {
            // 计算边界
            float minX = points.Min(p => p.X);
            float maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxY = points.Max(p => p.Y);
            
            float width = maxX - minX;
            float height = maxY - minY;
            
            // 使用更小的网格以提高性能
            int gridSize = 20; // 减少到20x20网格
            float cellWidth = width / gridSize;
            float cellHeight = height / gridSize;
            
            System.Diagnostics.Debug.WriteLine($"网格分析: {gridSize}x{gridSize}, 单元格大小: ({cellWidth:F2}, {cellHeight:F2})");
            
            // 创建网格并分配点到网格单元
            var grid = new Dictionary<(int, int), List<Vector3>>();
            
            foreach (var point in points)
            {
                int gridX = Math.Min(gridSize - 1, (int)((point.X - minX) / cellWidth));
                int gridY = Math.Min(gridSize - 1, (int)((point.Y - minY) / cellHeight));
                
                if (!grid.ContainsKey((gridX, gridY)))
                    grid[(gridX, gridY)] = new List<Vector3>();
                
                grid[(gridX, gridY)].Add(point);
            }
            
            System.Diagnostics.Debug.WriteLine($"网格填充完成，有效单元: {grid.Count}/{gridSize * gridSize}");
            
            // 识别边界网格
            var boundaryGrids = new HashSet<(int, int)>();
            
            foreach (var cell in grid.Keys)
            {
                if (IsBoundaryGrid(cell.Item1, cell.Item2, gridSize, grid))
                {
                    boundaryGrids.Add(cell);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"边界网格识别完成: {boundaryGrids.Count} 个边界单元");
            
            // 收集边界网格中的所有点
            var outerPoints = new List<Vector3>();
            foreach (var boundaryGrid in boundaryGrids)
            {
                if (grid.ContainsKey(boundaryGrid))
                {
                    outerPoints.AddRange(grid[boundaryGrid]);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"外围点提取完成: {outerPoints.Count} 个外围点");
            
            return outerPoints;
        }

        /// <summary>
        /// 生成规律的立面点云
        /// </summary>
        private void GenerateRegularFacades(List<Vector3> originalPoints,
            float minX, float maxX, float minY, float maxY,
            float mainDirX, float mainDirY, float perpDirX, float perpDirY,
            float centerX, float centerY, float zMin, float zMax)
        {
            // 清空之前生成的点
            generatedFacadePoints.Clear();
            foreach (var state in facadeStates.Values)
            {
                state.GeneratedPoints.Clear();
            }

            if (generationMode == FacadeGenerationMode.ExtrudedHull)
            {
                GenerateFacadesByExtrudedHull(originalPoints, centerX, centerY, zMin, zMax);
                return;
            }

            // 旧策略：旋转矩形四面
            float pointSpacing = 0.8f; // 点间距
            float zSpacing = 1.3f;     // Z间距

            float mainSize = maxX - minX;
            float perpSize = maxY - minY;
            float height = zMax - zMin;

            System.Diagnostics.Debug.WriteLine($"立面生成参数: 点间距={pointSpacing:F2}, Z间距={zSpacing:F2}");
            System.Diagnostics.Debug.WriteLine($"建筑尺寸: 主方向={mainSize:F2}, 垂直方向={perpSize:F2}, 高度={height:F2}");

            var boundaryPoints = CalculateUnifiedBoundaryPoints(
                centerX, centerY, minX, maxX, minY, maxY,
                mainDirX, mainDirY, perpDirX, perpDirY);

            System.Diagnostics.Debug.WriteLine($"立面生成参数验证:");
            System.Diagnostics.Debug.WriteLine($"  主方向向量: ({mainDirX:F3}, {mainDirY:F3})");
            System.Diagnostics.Debug.WriteLine($"  垂直方向向量: ({perpDirX:F3}, {perpDirY:F3})");
            System.Diagnostics.Debug.WriteLine($"  主方向尺寸: {mainSize:F2}, 垂直方向尺寸: {perpSize:F2}");
            
            GenerateFacadePlane(FacadeType.MainPositive, boundaryPoints[FacadeType.MainPositive], 
                perpDirX, perpDirY, perpSize, pointSpacing, zSpacing, zMin, zMax);
            GenerateFacadePlane(FacadeType.MainNegative, boundaryPoints[FacadeType.MainNegative], 
                perpDirX, perpDirY, perpSize, pointSpacing, zSpacing, zMin, zMax);
            GenerateFacadePlane(FacadeType.PerpPositive, boundaryPoints[FacadeType.PerpPositive], 
                mainDirX, mainDirY, mainSize, pointSpacing, zSpacing, zMin, zMax);
            GenerateFacadePlane(FacadeType.PerpNegative, boundaryPoints[FacadeType.PerpNegative], 
                mainDirX, mainDirY, mainSize, pointSpacing, zSpacing, zMin, zMax);

            System.Diagnostics.Debug.WriteLine($"规律立面生成完成:");
            foreach (var kvp in facadeStates)
            {
                System.Diagnostics.Debug.WriteLine($"  {kvp.Value.Name}: {kvp.Value.GeneratedPoints.Count} 个规律点");
            }
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
                System.Diagnostics.Debug.WriteLine("凸包点不足，回退到旧策略");
                generationMode = FacadeGenerationMode.RotatedBox;
                return;
            }

            // 2) 生成Z层
            float pointSpacing = 0.8f;
            float zSpacing = 1.3f;
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
                AssignEdgeSamplesToFacade(a, b, normal, pointSpacing, zMin, zMax);
            }

            System.Diagnostics.Debug.WriteLine($"凸包挤出生成完成: {generatedFacadePoints.Count} 个点");
        }

        /// <summary>
        /// 将一条凸包边的样本点分配给四个立面之一
        /// </summary>
        private void AssignEdgeSamplesToFacade(Vector2 a, Vector2 b, Vector2 outwardNormal, float pointSpacing, float zMin, float zMax)
        {
            var edge = b - a;
            float edgeLen = edge.Length;
            int samples = Math.Max(2, (int)(edgeLen / pointSpacing)) + 1;
            int zLayers = Math.Max(3, (int)((zMax - zMin) / 1.3f)) + 1; // 与上方一致

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
        /// 计算统一的边界点，确保四个立面形成完整的矩形
        /// </summary>
        private Dictionary<FacadeType, Vector3> CalculateUnifiedBoundaryPoints(
            float centerX, float centerY, float minX, float maxX, float minY, float maxY,
            float mainDirX, float mainDirY, float perpDirX, float perpDirY)
        {
            var boundaryPoints = new Dictionary<FacadeType, Vector3>();

            // 重新设计：计算矩形的四个角点，然后确定每个立面的起点
            // 矩形四个角点（按逆时针顺序）
            Vector3 corner1 = new Vector3(  // minMain, minPerp
                centerX + mainDirX * minX + perpDirX * minY,
                centerY + mainDirY * minX + perpDirY * minY, 0);
            Vector3 corner2 = new Vector3(  // maxMain, minPerp  
                centerX + mainDirX * maxX + perpDirX * minY,
                centerY + mainDirY * maxX + perpDirY * minY, 0);
            Vector3 corner3 = new Vector3(  // maxMain, maxPerp
                centerX + mainDirX * maxX + perpDirX * maxY,
                centerY + mainDirY * maxX + perpDirY * maxY, 0);
            Vector3 corner4 = new Vector3(  // minMain, maxPerp
                centerX + mainDirX * minX + perpDirX * maxY,
                centerY + mainDirY * minX + perpDirY * maxY, 0);
            
            // 立面起点分配：每个立面从对应的边界线的一个端点开始
            boundaryPoints[FacadeType.MainPositive] = corner2;    // 主方向正面：从角点2开始，沿垂直方向到角点3
            boundaryPoints[FacadeType.MainNegative] = corner1;    // 主方向负面：从角点1开始，沿垂直方向到角点4  
            boundaryPoints[FacadeType.PerpPositive] = corner4;    // 垂直方向正面：从角点4开始，沿主方向到角点3（反向）
            boundaryPoints[FacadeType.PerpNegative] = corner1;    // 垂直方向负面：从角点1开始，沿主方向到角点2
            
            System.Diagnostics.Debug.WriteLine($"矩形角点计算:");
            System.Diagnostics.Debug.WriteLine($"  角点1 (minMain,minPerp): ({corner1.X:F2}, {corner1.Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  角点2 (maxMain,minPerp): ({corner2.X:F2}, {corner2.Y:F2})"); 
            System.Diagnostics.Debug.WriteLine($"  角点3 (maxMain,maxPerp): ({corner3.X:F2}, {corner3.Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  角点4 (minMain,maxPerp): ({corner4.X:F2}, {corner4.Y:F2})");

            // 输出调试信息
            System.Diagnostics.Debug.WriteLine($"统一边界点计算完成:");
            System.Diagnostics.Debug.WriteLine($"  中心点: ({centerX:F2}, {centerY:F2})");
            System.Diagnostics.Debug.WriteLine($"  主方向向量: ({mainDirX:F3}, {mainDirY:F3})");
            System.Diagnostics.Debug.WriteLine($"  垂直方向向量: ({perpDirX:F3}, {perpDirY:F3})");
            System.Diagnostics.Debug.WriteLine($"  投影范围: 主方向[{minX:F2}, {maxX:F2}], 垂直方向[{minY:F2}, {maxY:F2}]");

            System.Diagnostics.Debug.WriteLine($"立面起点位置:");
            System.Diagnostics.Debug.WriteLine($"  主方向正面: ({boundaryPoints[FacadeType.MainPositive].X:F2}, {boundaryPoints[FacadeType.MainPositive].Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  主方向负面: ({boundaryPoints[FacadeType.MainNegative].X:F2}, {boundaryPoints[FacadeType.MainNegative].Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  垂直方向正面: ({boundaryPoints[FacadeType.PerpPositive].X:F2}, {boundaryPoints[FacadeType.PerpPositive].Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  垂直方向负面: ({boundaryPoints[FacadeType.PerpNegative].X:F2}, {boundaryPoints[FacadeType.PerpNegative].Y:F2})");

            return boundaryPoints;
        }



        /// <summary>
        /// 为单个立面生成规律点云
        /// </summary>
        private void GenerateFacadePlane(FacadeType facadeType, Vector3 extremePoint,
            float dirX, float dirY, float facadeLength, 
            float pointSpacing, float zSpacing, float zMin, float zMax)
        {
            var facadePoints = new List<Vector3>();

            // 计算沿立面的点数和Z方向的层数
            int pointsAlongFacade = Math.Max(3, (int)(facadeLength / pointSpacing)) + 1;
            int zLayers = Math.Max(3, (int)((zMax - zMin) / zSpacing)) + 1;

            // 沿立面方向生成点 - 从起点开始，而不是从中心
            for (int i = 0; i < pointsAlongFacade; i++)
            {
                // 从立面起点开始，沿方向延伸
                float t = (float)i / (pointsAlongFacade - 1);
                float offsetDistance = t * facadeLength; // 从0到全长，不缩短

                float offsetX = dirX * offsetDistance;
                float offsetY = dirY * offsetDistance;

                // 沿Z方向生成点
                for (int j = 0; j < zLayers; j++)
                {
                    float z = zMin + (zMax - zMin) * j / (zLayers - 1);

                    var newPoint = new Vector3(
                        extremePoint.X + offsetX,
                        extremePoint.Y + offsetY,
                        z
                    );

                    facadePoints.Add(newPoint);
                }
            }

            // 存储到对应的立面状态
            facadeStates[facadeType].GeneratedPoints = facadePoints;
            
            // 同时添加到总的生成点列表中
            generatedFacadePoints.AddRange(facadePoints);

            System.Diagnostics.Debug.WriteLine($"{facadeStates[facadeType].Name}生成: {pointsAlongFacade}x{zLayers} = {facadePoints.Count} 个点");
        }

        /// <summary>
        /// 判断网格是否为边界网格
        /// </summary>
        private bool IsBoundaryGrid(int x, int y, int gridSize, Dictionary<(int, int), List<Vector3>> grid)
        {
            // 如果在整体网格的边缘，肯定是边界
            if (x == 0 || x == gridSize - 1 || y == 0 || y == gridSize - 1)
                return true;
            
            // 检查8个邻居
            int emptyNeighbors = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // 跳过自己
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx < 0 || nx >= gridSize || ny < 0 || ny >= gridSize || !grid.ContainsKey((nx, ny)))
                    {
                        emptyNeighbors++;
                    }
                }
            }
            
            // 如果有2个或更多空邻居，认为是边界
            return emptyNeighbors >= 2;
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
        /// 获取所有生成的立面点云（用于替换原始点云）
        /// </summary>
        public List<Vector3> GetGeneratedFacadePoints()
        {
            if (!isInitialized) return new List<Vector3>();
            
            var visiblePoints = new List<Vector3>();
            foreach (var kvp in facadeStates)
            {
                if (kvp.Value.IsVisible)
                {
                    visiblePoints.AddRange(kvp.Value.GeneratedPoints);
                }
            }
            return visiblePoints;
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
        /// 渲染生成的立面点云（新的渲染方法）
        /// </summary>
        public void RenderGeneratedFacades()
        {
            if (!isInitialized) return;

            foreach (var kvp in facadeStates)
            {
                if (kvp.Value.IsVisible && kvp.Value.GeneratedPoints.Count > 0)
                {
                    // 设置立面颜色
                    OpenTK.Graphics.OpenGL.GL.Color3(kvp.Value.Color.X, kvp.Value.Color.Y, kvp.Value.Color.Z);
                    
                    // 渲染该立面的所有生成点
                    foreach (var point in kvp.Value.GeneratedPoints)
                    {
                        OpenTK.Graphics.OpenGL.GL.Vertex3(point.X, point.Y, point.Z);
                    }
                }
            }
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
        /// 获取建筑真实方位角
        /// </summary>
        /// <returns>建筑主方向的真实方位角（度）</returns>
        public float GetBuildingAzimuth()
        {
            return buildingAzimuth;
        }

        /// <summary>
        /// 获取建筑朝向描述
        /// </summary>
        /// <returns>建筑主方向的指南针方向</returns>
        public string GetBuildingOrientation()
        {
            return isInitialized ? GetCompassDirection(buildingAzimuth) : "未分析";
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