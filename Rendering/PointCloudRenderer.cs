using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using LoadPCDtest.Core;

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
        
        // 显示控制
        public bool ShowMesh { get; set; } = false;
        public bool ShowPoints { get; set; } = true;
        public bool ShowTrackball { get; set; } = false;
        public bool ShowBoundingBox { get; set; } = false;
        public bool ShowGeneratedFacades { get; set; } = false; // 控制生成立面的显示
        public bool ShowDetectedWalls { get; set; } = false;    // 控制检测墙面点显示
        
        // 当前网格
        public SurfaceReconstruction.Mesh CurrentMesh { get; set; } = null;
        public List<Vector3> DetectedWallPoints { get; set; } = new List<Vector3>();
        
        // 颜色管理器访问器
        public ColorManager ColorManager => colorManager;

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

            // 绘制原始点云
            if (ShowPoints)
            {
                GL.PointSize(PointSize);
                colorManager.RenderColoredPoints(data);
            }

            // 绘制生成的立面点云
            if (ShowGeneratedFacades)
            {
                GL.PointSize(PointSize);
                RenderGeneratedFacades();
            }

            // 绘制检测的墙面点云
            if (ShowDetectedWalls && DetectedWallPoints != null && DetectedWallPoints.Count > 0)
            {
                GL.PointSize(PointSize);
                RenderDetectedWalls();
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
        /// 切换生成立面显示
        /// </summary>
        public void ToggleGeneratedFacades()
        {
            ShowGeneratedFacades = !ShowGeneratedFacades;
            System.Diagnostics.Debug.WriteLine($"生成立面显示: {(ShowGeneratedFacades ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换检测墙面显示
        /// </summary>
        public void ToggleDetectedWalls()
        {
            ShowDetectedWalls = !ShowDetectedWalls;
            System.Diagnostics.Debug.WriteLine($"检测墙面显示: {(ShowDetectedWalls ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 渲染生成的立面点云
        /// </summary>
        private void RenderGeneratedFacades()
        {
            GL.Begin(PrimitiveType.Points);
            
            // 渲染各个立面
            foreach (var facadeType in System.Enum.GetValues(typeof(Analysis.FacadeManager.FacadeType)))
            {
                var facade = (Analysis.FacadeManager.FacadeType)facadeType;
                
                if (colorManager.FacadeManager.IsFacadeVisible(facade))
                {
                    var facadePoints = colorManager.FacadeManager.GetFacadePoints(facade);
                    var facadeColor = colorManager.FacadeManager.GetFacadeColor(facade);
                    
                    GL.Color3(facadeColor.X, facadeColor.Y, facadeColor.Z);
                    
                    foreach (var point in facadePoints)
                    {
                        GL.Vertex3(point.X, point.Y, point.Z);
                    }
                }
            }
            
            GL.End();
        }

        /// <summary>
        /// 渲染检测到的墙面点
        /// </summary>
        private void RenderDetectedWalls()
        {
            GL.Begin(PrimitiveType.Points);
            // 采用青色显示
            GL.Color3(0.0f, 1.0f, 1.0f);
            foreach (var p in DetectedWallPoints)
            {
                GL.Vertex3(p.X, p.Y, p.Z);
            }
            GL.End();
        }

        /// <summary>
        /// 设置轨迹球显示状态（用于交互时自动显示）
        /// </summary>
        public void SetTrackballVisibility(bool visible)
        {
            ShowTrackball = visible;
        }
    }
}
