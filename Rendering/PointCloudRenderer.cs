using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using LoadPCDtest.Core;
using LoadPCDtest.Analysis;

namespace LoadPCDtest.Rendering
{
    /// <summary>
    /// 点云渲染器
    /// </summary>
    public class PointCloudRenderer
    {
        private float _pointSize = 2.0f;
        private ColorManager colorManager;
        private MeshRenderer meshRenderer;
        private TrackballRenderer trackballRenderer;
        private BoundingBoxRenderer boundingBoxRenderer;
        private WallRenderer wallRenderer;
        private List<Vector3> enclosureWallPoints = new List<Vector3>();
        public IReadOnlyList<Vector3> GetEnclosureWallPoints() => enclosureWallPoints;
        
        // 显示控制
        public bool ShowMesh { get; set; } = false;
        public bool ShowPoints { get; set; } = true;
        public bool ShowTrackball { get; set; } = false;
        public bool ShowBoundingBox { get; set; } = false;
        public bool ShowWalls { get; set; } = false;
        public bool ShowWallBoundingBoxes { get; set; } = false;
        public bool ShowWallFourSidedBoxes { get; set; } = false;
        public bool ShowEnclosureWalls { get; set; } = false;
        public float EnclosureWallPointSize { get; set; } = 2.5f;
        
        // 当前网格
        public SurfaceReconstruction.Mesh CurrentMesh { get; set; } = null;
        
        // 墙面分析数据
        public List<WallSeparationAnalyzer.Wall> CurrentWalls { get; set; } = null;

        /// <summary>
        /// 点的大小
        /// </summary>
        public float PointSize
        {
            get => _pointSize;
            set
            {
                _pointSize = Math.Max(0.1f, Math.Min(20f, value));
                GL.PointSize(_pointSize);
            }
        }

        /// <summary>
        /// 初始化渲染器
        /// </summary>
        public void Initialize()
        {
            // 初始化所有渲染器
            colorManager = new ColorManager();
            meshRenderer = new MeshRenderer();
            trackballRenderer = new TrackballRenderer();
            boundingBoxRenderer = new BoundingBoxRenderer();
            wallRenderer = new WallRenderer();
            
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.DepthTest);
            GL.PointSize(PointSize);
            
            // 启用点平滑
            GL.Enable(EnableCap.PointSmooth);
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);
            
            // 启用线平滑
            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            
            // 启用抗锯齿
            GL.Enable(EnableCap.Multisample);
        }

        /// <summary>
        /// 设置投影矩阵
        /// </summary>
        public void SetupProjection(int width, int height)
        {
            // 设置视口
            GL.Viewport(0, 0, width, height);
            
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            float aspect = Math.Max(1, width) / (float)Math.Max(1, height);
            
            var proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.01f, 2000f);
            GL.LoadMatrix(ref proj);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }

        /// <summary>
        /// 渲染点云和所有组件
        /// </summary>
        public void RenderPointCloud(PointCloudData data, CameraController camera, int viewportWidth, int viewportHeight)
        {
            if (data.Points == null || data.Points.Count == 0) return;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // 设置相机变换
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            // 相机位置
            GL.Translate(camera.Pan.X, camera.Pan.Y, -camera.Distance);

            // 对象变换
            GL.Translate(-data.Center.X, -data.Center.Y, -data.Center.Z);
            GL.Rotate(camera.PointCloudPitch, 1, 0, 0);
            GL.Rotate(camera.PointCloudYaw, 0, 1, 0);
            GL.Scale(data.ObjectScale * camera.GlobalScale, data.ObjectScale * camera.GlobalScale, data.ObjectScale * camera.GlobalScale);

            // 绘制坐标轴
            DrawAxes();

            // 绘制边界框
            if (ShowBoundingBox)
            {
                boundingBoxRenderer.RenderBoundingBox(data.Points);
            }

            // 绘制轨迹球（在点云中心位置绘制，跟随点云位置和旋转，但不缩放）
            if (ShowTrackball)
            {
                // 保存当前缩放前的矩阵状态
                GL.PushMatrix();
                trackballRenderer.RenderTrackball(data, camera, viewportWidth, viewportHeight);
                GL.PopMatrix();
            }

            // 绘制3D网格
            if (ShowMesh && CurrentMesh != null && CurrentMesh.Triangles.Count > 0)
            {
                meshRenderer.RenderMesh(CurrentMesh);
            }

            // 绘制墙面
            if (ShowWalls && CurrentWalls != null && CurrentWalls.Count > 0)
            {
                wallRenderer.RenderWalls(CurrentWalls);
            }

            // 绘制墙面边界框
            if (ShowWallBoundingBoxes && CurrentWalls != null && CurrentWalls.Count > 0)
            {
                wallRenderer.RenderWallBoundingBoxes(CurrentWalls);
            }

            // 绘制墙体四侧包围
            if (ShowWallFourSidedBoxes && CurrentWalls != null && CurrentWalls.Count > 0)
            {
                wallRenderer.RenderWallFourSidedBoxes(CurrentWalls, 0.05f, 0.25f);
            }

            // 绘制点云
            if (ShowPoints)
            {
                GL.PointSize(PointSize);
                colorManager.RenderColoredPoints(data);
            }

            // 绘制生成的建筑包裹外立面点云
            if (ShowEnclosureWalls && enclosureWallPoints.Count > 0)
            {
                GL.PointSize(EnclosureWallPointSize);
                GL.Begin(PrimitiveType.Points);
                GL.Color3(1.0f, 1.0f, 0.0f); // 黄色高亮，区别于原始墙面
                foreach (var p in enclosureWallPoints)
                {
                    GL.Vertex3(p);
                }
                GL.End();
                GL.PointSize(PointSize);
            }
        }



        /// <summary>
        /// 绘制坐标轴
        /// </summary>
        private void DrawAxes()
        {
            float len = 3.0f;
            GL.LineWidth(2f);
            GL.Begin(PrimitiveType.Lines);

            // X 红
            GL.Color3(1.0f, 0.0f, 0.0f);
            GL.Vertex3(0, 0, 0); GL.Vertex3(len, 0, 0);

            // Y 绿
            GL.Color3(0.0f, 1.0f, 0.0f);
            GL.Vertex3(0, 0, 0); GL.Vertex3(0, len, 0);

            // Z 蓝
            GL.Color3(0.0f, 0.6f, 1.0f);
            GL.Vertex3(0, 0, 0); GL.Vertex3(0, 0, len);

            GL.End();
        }

        /// <summary>
        /// 切换颜色模式
        /// </summary>
        public void CycleColorMode(bool hasColors)
        {
            colorManager?.CycleColorMode(hasColors);
        }

        /// <summary>
        /// 获取当前颜色模式名称
        /// </summary>
        public string GetColorModeName()
        {
            return colorManager?.GetColorModeName() ?? "未知";
        }

        /// <summary>
        /// 生成3D网格
        /// </summary>
        public void GenerateMesh(List<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("没有点云数据，无法生成3D网格");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("开始生成3D网格...");
                
                // 使用简单网格重建
                CurrentMesh = SurfaceReconstruction.CreateSimpleGridMesh(points, 0.1f);
                
                if (CurrentMesh.Triangles.Count > 0)
                {
                    ShowMesh = true;
                    System.Diagnostics.Debug.WriteLine($"3D网格生成成功: {CurrentMesh.Triangles.Count} 个三角形");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("3D网格生成失败: 没有生成三角形");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"3D网格生成异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换网格显示
        /// </summary>
        public void ToggleMesh()
        {
            ShowMesh = !ShowMesh;
            System.Diagnostics.Debug.WriteLine($"3D网格显示: {(ShowMesh ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换点云显示
        /// </summary>
        public void TogglePoints()
        {
            ShowPoints = !ShowPoints;
            System.Diagnostics.Debug.WriteLine($"点云显示: {(ShowPoints ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换轨迹球显示
        /// </summary>
        public void ToggleTrackball()
        {
            ShowTrackball = !ShowTrackball;
            System.Diagnostics.Debug.WriteLine($"轨迹球显示: {(ShowTrackball ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换边界框显示
        /// </summary>
        public void ToggleBoundingBox()
        {
            ShowBoundingBox = !ShowBoundingBox;
            System.Diagnostics.Debug.WriteLine($"边界框显示: {(ShowBoundingBox ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 设置轨迹球显示状态（用于交互时自动显示）
        /// </summary>
        public void SetTrackballVisibility(bool visible)
        {
            ShowTrackball = visible;
        }

        /// <summary>
        /// 直接从原始点云生成贴合外立面（不依赖墙面分离）
        /// </summary>
        public void GenerateEnclosureWallsFromWalls(List<WallSeparationAnalyzer.Wall> walls, float step = 0.25f, float expand = 0.2f)
        {
            enclosureWallPoints.Clear();
            
            // 优先使用原始点云数据（如果可用）
            if (originalPointCloudData != null && originalPointCloudData.Points != null && originalPointCloudData.Points.Count > 0)
            {
                GenerateEnclosureWallsFromOriginalPoints(originalPointCloudData.Points, step, expand);
            }
            else if (walls != null && walls.Count > 0)
            {
                // 备用方案：使用墙面分离结果
                GenerateEnclosureWallsFromWallData(walls, step, expand);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("没有可用的点云数据，无法生成外立面");
            }
        }

        // 存储原始点云数据的引用
        private PointCloudData originalPointCloudData = null;

        /// <summary>
        /// 设置原始点云数据引用（用于外立面生成）
        /// </summary>
        public void SetOriginalPointCloudData(PointCloudData data)
        {
            originalPointCloudData = data;
        }

        /// <summary>
        /// 直接从原始点云生成贴合外立面（核心算法）
        /// </summary>
        private void GenerateEnclosureWallsFromOriginalPoints(List<Vector3> points, float step, float expand)
        {
            System.Diagnostics.Debug.WriteLine("使用原始点云直接生成贴合外立面(凸包包裹)...");
            
            // 过滤掉明显的地面和天花板点（只保留墙面高度范围）
            var wallHeightPoints = FilterWallHeightPoints(points);
            if (wallHeightPoints.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("没有找到墙面高度范围的点");
                return;
            }

            // 将过滤后的点投影到XY平面，计算2D凸包
            var xyPoints = wallHeightPoints.Select(p => new Vector2(p.X, p.Y)).ToList();
            var hull = ComputeConvexHull2D(xyPoints);
            if (hull == null || hull.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine("凸包顶点不足，回退到轴对齐边界法");
                var boundariesFallback = DetectWallBoundaries(wallHeightPoints);
                float minZFb = points.Min(p => p.Z);
                float maxZFb = points.Max(p => p.Z);
                GenerateBoundaryFacades(boundariesFallback, minZFb, maxZFb, step, expand);
                System.Diagnostics.Debug.WriteLine($"外立面(回退)生成完成: 总计 {enclosureWallPoints.Count:N0} 点");
                return;
            }

            // 对凸包做外扩(基于expand沿法线外推)
            var expandedHull = OffsetPolygonOutward(hull, Math.Max(0.0f, expand));

            // 计算整体Z轴范围（略微外扩，避免裁切）
            float minZ = points.Min(p => p.Z);
            float maxZ = points.Max(p => p.Z);
            float minZExpanded = minZ - Math.Max(0.0f, expand * 0.3f);
            float maxZExpanded = maxZ + Math.Max(0.0f, expand * 0.3f);

            // 沿凸包每条边生成竖向外立面点云（紧凑包裹）
            for (int i = 0; i < expandedHull.Count; i++)
            {
                var a = expandedHull[i];
                var b = expandedHull[(i + 1) % expandedHull.Count];
                GenerateFacadePointsAlongEdge(a, b, minZExpanded, maxZExpanded, step);
            }

            System.Diagnostics.Debug.WriteLine($"外立面(凸包)生成完成: 总计 {enclosureWallPoints.Count:N0} 点, 边数: {expandedHull.Count}");
        }

        /// <summary>
        /// 过滤出墙面高度范围的点（排除地面和天花板）
        /// </summary>
        private List<Vector3> FilterWallHeightPoints(List<Vector3> points)
        {
            if (points.Count == 0) return new List<Vector3>();
            
            // 计算Z轴统计信息
            var zValues = points.Select(p => p.Z).OrderBy(z => z).ToList();
            float minZ = zValues.First();
            float maxZ = zValues.Last();
            float zRange = maxZ - minZ;
            
            // 排除最底部10%和最顶部10%，专注于墙面区域
            float bottomThreshold = minZ + zRange * 0.1f;
            float topThreshold = maxZ - zRange * 0.1f;
            
            var filtered = points.Where(p => p.Z >= bottomThreshold && p.Z <= topThreshold).ToList();
            
            System.Diagnostics.Debug.WriteLine($"墙面高度过滤: {points.Count:N0} -> {filtered.Count:N0} 点 (Z范围: {bottomThreshold:F2}~{topThreshold:F2})");
            return filtered;
        }

        /// <summary>
        /// 墙面边界信息
        /// </summary>
        public class WallBoundary
        {
            public string Direction { get; set; }  // "East", "West", "North", "South"
            public float Position { get; set; }    // X或Y坐标位置
            public bool IsXFixed { get; set; }     // true: X固定(东西墙), false: Y固定(南北墙)
            public float MinRange1 { get; set; }   // 变化坐标1的最小值
            public float MaxRange1 { get; set; }   // 变化坐标1的最大值
            public int PointCount { get; set; }    // 边界点数量
        }

        /// <summary>
        /// 检测四个方向的墙面边界
        /// </summary>
        private List<WallBoundary> DetectWallBoundaries(List<Vector3> points)
        {
            var boundaries = new List<WallBoundary>();
            
            // 边界层厚度（取外围5%的点作为边界层）
            float boundaryRatio = 0.05f;
            int boundaryCount = Math.Max(10, (int)(points.Count * boundaryRatio));
            
            // 检测东西边界（X方向）
            var xSorted = points.OrderBy(p => p.X).ToList();
            var westBoundaryPoints = xSorted.Take(boundaryCount).ToList();
            var eastBoundaryPoints = xSorted.Skip(Math.Max(0, xSorted.Count - boundaryCount)).ToList();
            
            if (westBoundaryPoints.Any())
            {
                boundaries.Add(new WallBoundary
                {
                    Direction = "West",
                    Position = westBoundaryPoints.Average(p => p.X),
                    IsXFixed = true,
                    MinRange1 = westBoundaryPoints.Min(p => p.Y),
                    MaxRange1 = westBoundaryPoints.Max(p => p.Y),
                    PointCount = westBoundaryPoints.Count
                });
            }
            
            if (eastBoundaryPoints.Any())
            {
                boundaries.Add(new WallBoundary
                {
                    Direction = "East", 
                    Position = eastBoundaryPoints.Average(p => p.X),
                    IsXFixed = true,
                    MinRange1 = eastBoundaryPoints.Min(p => p.Y),
                    MaxRange1 = eastBoundaryPoints.Max(p => p.Y),
                    PointCount = eastBoundaryPoints.Count
                });
            }
            
            // 检测南北边界（Y方向）
            var ySorted = points.OrderBy(p => p.Y).ToList();
            var southBoundaryPoints = ySorted.Take(boundaryCount).ToList();
            var northBoundaryPoints = ySorted.Skip(Math.Max(0, ySorted.Count - boundaryCount)).ToList();
            
            if (southBoundaryPoints.Any())
            {
                boundaries.Add(new WallBoundary
                {
                    Direction = "South",
                    Position = southBoundaryPoints.Average(p => p.Y),
                    IsXFixed = false,
                    MinRange1 = southBoundaryPoints.Min(p => p.X),
                    MaxRange1 = southBoundaryPoints.Max(p => p.X),
                    PointCount = southBoundaryPoints.Count
                });
            }
            
            if (northBoundaryPoints.Any())
            {
                boundaries.Add(new WallBoundary
                {
                    Direction = "North",
                    Position = northBoundaryPoints.Average(p => p.Y),
                    IsXFixed = false,
                    MinRange1 = northBoundaryPoints.Min(p => p.X),
                    MaxRange1 = northBoundaryPoints.Max(p => p.X),
                    PointCount = northBoundaryPoints.Count
                });
            }
            
            foreach (var boundary in boundaries)
            {
                System.Diagnostics.Debug.WriteLine($"检测到{boundary.Direction}边界: 位置={boundary.Position:F2}, 点数={boundary.PointCount}");
            }
            
            return boundaries;
        }

        /// <summary>
        /// 为检测到的边界生成外立面
        /// </summary>
        private void GenerateBoundaryFacades(List<WallBoundary> boundaries, float minZ, float maxZ, float step, float expand)
        {
            foreach (var boundary in boundaries)
            {
                // 扩展范围以确保完整覆盖
                float min1 = boundary.MinRange1 - expand;
                float max1 = boundary.MaxRange1 + expand;
                float minZExpanded = minZ - expand * 0.3f;
                float maxZExpanded = maxZ + expand * 0.3f;
                
                // 生成该边界的外立面点云
                GenerateFacadePointCloud(boundary.Position, min1, max1, minZExpanded, maxZExpanded, step, boundary.IsXFixed, $"{boundary.Direction}边界立面");
            }
        }

        /// <summary>
        /// 备用方案：使用墙面分离数据生成外立面
        /// </summary>
        private void GenerateEnclosureWallsFromWallData(List<WallSeparationAnalyzer.Wall> walls, float step, float expand)
        {
            System.Diagnostics.Debug.WriteLine("使用墙面分离数据生成外立面...");
            
            // 收集所有垂直墙面的点
            var allWallPoints = new List<Vector3>();
            foreach (var wall in walls)
            {
                if (wall.Points != null && wall.Direction != WallSeparationAnalyzer.WallDirection.Horizontal)
                {
                    allWallPoints.AddRange(wall.Points);
                }
            }

            if (allWallPoints.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("没有找到垂直墙面点");
                return;
            }

            // 计算建筑物的整体边界
            var bounds = CalculateBuildingOutline(allWallPoints);

            // 生成贴合的外立面
            GenerateEnclosureFacades(bounds, step, expand, walls);
        }

        /// <summary>
        /// 建筑物外轮廓边界信息
        /// </summary>
        public class BuildingOutline
        {
            public float MinX { get; set; }
            public float MaxX { get; set; }
            public float MinY { get; set; }
            public float MaxY { get; set; }
            public float MinZ { get; set; }
            public float MaxZ { get; set; }
            public float CenterX => (MinX + MaxX) / 2f;
            public float CenterY => (MinY + MaxY) / 2f;
            public float Width => MaxX - MinX;
            public float Depth => MaxY - MinY;
            public float Height => MaxZ - MinZ;
        }

        /// <summary>
        /// 计算建筑物的整体外轮廓边界
        /// </summary>
        private BuildingOutline CalculateBuildingOutline(List<Vector3> allWallPoints)
        {
            var outline = new BuildingOutline
            {
                MinX = allWallPoints.Min(p => p.X),
                MaxX = allWallPoints.Max(p => p.X),
                MinY = allWallPoints.Min(p => p.Y),
                MaxY = allWallPoints.Max(p => p.Y),
                MinZ = allWallPoints.Min(p => p.Z),
                MaxZ = allWallPoints.Max(p => p.Z)
            };

            System.Diagnostics.Debug.WriteLine($"建筑外轮廓: X[{outline.MinX:F2}~{outline.MaxX:F2}] Y[{outline.MinY:F2}~{outline.MaxY:F2}] Z[{outline.MinZ:F2}~{outline.MaxZ:F2}]");
            System.Diagnostics.Debug.WriteLine($"建筑尺寸: 宽{outline.Width:F2}m × 深{outline.Depth:F2}m × 高{outline.Height:F2}m");
            
            return outline;
        }

        /// <summary>
        /// 生成四个方向的包裹性外立面点云
        /// </summary>
        private void GenerateEnclosureFacades(BuildingOutline outline, float step, float expand, List<WallSeparationAnalyzer.Wall> walls)
        {
            // 扩展边界，确保包裹效果
            float minX = outline.MinX - expand;
            float maxX = outline.MaxX + expand;
            float minY = outline.MinY - expand;
            float maxY = outline.MaxY + expand;
            float minZ = outline.MinZ - expand * 0.5f; // Z方向扩展较小
            float maxZ = outline.MaxZ + expand * 0.5f;

            // 检查每个方向是否存在对应的墙面，只为存在的方向生成外立面
            bool hasEastWall = walls.Any(w => w.Direction == WallSeparationAnalyzer.WallDirection.East);
            bool hasWestWall = walls.Any(w => w.Direction == WallSeparationAnalyzer.WallDirection.West);
            bool hasNorthWall = walls.Any(w => w.Direction == WallSeparationAnalyzer.WallDirection.North);
            bool hasSouthWall = walls.Any(w => w.Direction == WallSeparationAnalyzer.WallDirection.South);

            // 东立面 (X = maxX)
            if (hasEastWall)
            {
                GenerateFacadePointCloud(maxX, minY, maxY, minZ, maxZ, step, true, "东立面");
            }

            // 西立面 (X = minX)
            if (hasWestWall)
            {
                GenerateFacadePointCloud(minX, minY, maxY, minZ, maxZ, step, true, "西立面");
            }

            // 北立面 (Y = maxY)
            if (hasNorthWall)
            {
                GenerateFacadePointCloud(maxY, minX, maxX, minZ, maxZ, step, false, "北立面");
            }

            // 南立面 (Y = minY)
            if (hasSouthWall)
            {
                GenerateFacadePointCloud(minY, minX, maxX, minZ, maxZ, step, false, "南立面");
            }
        }

        /// <summary>
        /// 生成单个立面的点云
        /// </summary>
        /// <param name="fixedCoord">固定坐标值</param>
        /// <param name="min1">第一变化坐标范围最小值</param>
        /// <param name="max1">第一变化坐标范围最大值</param>
        /// <param name="min2">第二变化坐标范围最小值</param>
        /// <param name="max2">第二变化坐标范围最大值</param>
        /// <param name="step">采样步长</param>
        /// <param name="isXFixed">true: X固定(YZ变化), false: Y固定(XZ变化)</param>
        /// <param name="facadeName">立面名称（用于调试）</param>
        private void GenerateFacadePointCloud(float fixedCoord, float min1, float max1, float min2, float max2, float step, bool isXFixed, string facadeName)
        {
            int pointCount = 0;
            
            for (float coord1 = min1; coord1 <= max1; coord1 += step)
            {
                for (float coord2 = min2; coord2 <= max2; coord2 += step)
                {
                    if (isXFixed)
                    {
                        // X固定，YZ变化（东西立面）
                        enclosureWallPoints.Add(new Vector3(fixedCoord, coord1, coord2));
                    }
                    else
                    {
                        // Y固定，XZ变化（南北立面）
                        enclosureWallPoints.Add(new Vector3(coord1, fixedCoord, coord2));
                    }
                    pointCount++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"生成{facadeName}: {pointCount:N0}个点 (固定坐标:{fixedCoord:F2})");
        }

        // 计算二维凸包（单调链算法）
        private List<Vector2> ComputeConvexHull2D(List<Vector2> points)
        {
            if (points == null || points.Count < 3)
                return points ?? new List<Vector2>();

            var pts = points.Distinct(new Vector2Comparer()).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            if (pts.Count < 3) return pts;

            List<Vector2> lower = new List<Vector2>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            List<Vector2> upper = new List<Vector2>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = b - a;
            var ac = c - a;
            return ab.X * ac.Y - ab.Y * ac.X;
        }

        private class Vector2Comparer : IEqualityComparer<Vector2>
        {
            public bool Equals(Vector2 a, Vector2 b)
            {
                return Math.Abs(a.X - b.X) < 1e-6f && Math.Abs(a.Y - b.Y) < 1e-6f;
            }

            public int GetHashCode(Vector2 v)
            {
                unchecked
                {
                    int hx = v.X.GetHashCode();
                    int hy = v.Y.GetHashCode();
                    return (hx * 397) ^ hy;
                }
            }
        }

        private Vector2 NormalizeVec(Vector2 v)
        {
            float len = v.Length;
            if (len < 1e-6f) return Vector2.Zero;
            return v / len;
        }

        // 多边形外扩（简单法线外推 + miter连接）
        private List<Vector2> OffsetPolygonOutward(List<Vector2> polygon, float distance)
        {
            if (polygon == null || polygon.Count < 3 || distance <= 0)
                return new List<Vector2>(polygon ?? Enumerable.Empty<Vector2>());

            int n = polygon.Count;
            var result = new List<Vector2>(n);

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = polygon[(i - 1 + n) % n];
                Vector2 curr = polygon[i];
                Vector2 next = polygon[(i + 1) % n];

                Vector2 dir1 = NormalizeVec(curr - prev);
                Vector2 dir2 = NormalizeVec(next - curr);

                // 外法线（左法线）
                Vector2 n1 = new Vector2(-dir1.Y, dir1.X);
                Vector2 n2 = new Vector2(-dir2.Y, dir2.X);

                // 计算miter方向
                Vector2 miter = n1 + n2;
                float miterLen = miter.Length;
                if (miterLen < 1e-6f)
                {
                    // 近似直角或退化，使用简单平均外推
                    result.Add(curr + n1 * distance);
                }
                else
                {
                    miter = miter / miterLen;
                    // 计算缩放，避免在锐角处过长
                    float cosHalfAngle = Vector2.Dot(miter, n1);
                    float scale = distance / Math.Max(0.2f, cosHalfAngle);
                    // 限制最大miter长度
                    scale = Math.Min(scale, distance * 3.0f);
                    result.Add(curr + miter * scale);
                }
            }

            return result;
        }

        // 沿凸包边生成竖向外立面点云（在边上按step采样，沿Z挤出）
        private void GenerateFacadePointsAlongEdge(Vector2 a, Vector2 b, float minZ, float maxZ, float step)
        {
            float edgeLen = (b - a).Length;
            if (edgeLen < 1e-6f) return;

            int numAlong = Math.Max(1, (int)Math.Ceiling(edgeLen / step));
            for (int i = 0; i <= numAlong; i++)
            {
                float t = i / (float)numAlong;
                Vector2 p = a + (b - a) * t;
                for (float z = minZ; z <= maxZ; z += step)
                {
                    enclosureWallPoints.Add(new Vector3(p.X, p.Y, z));
                }
            }
        }



        /// <summary>
        /// 清空生成的建筑包裹外立面点云
        /// </summary>
        public void ClearEnclosureWalls()
        {
            enclosureWallPoints.Clear();
            System.Diagnostics.Debug.WriteLine("已清空建筑包裹外立面点云");
        }
    }
}
