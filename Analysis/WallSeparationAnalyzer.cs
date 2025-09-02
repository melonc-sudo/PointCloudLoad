using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoadPCDtest.Analysis
{
    /// <summary>
    /// 大楼墙面分离分析器
    /// </summary>
    public class WallSeparationAnalyzer
    {
        /// <summary>
        /// 检测到的墙面平面
        /// </summary>
        public class Wall
        {
            public List<Vector3> Points { get; set; } = new List<Vector3>();
            public Vector3 Normal { get; set; }
            public Vector3 CenterPoint { get; set; }
            public float Distance { get; set; } // 平面方程的d参数
            public WallDirection Direction { get; set; }
            public Vector3 Color { get; set; }
            public string Name { get; set; }

            public Wall(Vector3 normal, float distance)
            {
                Normal = normal.Normalized();
                Distance = distance;
                DetermineDirection();
                AssignColor();
            }

            private void DetermineDirection()
            {
                Vector3 n = Normal.Normalized();
                
                // 计算法向量与各个主要方向的点积
                float dotX = Math.Abs(Vector3.Dot(n, Vector3.UnitX));
                float dotY = Math.Abs(Vector3.Dot(n, Vector3.UnitY));
                float dotZ = Math.Abs(Vector3.Dot(n, Vector3.UnitZ));

                // 如果主要朝向Z轴，说明是水平面（地面/天花板）
                if (dotZ > 0.85f) // 提高阈值，更严格识别水平面
                {
                    Direction = WallDirection.Horizontal;
                    Name = n.Z > 0 ? "天花板" : "地面";
                    System.Diagnostics.Debug.WriteLine($"识别为水平面: ({n.X:F3}, {n.Y:F3}, {n.Z:F3}) -> {Name} (dotZ:{dotZ:F3})");
                    return;
                }

                // 检查是否为真正的垂直墙面（Z分量不能太大）
                if (dotZ > 0.5f) // 如果Z分量过大，可能是倾斜面，归类为水平面
                {
                    Direction = WallDirection.Horizontal;
                    Name = "倾斜面";
                    System.Diagnostics.Debug.WriteLine($"识别为倾斜面: ({n.X:F3}, {n.Y:F3}, {n.Z:F3}) -> {Name} (dotZ:{dotZ:F3})");
                    return;
                }

                // 垂直墙面分类 - 增加更明确的区分
                float threshold = 0.15f; // 增加方向判断的差异阈值
                
                if (dotX > dotY + threshold && dotX > 0.3f) // 确保X分量足够大
                {
                    // X轴方向的墙面（东西墙）- 法向量主要在X方向
                    Direction = n.X > 0 ? WallDirection.East : WallDirection.West;
                    Name = Direction == WallDirection.East ? "东墙" : "西墙";
                }
                else if (dotY > dotX + threshold && dotY > 0.3f) // 确保Y分量足够大
                {
                    // Y轴方向的墙面（南北墙）- 法向量主要在Y方向
                    Direction = n.Y > 0 ? WallDirection.North : WallDirection.South;
                    Name = Direction == WallDirection.North ? "北墙" : "南墙";
                }
                else if (dotX > 0.3f && dotX >= dotY) // X分量较大但差异不明显
                {
                    Direction = n.X > 0 ? WallDirection.East : WallDirection.West;
                    Name = Direction == WallDirection.East ? "东墙" : "西墙";
                }
                else if (dotY > 0.3f && dotY > dotX) // Y分量较大但差异不明显
                {
                    Direction = n.Y > 0 ? WallDirection.North : WallDirection.South;
                    Name = Direction == WallDirection.North ? "北墙" : "南墙";
                }
                else
                {
                    // 法向量在XY平面上都很小，可能是问题数据，归类为水平面
                    Direction = WallDirection.Horizontal;
                    Name = "未定义面";
                    System.Diagnostics.Debug.WriteLine($"法向量异常，归类为水平面: ({n.X:F3}, {n.Y:F3}, {n.Z:F3}) -> {Name}");
                    return;
                }

                // 调试输出法向量和方向判断
                System.Diagnostics.Debug.WriteLine($"法向量分析: ({n.X:F3}, {n.Y:F3}, {n.Z:F3}) -> {Name} (dotX:{dotX:F3}, dotY:{dotY:F3}, dotZ:{dotZ:F3})");
            }

            public void AssignColor()
            {
                // 使用传统的switch语句以确保兼容性
                switch (Direction)
                {
                    case WallDirection.North:
                        Color = new Vector3(1.0f, 0.2f, 0.2f);    // 红色 - 北墙
                        break;
                    case WallDirection.South:
                        Color = new Vector3(0.2f, 1.0f, 0.2f);    // 绿色 - 南墙
                        break;
                    case WallDirection.East:
                        Color = new Vector3(0.2f, 0.2f, 1.0f);     // 蓝色 - 东墙
                        break;
                    case WallDirection.West:
                        Color = new Vector3(1.0f, 1.0f, 0.2f);     // 黄色 - 西墙
                        break;
                    case WallDirection.Horizontal:
                        Color = new Vector3(0.8f, 0.8f, 0.8f);    // 灰色 - 水平面
                        break;
                    default:
                        Color = new Vector3(0.5f, 0.5f, 0.5f);    // 默认灰色
                        break;
                }
            }

            public void UpdateCenterPoint()
            {
                if (Points.Count > 0)
                {
                    Vector3 sum = Vector3.Zero;
                    foreach (var point in Points)
                    {
                        sum += point;
                    }
                    CenterPoint = sum / Points.Count;
                }
            }
        }

        public enum WallDirection
        {
            North,      // 北墙 (+Y方向)
            South,      // 南墙 (-Y方向)  
            East,       // 东墙 (+X方向)
            West,       // 西墙 (-X方向)
            Horizontal  // 水平面（地面/天花板）
        }

        // RANSAC参数
        public int MaxIterations { get; set; } = 500; // 减少迭代次数提高速度
        public float DistanceThreshold { get; set; } = 0.1f; // 10cm阈值
        public int MinPointsForPlane { get; set; } = 100;
        public float MinVerticalAngle { get; set; } = 60.0f; // 最小垂直角度（度）
        public float WallMergeAngleThreshold { get; set; } = 15.0f; // 墙面合并角度阈值（度）- 稍微放宽
        public float WallMergeDistanceThreshold { get; set; } = 0.3f; // 墙面合并距离阈值（米）- 更严格
        
        // 边界约束参数
        public bool EnableBoundaryConstraints { get; set; } = false; // 默认禁用边界约束，保持墙面完整性
        public float BoundaryConstraintStrength { get; set; } = 0.1f; // 约束强度 (0.0-1.0, 越小越温和)

        /// <summary>
        /// 分析点云并分离墙面
        /// </summary>
        public List<Wall> AnalyzeWalls(List<Vector3> points)
        {
            if (points == null || points.Count < MinPointsForPlane * 3)
            {
                System.Diagnostics.Debug.WriteLine("点云数据不足，无法进行墙面分析");
                return new List<Wall>();
            }

            System.Diagnostics.Debug.WriteLine($"开始墙面分析，点数: {points.Count:N0}");

            List<Wall> walls = new List<Wall>();
            List<Vector3> remainingPoints = new List<Vector3>(points);

            // 迭代检测多个平面
            int maxWalls = 8; // 最多检测8个平面（4面墙+地面+天花板+其他）
            
            for (int wallIndex = 0; wallIndex < maxWalls && remainingPoints.Count >= MinPointsForPlane; wallIndex++)
            {
                var plane = DetectLargestPlane(remainingPoints);
                if (plane == null)
                    break;

                // 提取属于此平面的所有点
                var planePoints = ExtractPointsFromPlane(remainingPoints, plane.Normal, plane.Distance);
                
                if (planePoints.Count >= MinPointsForPlane)
                {
                    plane.Points = planePoints;
                    plane.UpdateCenterPoint();
                    walls.Add(plane);

                    // 从剩余点中移除已分类的点
                    remainingPoints = remainingPoints.Except(planePoints).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"检测到{plane.Name}: {planePoints.Count:N0}个点, 法向量: ({plane.Normal.X:F3}, {plane.Normal.Y:F3}, {plane.Normal.Z:F3})");
                }
            }

            System.Diagnostics.Debug.WriteLine($"初步检测到 {walls.Count} 个平面");

            // 合并相似的墙面
            walls = MergeSimilarWalls(walls);

            // 移除噪声墙面
            walls = RemoveNoiseWalls(walls, MinPointsForPlane);

            // 应用墙面边界约束，防止墙面突出（可选）
            if (EnableBoundaryConstraints)
            {
                walls = ApplyWallBoundaryConstraints(walls);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("边界约束功能已禁用 - 保持墙面完整性和连续性");
            }

            // 按点数排序，优先显示主要墙面
            walls = walls.OrderByDescending(w => w.Points.Count).ToList();

            System.Diagnostics.Debug.WriteLine($"墙面分析完成，最终得到 {walls.Count} 个主要墙面:");
            foreach (var wall in walls)
            {
                System.Diagnostics.Debug.WriteLine($"  {wall.Name}: {wall.Points.Count:N0} 个点");
            }

            return walls;
        }

        /// <summary>
        /// 使用RANSAC算法检测最大的平面
        /// </summary>
        private Wall DetectLargestPlane(List<Vector3> points)
        {
            if (points.Count < 3)
                return null;

            Wall bestPlane = null;
            int maxInliers = 0;

            Random rand = new Random();

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                // 随机选择3个点定义一个平面
                var indices = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    int attempts = 0;
                    do
                    {
                        indices[i] = rand.Next(points.Count);
                        attempts++;
                    } while (attempts < 100 && (i > 0 && (indices[i] == indices[0] || (i > 1 && indices[i] == indices[1]))));
                }

                var p1 = points[indices[0]];
                var p2 = points[indices[1]];
                var p3 = points[indices[2]];

                // 计算平面法向量
                var v1 = p2 - p1;
                var v2 = p3 - p1;
                var normal = Vector3.Cross(v1, v2);

                if (normal.Length < 0.001f) // 三点共线
                    continue;

                normal = normal.Normalized();

                // 计算平面方程的d参数
                float d = Vector3.Dot(normal, p1);

                // 统计内点数量
                int inliers = 0;
                foreach (var point in points)
                {
                    float distance = Math.Abs(Vector3.Dot(normal, point) - d);
                    if (distance < DistanceThreshold)
                    {
                        inliers++;
                    }
                }

                // 更新最佳平面
                if (inliers > maxInliers)
                {
                    maxInliers = inliers;
                    bestPlane = new Wall(normal, d);
                }

            }

            return maxInliers >= MinPointsForPlane ? bestPlane : null;
        }

        /// <summary>
        /// 提取属于指定平面的所有点
        /// </summary>
        private List<Vector3> ExtractPointsFromPlane(List<Vector3> points, Vector3 normal, float distance)
        {
            List<Vector3> planePoints = new List<Vector3>();

            foreach (var point in points)
            {
                float pointDistance = Math.Abs(Vector3.Dot(normal, point) - distance);
                if (pointDistance < DistanceThreshold)
                {
                    planePoints.Add(point);
                }
            }

            return planePoints;
        }

        /// <summary>
        /// 只获取垂直墙面（排除地面和天花板）
        /// </summary>
        public List<Wall> GetVerticalWallsOnly(List<Wall> walls)
        {
            return walls.Where(w => w.Direction != WallDirection.Horizontal).ToList();
        }

        /// <summary>
        /// 获取四个主要方向的墙面
        /// </summary>
        public Dictionary<WallDirection, Wall> GetMainWalls(List<Wall> walls)
        {
            var mainWalls = new Dictionary<WallDirection, Wall>();
            var verticalWalls = GetVerticalWallsOnly(walls);

            foreach (var direction in new[] { WallDirection.North, WallDirection.South, WallDirection.East, WallDirection.West })
            {
                var wall = verticalWalls
                    .Where(w => w.Direction == direction)
                    .OrderByDescending(w => w.Points.Count)
                    .FirstOrDefault();

                if (wall != null)
                {
                    mainWalls[direction] = wall;
                }
            }

            return mainWalls;
        }

        /// <summary>
        /// 生成墙面分析报告
        /// </summary>
        public string GenerateWallReport(List<Wall> walls)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== 墙面分析报告 ===");
            report.AppendLine($"检测时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"检测到 {walls.Count} 个平面");
            report.AppendLine();

            var verticalWalls = GetVerticalWallsOnly(walls);
            var horizontalWalls = walls.Where(w => w.Direction == WallDirection.Horizontal).ToList();

            report.AppendLine($"垂直墙面: {verticalWalls.Count} 个");
            foreach (var wall in verticalWalls.OrderByDescending(w => w.Points.Count))
            {
                report.AppendLine($"  {wall.Name}: {wall.Points.Count:N0} 个点, 法向量: ({wall.Normal.X:F3}, {wall.Normal.Y:F3}, {wall.Normal.Z:F3})");
            }

            if (horizontalWalls.Any())
            {
                report.AppendLine();
                report.AppendLine($"水平面: {horizontalWalls.Count} 个");
                foreach (var wall in horizontalWalls.OrderByDescending(w => w.Points.Count))
                {
                    report.AppendLine($"  {wall.Name}: {wall.Points.Count:N0} 个点");
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// 合并相似的墙面（解决同一墙面被识别为多个平面的问题）
        /// </summary>
        private List<Wall> MergeSimilarWalls(List<Wall> walls)
        {
            if (walls.Count <= 1) return walls;

            System.Diagnostics.Debug.WriteLine($"开始墙面合并，原始平面数: {walls.Count}");
            
            // 先更新所有墙面的中心点
            foreach (var wall in walls)
            {
                wall.UpdateCenterPoint();
            }

            // 使用基于空间位置的智能合并算法
            return MergeWallsBySpatialClustering(walls);
        }

        /// <summary>
        /// 基于空间聚类的墙面合并算法
        /// </summary>
        private List<Wall> MergeWallsBySpatialClustering(List<Wall> walls)
        {
            var verticalWalls = walls.Where(w => w.Direction != WallDirection.Horizontal).ToList();
            var horizontalWalls = walls.Where(w => w.Direction == WallDirection.Horizontal).ToList();

            System.Diagnostics.Debug.WriteLine($"初始分类 - 垂直墙面: {verticalWalls.Count}, 水平墙面: {horizontalWalls.Count}");

            // 输出每个墙面的详细信息
            foreach (var wall in walls)
            {
                System.Diagnostics.Debug.WriteLine($"墙面详情: {wall.Name}, 法向量: ({wall.Normal.X:F3}, {wall.Normal.Y:F3}, {wall.Normal.Z:F3}), 中心: ({wall.CenterPoint.X:F2}, {wall.CenterPoint.Y:F2}, {wall.CenterPoint.Z:F2})");
            }

            // 对垂直墙面进行空间聚类
            var mergedVerticalWalls = ClusterVerticalWallsByPosition(verticalWalls);
            
            // 水平墙面保持不变
            var result = new List<Wall>();
            result.AddRange(mergedVerticalWalls);
            result.AddRange(horizontalWalls);

            System.Diagnostics.Debug.WriteLine($"墙面合并完成：从 {walls.Count} 个平面合并为 {result.Count} 个墙面");
            return result;
        }

        /// <summary>
        /// 基于法向量和空间位置的智能聚类垂直墙面
        /// </summary>
        private List<Wall> ClusterVerticalWallsByPosition(List<Wall> verticalWalls)
        {
            if (verticalWalls.Count == 0) return new List<Wall>();

            var result = new List<Wall>();

            System.Diagnostics.Debug.WriteLine($"开始智能墙面聚类，总计{verticalWalls.Count}个垂直墙面");

            // 步骤1：按法向量主要方向分类
            var eastWestWalls = new List<Wall>(); // X轴主导的墙面（东西墙）
            var northSouthWalls = new List<Wall>(); // Y轴主导的墙面（南北墙）

            foreach (var wall in verticalWalls)
            {
                Vector3 n = wall.Normal.Normalized();
                float dotX = Math.Abs(Vector3.Dot(n, Vector3.UnitX));
                float dotY = Math.Abs(Vector3.Dot(n, Vector3.UnitY));

                if (dotX > dotY)
                {
                    eastWestWalls.Add(wall);
                    System.Diagnostics.Debug.WriteLine($"分类为东西墙候选: {wall.Name}, 法向量: ({n.X:F3}, {n.Y:F3}, {n.Z:F3}), 中心: ({wall.CenterPoint.X:F2}, {wall.CenterPoint.Y:F2})");
                }
                else
                {
                    northSouthWalls.Add(wall);
                    System.Diagnostics.Debug.WriteLine($"分类为南北墙候选: {wall.Name}, 法向量: ({n.X:F3}, {n.Y:F3}, {n.Z:F3}), 中心: ({wall.CenterPoint.X:F2}, {wall.CenterPoint.Y:F2})");
                }
            }

            System.Diagnostics.Debug.WriteLine($"法向量分类结果 - 东西墙候选: {eastWestWalls.Count}个, 南北墙候选: {northSouthWalls.Count}个");

            // 步骤2：在每个类别内按位置分组
            if (eastWestWalls.Count > 0)
            {
                var processedEastWest = ProcessEastWestWalls(eastWestWalls);
                result.AddRange(processedEastWest);
            }

            if (northSouthWalls.Count > 0)
            {
                var processedNorthSouth = ProcessNorthSouthWalls(northSouthWalls);
                result.AddRange(processedNorthSouth);
            }

            System.Diagnostics.Debug.WriteLine($"智能聚类完成：最终得到 {result.Count} 个墙面");
            return result;
        }

        /// <summary>
        /// 处理东西墙：按X坐标位置分组
        /// </summary>
        private List<Wall> ProcessEastWestWalls(List<Wall> eastWestWalls)
        {
            var result = new List<Wall>();
            
            if (eastWestWalls.Count == 0) return result;

            // 按X坐标排序
            var sortedByX = eastWestWalls.OrderBy(w => w.CenterPoint.X).ToList();

            if (sortedByX.Count == 1)
            {
                // 只有一个东西墙候选，根据X位置判断是东墙还是西墙
                var wall = sortedByX[0];
                var mergedWall = CreateSingleWallFromGroup(new List<Wall> { wall }, true, wall.CenterPoint.X > -25);
                result.Add(mergedWall);
            }
            else
            {
                // 多个东西墙候选，按位置分组
                float minX = sortedByX.First().CenterPoint.X;
                float maxX = sortedByX.Last().CenterPoint.X;
                float middleX = (minX + maxX) / 2;

                System.Diagnostics.Debug.WriteLine($"东西墙X位置分析: 最小={minX:F1}, 最大={maxX:F1}, 中间={middleX:F1}");

                var westGroup = sortedByX.Where(w => w.CenterPoint.X <= middleX).ToList();
                var eastGroup = sortedByX.Where(w => w.CenterPoint.X > middleX).ToList();

                System.Diagnostics.Debug.WriteLine($"东西墙分组: 西墙组({westGroup.Count}个), 东墙组({eastGroup.Count}个)");

                if (westGroup.Any())
                {
                    var westWall = CreateSingleWallFromGroup(westGroup, true, false); // X轴，西墙
                    result.Add(westWall);
                }

                if (eastGroup.Any())
                {
                    var eastWall = CreateSingleWallFromGroup(eastGroup, true, true); // X轴，东墙
                    result.Add(eastWall);
                }
            }

            return result;
        }

        /// <summary>
        /// 处理南北墙：按Y坐标位置分组
        /// </summary>
        private List<Wall> ProcessNorthSouthWalls(List<Wall> northSouthWalls)
        {
            var result = new List<Wall>();
            
            if (northSouthWalls.Count == 0) return result;

            // 按Y坐标排序
            var sortedByY = northSouthWalls.OrderBy(w => w.CenterPoint.Y).ToList();

            if (sortedByY.Count == 1)
            {
                // 只有一个南北墙候选，根据Y位置判断是南墙还是北墙
                var wall = sortedByY[0];
                var mergedWall = CreateSingleWallFromGroup(new List<Wall> { wall }, false, wall.CenterPoint.Y > 0);
                result.Add(mergedWall);
            }
            else
            {
                // 多个南北墙候选，按位置分组
                float minY = sortedByY.First().CenterPoint.Y;
                float maxY = sortedByY.Last().CenterPoint.Y;
                float middleY = (minY + maxY) / 2;

                System.Diagnostics.Debug.WriteLine($"南北墙Y位置分析: 最小={minY:F1}, 最大={maxY:F1}, 中间={middleY:F1}");

                var southGroup = sortedByY.Where(w => w.CenterPoint.Y <= middleY).ToList();
                var northGroup = sortedByY.Where(w => w.CenterPoint.Y > middleY).ToList();

                System.Diagnostics.Debug.WriteLine($"南北墙分组: 南墙组({southGroup.Count}个), 北墙组({northGroup.Count}个)");

                if (southGroup.Any())
                {
                    var southWall = CreateSingleWallFromGroup(southGroup, false, false); // Y轴，南墙
                    result.Add(southWall);
                }

                if (northGroup.Any())
                {
                    var northWall = CreateSingleWallFromGroup(northGroup, false, true); // Y轴，北墙
                    result.Add(northWall);
                }
            }

            return result;
        }

        /// <summary>
        /// 从一组墙面创建单一合并墙面
        /// </summary>
        private Wall CreateSingleWallFromGroup(List<Wall> group, bool isXAxis, bool isPositiveDirection)
        {
            if (group.Count == 0) return null;

            // 选择点数最多的作为主墙面
            var mainWall = group.OrderByDescending(w => w.Points.Count).First();
            
            // 计算平均法向量
            var avgNormal = Vector3.Zero;
            foreach (var wall in group)
            {
                avgNormal += wall.Normal;
            }
            avgNormal = avgNormal.Normalized();

            // 根据轴向和方向确定墙面类型
            WallDirection direction;
            string name;

            if (isXAxis) // 东西墙
            {
                if (isPositiveDirection)
                {
                    direction = WallDirection.East;
                    name = "东墙";
                }
                else
                {
                    direction = WallDirection.West;
                    name = "西墙";
                }
            }
            else // 南北墙
            {
                if (isPositiveDirection)
                {
                    direction = WallDirection.North;
                    name = "北墙";
                }
                else
                {
                    direction = WallDirection.South;
                    name = "南墙";
                }
            }

            // 创建合并后的墙面
            var mergedWall = new Wall(avgNormal, mainWall.Distance)
            {
                Direction = direction,
                Name = name,
                Points = new List<Vector3>()
            };

            // 合并所有点
            foreach (var wall in group)
            {
                mergedWall.Points.AddRange(wall.Points);
            }

            // 更新中心点和颜色
            mergedWall.UpdateCenterPoint();
            mergedWall.AssignColor();

            System.Diagnostics.Debug.WriteLine($"创建{name}: 合并了{group.Count}个墙面片段, 总点数: {mergedWall.Points.Count:N0}, 最终位置: ({mergedWall.CenterPoint.X:F2}, {mergedWall.CenterPoint.Y:F2}), 颜色: ({mergedWall.Color.X:F1}, {mergedWall.Color.Y:F1}, {mergedWall.Color.Z:F1})");
            return mergedWall;
        }

        /// <summary>
        /// 从一组墙面创建合并的墙面（废弃的旧版本）
        /// </summary>
        [Obsolete("使用CreateSingleWallFromGroup替代")]
        private Wall CreateMergedWallFromGroup(List<Wall> group, bool isXAxis, bool isMinPosition)
        {
            // 选择点数最多的作为主墙面
            var mainWall = group.OrderByDescending(w => w.Points.Count).First();
            
            // 计算平均法向量
            var avgNormal = Vector3.Zero;
            foreach (var wall in group)
            {
                avgNormal += wall.Normal;
            }
            avgNormal = avgNormal.Normalized();

            // 根据轴向和位置确定方向
            WallDirection direction;
            string name;

            if (isXAxis)
            {
                if (isMinPosition) // X轴较小位置通常是西墙
                {
                    direction = WallDirection.West;
                    name = "西墙";
                }
                else // X轴较大位置通常是东墙
                {
                    direction = WallDirection.East;
                    name = "东墙";
                }
            }
            else
            {
                if (isMinPosition) // Y轴较小位置通常是南墙
                {
                    direction = WallDirection.South;
                    name = "南墙";
                }
                else // Y轴较大位置通常是北墙
                {
                    direction = WallDirection.North;
                    name = "北墙";
                }
            }

            // 创建合并后的墙面
            var mergedWall = new Wall(avgNormal, mainWall.Distance)
            {
                Direction = direction,
                Name = name,
                Points = new List<Vector3>()
            };

            // 合并所有点
            foreach (var wall in group)
            {
                mergedWall.Points.AddRange(wall.Points);
            }

            // 更新中心点和颜色
            mergedWall.UpdateCenterPoint();
            mergedWall.AssignColor(); // 关键：分配正确的颜色！

            System.Diagnostics.Debug.WriteLine($"创建{name}: 合并了{group.Count}个墙面, 总点数: {mergedWall.Points.Count:N0}, 最终位置: ({mergedWall.CenterPoint.X:F2}, {mergedWall.CenterPoint.Y:F2}), 颜色: ({mergedWall.Color.X:F1}, {mergedWall.Color.Y:F1}, {mergedWall.Color.Z:F1})");
            return mergedWall;
        }



        /// <summary>
        /// 判断两个墙面是否相似（已废弃，使用新的空间聚类算法）
        /// </summary>
        [Obsolete("使用新的空间聚类算法替代")]
        private bool AreSimilarWalls(Wall wall1, Wall wall2)
        {
            // 此方法已被新的空间聚类算法替代
            return false;
        }

        /// <summary>
        /// 检查两个方向是否是相对的（如东墙和西墙）
        /// </summary>
        private bool AreOppositeDirections(WallDirection dir1, WallDirection dir2)
        {
            return (dir1 == WallDirection.North && dir2 == WallDirection.South) ||
                   (dir1 == WallDirection.South && dir2 == WallDirection.North) ||
                   (dir1 == WallDirection.East && dir2 == WallDirection.West) ||
                   (dir1 == WallDirection.West && dir2 == WallDirection.East);
        }

        /// <summary>
        /// 将多个相似墙面合并为一个墙面
        /// </summary>
        private Wall MergeWalls(List<Wall> wallsToMerge)
        {
            if (wallsToMerge.Count == 1)
                return wallsToMerge[0];

            // 选择点数最多的墙面作为主墙面
            var mainWall = wallsToMerge.OrderByDescending(w => w.Points.Count).First();
            
            // 创建新的合并墙面
            var mergedWall = new Wall(mainWall.Normal, mainWall.Distance)
            {
                Direction = mainWall.Direction,
                Name = DetermineMainWallName(wallsToMerge),
                Points = new List<Vector3>()
            };

            // 合并所有点
            foreach (var wall in wallsToMerge)
            {
                mergedWall.Points.AddRange(wall.Points);
            }

            // 重新计算中心点
            mergedWall.UpdateCenterPoint();

            System.Diagnostics.Debug.WriteLine($"创建合并墙面: {mergedWall.Name}, 总点数: {mergedWall.Points.Count:N0}");
            return mergedWall;
        }

        /// <summary>
        /// 确定合并墙面的主要名称
        /// </summary>
        private string DetermineMainWallName(List<Wall> walls)
        {
            // 统计各个名称的点数
            var namePointCounts = walls.GroupBy(w => w.Name)
                                      .ToDictionary(g => g.Key, g => g.Sum(w => w.Points.Count));

            // 返回点数最多的名称
            return namePointCounts.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// 进一步优化：移除点数过少的噪声墙面
        /// </summary>
        public List<Wall> RemoveNoiseWalls(List<Wall> walls, int minPointsThreshold = 500)
        {
            var filteredWalls = walls.Where(w => w.Points.Count >= minPointsThreshold).ToList();
            
            if (filteredWalls.Count < walls.Count)
            {
                System.Diagnostics.Debug.WriteLine($"移除噪声墙面: {walls.Count - filteredWalls.Count} 个 (点数小于 {minPointsThreshold})");
            }

            return filteredWalls;
        }

        /// <summary>
        /// 墙面边界信息
        /// </summary>
        public class WallBounds
        {
            public float MinX { get; set; }
            public float MaxX { get; set; }
            public float MinY { get; set; }
            public float MaxY { get; set; }
            public float MinZ { get; set; }
            public float MaxZ { get; set; }
        }

        /// <summary>
        /// 计算墙面的实际边界
        /// </summary>
        private WallBounds CalculateWallBounds(Wall wall)
        {
            if (wall.Points.Count == 0)
                return new WallBounds();

            var bounds = new WallBounds
            {
                MinX = wall.Points.Min(p => p.X),
                MaxX = wall.Points.Max(p => p.X),
                MinY = wall.Points.Min(p => p.Y),
                MaxY = wall.Points.Max(p => p.Y),
                MinZ = wall.Points.Min(p => p.Z),
                MaxZ = wall.Points.Max(p => p.Z)
            };

            return bounds;
        }

        /// <summary>
        /// 对墙面进行边界约束处理，防止墙面突出
        /// </summary>
        public List<Wall> ApplyWallBoundaryConstraints(List<Wall> walls)
        {
            if (walls.Count <= 1) return walls;

            System.Diagnostics.Debug.WriteLine($"开始墙面边界约束处理... (强度: {BoundaryConstraintStrength:F2})");

            var constrainedWalls = new List<Wall>();
            var verticalWalls = walls.Where(w => w.Direction != WallDirection.Horizontal).ToList();
            var horizontalWalls = walls.Where(w => w.Direction == WallDirection.Horizontal).ToList();

            // 计算整体建筑边界
            var buildingBounds = CalculateBuildingBounds(walls);
            System.Diagnostics.Debug.WriteLine($"建筑整体边界: X({buildingBounds.MinX:F2} ~ {buildingBounds.MaxX:F2}), Y({buildingBounds.MinY:F2} ~ {buildingBounds.MaxY:F2})");

            // 对垂直墙面进行边界约束
            foreach (var wall in verticalWalls)
            {
                var constrainedWall = ApplyBoundaryConstraintToWall(wall, verticalWalls, buildingBounds);
                constrainedWalls.Add(constrainedWall);
            }

            // 水平墙面保持原样
            constrainedWalls.AddRange(horizontalWalls);

            System.Diagnostics.Debug.WriteLine($"墙面边界约束完成，处理了 {verticalWalls.Count} 个垂直墙面");
            return constrainedWalls;
        }

        /// <summary>
        /// 计算建筑整体边界
        /// </summary>
        private WallBounds CalculateBuildingBounds(List<Wall> walls)
        {
            var allPoints = walls.SelectMany(w => w.Points).ToList();
            
            return new WallBounds
            {
                MinX = allPoints.Min(p => p.X),
                MaxX = allPoints.Max(p => p.X),
                MinY = allPoints.Min(p => p.Y),
                MaxY = allPoints.Max(p => p.Y),
                MinZ = allPoints.Min(p => p.Z),
                MaxZ = allPoints.Max(p => p.Z)
            };
        }

        /// <summary>
        /// 对单个墙面应用边界约束
        /// </summary>
        private Wall ApplyBoundaryConstraintToWall(Wall wall, List<Wall> allVerticalWalls, WallBounds buildingBounds)
        {
            var constrainedPoints = new List<Vector3>();
            
            // 根据墙面方向确定约束范围
            var constraints = CalculateWallConstraints(wall, allVerticalWalls, buildingBounds);

            // 过滤超出约束范围的点
            foreach (var point in wall.Points)
            {
                if (IsPointWithinConstraints(point, wall.Direction, constraints))
                {
                    constrainedPoints.Add(point);
                }
            }

            // 创建约束后的墙面
            var constrainedWall = new Wall(wall.Normal, wall.Distance)
            {
                Direction = wall.Direction,
                Name = wall.Name,
                Points = constrainedPoints,
                Color = wall.Color
            };

            constrainedWall.UpdateCenterPoint();

            float reductionPercentage = wall.Points.Count > 0 ? 
                ((float)(wall.Points.Count - constrainedPoints.Count) / wall.Points.Count * 100) : 0;
                
            System.Diagnostics.Debug.WriteLine($"{wall.Name} 边界约束: {wall.Points.Count:N0} -> {constrainedPoints.Count:N0} 个点 (减少{reductionPercentage:F1}%)");
            
            if (reductionPercentage > 10)
            {
                System.Diagnostics.Debug.WriteLine($"  提示: {wall.Name}裁剪了{reductionPercentage:F1}%的点，如果觉得太多可以降低BoundaryConstraintStrength");
            }
            
            return constrainedWall;
        }

        /// <summary>
        /// 计算墙面的约束范围（温和版本）
        /// </summary>
        private WallBounds CalculateWallConstraints(Wall wall, List<Wall> allVerticalWalls, WallBounds buildingBounds)
        {
            var constraints = new WallBounds
            {
                MinX = buildingBounds.MinX,
                MaxX = buildingBounds.MaxX,
                MinY = buildingBounds.MinY,
                MaxY = buildingBounds.MaxY,
                MinZ = buildingBounds.MinZ,
                MaxZ = buildingBounds.MaxZ
            };

            // 计算当前墙面的原始边界
            var currentBounds = CalculateWallBounds(wall);
            float bufferRatio = Math.Max(0.05f, BoundaryConstraintStrength * 0.5f); // 使用可调节的缓冲比例
            
            // 统一计算最小缓冲区大小，避免重复定义 - 使用更大的基础缓冲区
            float minBuffer = 0.5f + (1.0f - BoundaryConstraintStrength) * 1.5f; // 强度越低，最小缓冲越大

            // 根据墙面方向和相邻墙面位置调整约束（更温和的策略）
            switch (wall.Direction)
            {
                case WallDirection.East:
                case WallDirection.West:
                    // 东西墙：限制Y方向的延伸，但保持更大的缓冲区
                    var northWall = allVerticalWalls.FirstOrDefault(w => w.Direction == WallDirection.North);
                    var southWall = allVerticalWalls.FirstOrDefault(w => w.Direction == WallDirection.South);
                    
                    float currentYRange = currentBounds.MaxY - currentBounds.MinY;
                    float yBuffer = Math.Max(minBuffer, currentYRange * bufferRatio);
                    
                    if (northWall != null)
                    {
                        var northBounds = CalculateWallBounds(northWall);
                        float proposedMaxY = northBounds.MinY + yBuffer;
                        // 只有在严重突出时才约束（增加额外的容忍度）
                        float tolerance = yBuffer * 2.0f; // 双倍缓冲区作为容忍度
                        if (currentBounds.MaxY > proposedMaxY + tolerance)
                        {
                            // 约束到更宽松的位置，不是紧贴边界
                            constraints.MaxY = Math.Min(constraints.MaxY, proposedMaxY + yBuffer * 0.5f);
                        }
                    }
                    
                    if (southWall != null)
                    {
                        var southBounds = CalculateWallBounds(southWall);
                        float proposedMinY = southBounds.MaxY - yBuffer;
                        // 只有在严重突出时才约束
                        float tolerance = yBuffer * 2.0f;
                        if (currentBounds.MinY < proposedMinY - tolerance)
                        {
                            // 约束到更宽松的位置
                            constraints.MinY = Math.Max(constraints.MinY, proposedMinY - yBuffer * 0.5f);
                        }
                    }
                    break;

                case WallDirection.North:
                case WallDirection.South:
                    // 南北墙：限制X方向的延伸，但保持更大的缓冲区
                    var eastWall = allVerticalWalls.FirstOrDefault(w => w.Direction == WallDirection.East);
                    var westWall = allVerticalWalls.FirstOrDefault(w => w.Direction == WallDirection.West);
                    
                    float currentXRange = currentBounds.MaxX - currentBounds.MinX;
                    float xBuffer = Math.Max(minBuffer, currentXRange * bufferRatio);
                    
                    if (eastWall != null)
                    {
                        var eastBounds = CalculateWallBounds(eastWall);
                        float proposedMaxX = eastBounds.MinX + xBuffer;
                        // 只有在严重突出时才约束（增加额外的容忍度）
                        float tolerance = xBuffer * 2.0f; // 双倍缓冲区作为容忍度
                        if (currentBounds.MaxX > proposedMaxX + tolerance)
                        {
                            // 约束到更宽松的位置，不是紧贴边界
                            constraints.MaxX = Math.Min(constraints.MaxX, proposedMaxX + xBuffer * 0.5f);
                        }
                    }
                    
                    if (westWall != null)
                    {
                        var westBounds = CalculateWallBounds(westWall);
                        float proposedMinX = westBounds.MaxX - xBuffer;
                        // 只有在严重突出时才约束
                        float tolerance = xBuffer * 2.0f;
                        if (currentBounds.MinX < proposedMinX - tolerance)
                        {
                            // 约束到更宽松的位置
                            constraints.MinX = Math.Max(constraints.MinX, proposedMinX - xBuffer * 0.5f);
                        }
                    }
                    break;
            }

            return constraints;
        }

        /// <summary>
        /// 检查点是否在约束范围内
        /// </summary>
        private bool IsPointWithinConstraints(Vector3 point, WallDirection direction, WallBounds constraints)
        {
            // 基本边界检查
            if (point.X < constraints.MinX || point.X > constraints.MaxX ||
                point.Y < constraints.MinY || point.Y > constraints.MaxY ||
                point.Z < constraints.MinZ || point.Z > constraints.MaxZ)
            {
                return false;
            }

            return true;
        }
    }
}

