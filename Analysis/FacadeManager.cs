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
        /// 立面类型枚举
        /// </summary>
        public enum FacadeType
        {
            East,   // 东面 (X+)
            West,   // 西面 (X-)
            South,  // 南面 (Y+)
            North   // 北面 (Y-)
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
        }

        private Dictionary<FacadeType, FacadeState> facadeStates;
        private bool isInitialized = false;
        private int lastAnalyzedPointCount = 0;
        private List<Vector3> generatedFacadePoints = new List<Vector3>(); // 所有生成的立面点的集合

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
                [FacadeType.East] = new FacadeState 
                { 
                    Color = new Vector3(1.0f, 0.0f, 0.0f), // 红色
                    Name = "东面",
                    IsVisible = true
                },
                [FacadeType.West] = new FacadeState 
                { 
                    Color = new Vector3(0.0f, 0.0f, 1.0f), // 蓝色
                    Name = "西面",
                    IsVisible = true
                },
                [FacadeType.South] = new FacadeState 
                { 
                    Color = new Vector3(0.0f, 1.0f, 0.0f), // 绿色
                    Name = "南面",
                    IsVisible = true
                },
                [FacadeType.North] = new FacadeState 
                { 
                    Color = new Vector3(1.0f, 1.0f, 0.0f), // 黄色
                    Name = "北面",
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

            // 步骤1: 提取建筑外围点
            System.Diagnostics.Debug.WriteLine("开始提取建筑外围...");
            var outerPoints = ExtractOuterPerimeter(points);
            System.Diagnostics.Debug.WriteLine($"外围点提取: {points.Count} → {outerPoints.Count} 个点");

            if (outerPoints.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("未找到外围点，使用全部点进行分析");
                outerPoints = points;
            }

            // 步骤2: 基于外围点计算建筑的主方向（PCA主成分分析）
            System.Diagnostics.Debug.WriteLine("开始计算建筑主方向...");
            
            // 计算外围点的协方差矩阵（只考虑XY平面）
            double sumX = 0, sumY = 0;
            foreach (var point in outerPoints)
            {
                sumX += point.X;
                sumY += point.Y;
            }
            double meanX = sumX / outerPoints.Count;
            double meanY = sumY / outerPoints.Count;
            
            double cov_xx = 0, cov_xy = 0, cov_yy = 0;
            foreach (var point in outerPoints)
            {
                double dx = point.X - meanX;
                double dy = point.Y - meanY;
                cov_xx += dx * dx;
                cov_xy += dx * dy;
                cov_yy += dy * dy;
            }
            cov_xx /= outerPoints.Count;
            cov_xy /= outerPoints.Count;
            cov_yy /= outerPoints.Count;
            
            // 计算主方向向量（最大特征值对应的特征向量）
            double trace = cov_xx + cov_yy;
            double det = cov_xx * cov_yy - cov_xy * cov_xy;
            double lambda1 = (trace + Math.Sqrt(trace * trace - 4 * det)) / 2;
            
            // 主方向向量（归一化）
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
            
            // 归一化主方向向量
            double length = Math.Sqrt(mainDirX * mainDirX + mainDirY * mainDirY);
            mainDirX /= length;
            mainDirY /= length;
            
            // 垂直方向向量
            double perpDirX = -mainDirY;
            double perpDirY = mainDirX;
            
            double angle = Math.Atan2(mainDirY, mainDirX) * 180.0 / Math.PI;
            System.Diagnostics.Debug.WriteLine($"建筑主方向: ({mainDirX:F3}, {mainDirY:F3}), 角度: {angle:F1}°");
            System.Diagnostics.Debug.WriteLine($"垂直方向: ({perpDirX:F3}, {perpDirY:F3})");
            
            // 步骤3: 将所有点投影到主方向坐标系（基于外围点的方向）
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
            int eastCount = 0, westCount = 0, southCount = 0, northCount = 0;
            
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
                    // 更靠近主方向边界 - 分配给东面或西面
                    if (Math.Abs(mainProj - maxMain) < Math.Abs(mainProj - minMain))
                    {
                        // 更靠近主方向最大值 - 东面
                        facadeStates[FacadeType.East].PointIndices.Add(i);
                        eastCount++;
                        assigned = true;
                    }
                    else
                    {
                        // 更靠近主方向最小值 - 西面
                        facadeStates[FacadeType.West].PointIndices.Add(i);
                        westCount++;
                        assigned = true;
                    }
                }
                else if (perpBoundaryDistance <= perpThickness)
                {
                    // 更靠近垂直方向边界 - 分配给南面或北面
                    if (Math.Abs(perpProj - maxPerp) < Math.Abs(perpProj - minPerp))
                    {
                        // 更靠近垂直方向最大值 - 南面
                        facadeStates[FacadeType.South].PointIndices.Add(i);
                        southCount++;
                        assigned = true;
                    }
                    else
                    {
                        // 更靠近垂直方向最小值 - 北面
                        facadeStates[FacadeType.North].PointIndices.Add(i);
                        northCount++;
                        assigned = true;
                    }
                }
                
                // 每处理1万个点输出一次进度
                if ((i + 1) % 10000 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"立面分析进度: {i + 1}/{points.Count} ({(float)(i + 1) / points.Count * 100:F1}%)");
                }
            }
            
            int totalAssigned = eastCount + westCount + southCount + northCount;
            int unassigned = points.Count - totalAssigned;
            
            System.Diagnostics.Debug.WriteLine($"外围立面分割完成:");
            System.Diagnostics.Debug.WriteLine($"  东面: {eastCount} 个点 (厚度: {mainThickness:F2})");
            System.Diagnostics.Debug.WriteLine($"  西面: {westCount} 个点 (厚度: {mainThickness:F2})"); 
            System.Diagnostics.Debug.WriteLine($"  南面: {southCount} 个点 (厚度: {perpThickness:F2})");
            System.Diagnostics.Debug.WriteLine($"  北面: {northCount} 个点 (厚度: {perpThickness:F2})");
            System.Diagnostics.Debug.WriteLine($"  内部区域: {unassigned} 个点 (未分配)");
            System.Diagnostics.Debug.WriteLine($"  立面覆盖率: {(float)totalAssigned / points.Count * 100:F1}%");
            
            // 厚度均匀性检查
            int eastWestTotal = eastCount + westCount;
            int southNorthTotal = southCount + northCount;
            System.Diagnostics.Debug.WriteLine($"厚度均匀性: 东西总计={eastWestTotal}, 南北总计={southNorthTotal}");

            // 步骤6: 生成规律的立面点云
            System.Diagnostics.Debug.WriteLine("开始生成规律立面点云...");
            GenerateRegularFacades(points, 
                minX: (float)minMain, maxX: (float)maxMain, 
                minY: (float)minPerp, maxY: (float)maxPerp,
                mainDirX: (float)mainDirX, mainDirY: (float)mainDirY,
                perpDirX: (float)perpDirX, perpDirY: (float)perpDirY,
                centerX: (float)meanX, centerY: (float)meanY,
                zMin: min.Z, zMax: max.Z);

            // 输出分析结果
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;
            System.Diagnostics.Debug.WriteLine($"优化立面分析完成，耗时: {duration:F1} ms");
            System.Diagnostics.Debug.WriteLine($"生成的规律立面点总数: {generatedFacadePoints.Count}");

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

            // 生成参数
            float pointSpacing = 0.8f; // 点间距 - 更紧密以减少间隙
            float zSpacing = 1.3f; // Z方向间距 - 更紧密

            // 计算建筑尺寸
            float mainSize = maxX - minX;
            float perpSize = maxY - minY;
            float height = zMax - zMin;

            System.Diagnostics.Debug.WriteLine($"立面生成参数: 点间距={pointSpacing:F2}, Z间距={zSpacing:F2}");
            System.Diagnostics.Debug.WriteLine($"建筑尺寸: 主方向={mainSize:F2}, 垂直方向={perpSize:F2}, 高度={height:F2}");

            // 计算四个立面的统一边界点，确保形成完整的矩形
            var boundaryPoints = CalculateUnifiedBoundaryPoints(
                centerX, centerY, minX, maxX, minY, maxY,
                mainDirX, mainDirY, perpDirX, perpDirY);

            // 为每个立面生成规律点云，基于统一的边界
            GenerateFacadePlane(FacadeType.East, boundaryPoints[FacadeType.East], 
                perpDirX, perpDirY, perpSize, pointSpacing, zSpacing, zMin, zMax);
            
            GenerateFacadePlane(FacadeType.West, boundaryPoints[FacadeType.West], 
                perpDirX, perpDirY, perpSize, pointSpacing, zSpacing, zMin, zMax);
            
            GenerateFacadePlane(FacadeType.South, boundaryPoints[FacadeType.South], 
                mainDirX, mainDirY, mainSize, pointSpacing, zSpacing, zMin, zMax);
            
            GenerateFacadePlane(FacadeType.North, boundaryPoints[FacadeType.North], 
                mainDirX, mainDirY, mainSize, pointSpacing, zSpacing, zMin, zMax);

            System.Diagnostics.Debug.WriteLine($"规律立面生成完成:");
            foreach (var kvp in facadeStates)
            {
                System.Diagnostics.Debug.WriteLine($"  {kvp.Value.Name}: {kvp.Value.GeneratedPoints.Count} 个规律点");
            }
        }

        /// <summary>
        /// 计算统一的边界点，确保四个立面形成完整的矩形
        /// </summary>
        private Dictionary<FacadeType, Vector3> CalculateUnifiedBoundaryPoints(
            float centerX, float centerY, float minX, float maxX, float minY, float maxY,
            float mainDirX, float mainDirY, float perpDirX, float perpDirY)
        {
            var boundaryPoints = new Dictionary<FacadeType, Vector3>();

            // 计算四个立面的起点，确保它们能够连接成一个完整的矩形
            // 东面：主方向最大值，垂直方向从最小值到最大值
            boundaryPoints[FacadeType.East] = new Vector3(
                centerX + mainDirX * maxX + perpDirX * minY,
                centerY + mainDirY * maxX + perpDirY * minY,
                0
            );

            // 西面：主方向最小值，垂直方向从最小值到最大值
            boundaryPoints[FacadeType.West] = new Vector3(
                centerX + mainDirX * minX + perpDirX * minY,
                centerY + mainDirY * minX + perpDirY * minY,
                0
            );

            // 南面：垂直方向最大值，主方向从最小值到最大值
            boundaryPoints[FacadeType.South] = new Vector3(
                centerX + mainDirX * minX + perpDirX * maxY,
                centerY + mainDirY * minX + perpDirY * maxY,
                0
            );

            // 北面：垂直方向最小值，主方向从最小值到最大值
            boundaryPoints[FacadeType.North] = new Vector3(
                centerX + mainDirX * minX + perpDirX * minY,
                centerY + mainDirY * minX + perpDirY * minY,
                0
            );

            // 输出调试信息
            System.Diagnostics.Debug.WriteLine($"统一边界点计算完成:");
            System.Diagnostics.Debug.WriteLine($"  中心点: ({centerX:F2}, {centerY:F2})");
            System.Diagnostics.Debug.WriteLine($"  主方向向量: ({mainDirX:F3}, {mainDirY:F3})");
            System.Diagnostics.Debug.WriteLine($"  垂直方向向量: ({perpDirX:F3}, {perpDirY:F3})");
            System.Diagnostics.Debug.WriteLine($"  投影范围: 主方向[{minX:F2}, {maxX:F2}], 垂直方向[{minY:F2}, {maxY:F2}]");

            System.Diagnostics.Debug.WriteLine($"立面起点位置:");
            System.Diagnostics.Debug.WriteLine($"  东面: ({boundaryPoints[FacadeType.East].X:F2}, {boundaryPoints[FacadeType.East].Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  西面: ({boundaryPoints[FacadeType.West].X:F2}, {boundaryPoints[FacadeType.West].Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  南面: ({boundaryPoints[FacadeType.South].X:F2}, {boundaryPoints[FacadeType.South].Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  北面: ({boundaryPoints[FacadeType.North].X:F2}, {boundaryPoints[FacadeType.North].Y:F2})");

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
    }
}