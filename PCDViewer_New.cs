using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using LoadPCDtest.Core;
using LoadPCDtest.Rendering;
using LoadPCDtest.IO;
using LoadPCDtest.Filtering;
using LoadPCDtest.Analysis;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoadPCDtest
{
    /// <summary>
    /// 简化的点云查看器主窗口
    /// </summary>
    public partial class PCDViewer_New : Form
    {
        // 核心组件
        private GLControl gl;
        private PointCloudData pointCloudData;
        private CameraController camera;
        private PointCloudRenderer renderer;
        private CoordinateMapping currentMapping = CoordinateMapping.Original;
        
        // 墙面分离相关组件
        private WallSeparationAnalyzer wallAnalyzer;
        private WallRenderer wallRenderer;
        private List<WallSeparationAnalyzer.Wall> detectedWalls = null;
        private bool showWalls = false;
        private bool showOriginalPointCloud = true;
        
        // 交互状态
        private Point lastMouse;
        // 最近一次墙面检测上下文
        private List<Analysis.WallRefinement.WallFace> lastDetectedFaces = null;
        private float lastZMin = 0f;
        private float lastZMax = 0f;
        private List<Vector2> lastPolygon = null;
        private List<Vector3> lastFilteredPoints = null;
        private float currentOutwardBias = 0f;
        
        public PCDViewer_New(string filePath = null)
        {
            InitializeManagers(); // 先初始化管理器
            InitializeComponents(); // 再创建UI组件（包括菜单）
            
            if (!string.IsNullOrEmpty(filePath))
            {
                Shown += (s, e) => LoadPointCloudFile(filePath);
            }
            else
            {
                Shown += (s, e) => ShowFileDialog();
            }
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeComponents()
        {
            Text = "点云查看器 - 简化版";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            // 创建菜单
            CreateMenuBar();

            // 创建OpenGL控件
            gl = new GLControl();
            gl.Dock = DockStyle.Fill;
            Controls.Add(gl);

            // 绑定事件
            gl.Load += Gl_Load;
            gl.Paint += Gl_Paint;
            gl.Resize += Gl_Resize;
            gl.MouseDown += Gl_MouseDown;
            gl.MouseUp += Gl_MouseUp;
            gl.MouseMove += Gl_MouseMove;
            gl.MouseWheel += Gl_MouseWheel;
            
            // 键盘事件
            this.KeyPreview = true;
            this.KeyDown += OnKeyDown;
        }

        /// <summary>
        /// 初始化管理器
        /// </summary>
        private void InitializeManagers()
        {
            pointCloudData = new PointCloudData();
            camera = new CameraController();
            renderer = new PointCloudRenderer();
            wallAnalyzer = new WallSeparationAnalyzer();
            wallRenderer = new WallRenderer();
        }

        /// <summary>
        /// 创建菜单栏
        /// </summary>
        private void CreateMenuBar()
        {
            var menuStrip = new MenuStrip();
            
            // 文件菜单
            var fileMenu = new ToolStripMenuItem("文件(&F)");
            
            var openMenuItem = new ToolStripMenuItem("打开(&O)");
            openMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openMenuItem.Click += (s, e) => ShowFileDialog();
            
            var reloadMenuItem = new ToolStripMenuItem("重新加载(&R)");
            reloadMenuItem.ShortcutKeys = Keys.F5;
            reloadMenuItem.Click += (s, e) => ReloadCurrentFile();
            
            fileMenu.DropDownItems.Add(openMenuItem);
            fileMenu.DropDownItems.Add(reloadMenuItem);
            
            // 工具菜单
            var toolsMenu = new ToolStripMenuItem("工具(&T)");
            
            var rangeSelectionMenuItem = new ToolStripMenuItem("选择范围(&S)");
            rangeSelectionMenuItem.ShortcutKeys = Keys.Control | Keys.R;
            rangeSelectionMenuItem.Click += (s, e) => StartRangeSelection();
            
            var wallSeparationMenuItem = new ToolStripMenuItem("墙面分离(&W)");
            wallSeparationMenuItem.ShortcutKeys = Keys.Control | Keys.W;
            wallSeparationMenuItem.Click += (s, e) => PerformWallSeparation();
            
            var wallSettingsMenuItem = new ToolStripMenuItem("墙面检测参数(&P)");
            wallSettingsMenuItem.Click += (s, e) => ShowWallDetectionSettings();
            
            toolsMenu.DropDownItems.Add(rangeSelectionMenuItem);
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            toolsMenu.DropDownItems.Add(wallSeparationMenuItem);
            toolsMenu.DropDownItems.Add(wallSettingsMenuItem);
            
            // 显示菜单
            var displayMenu = new ToolStripMenuItem("显示(&D)");
            
            var pointSizeSubMenu = new ToolStripMenuItem("点大小(&P)");
            
            var increaseSizeMenuItem = new ToolStripMenuItem("增大 (+)");
            increaseSizeMenuItem.ShortcutKeys = Keys.F10;
            increaseSizeMenuItem.Click += (s, e) => IncreasePointSize();
            
            var decreaseSizeMenuItem = new ToolStripMenuItem("减小 (-)");
            decreaseSizeMenuItem.ShortcutKeys = Keys.F11;
            decreaseSizeMenuItem.Click += (s, e) => DecreasePointSize();
            
            var resetSizeMenuItem = new ToolStripMenuItem("重置 (0)");
            resetSizeMenuItem.ShortcutKeys = Keys.F12;
            resetSizeMenuItem.Click += (s, e) => ResetPointSize();
            
            pointSizeSubMenu.DropDownItems.Add(increaseSizeMenuItem);
            pointSizeSubMenu.DropDownItems.Add(decreaseSizeMenuItem);
            pointSizeSubMenu.DropDownItems.Add(resetSizeMenuItem);
            
            // 墙面显示子菜单
            var wallDisplaySubMenu = new ToolStripMenuItem("墙面显示(&W)");
            
            var showWallsMenuItem = new ToolStripMenuItem("显示墙面(&S)");
            showWallsMenuItem.CheckOnClick = true;
            showWallsMenuItem.Checked = showWalls;
            showWallsMenuItem.Click += (s, e) => ToggleWallsVisibility();
            
            var showOriginalMenuItem = new ToolStripMenuItem("显示原始点云(&O)");
            showOriginalMenuItem.CheckOnClick = true;
            showOriginalMenuItem.Checked = showOriginalPointCloud;
            showOriginalMenuItem.Click += (s, e) => ToggleOriginalPointCloudVisibility();
            
            var showBoundingBoxesMenuItem = new ToolStripMenuItem("显示墙面边界框(&B)");
            showBoundingBoxesMenuItem.Click += (s, e) => ToggleBoundingBoxes();
            
            wallDisplaySubMenu.DropDownItems.Add(showWallsMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(showOriginalMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(showBoundingBoxesMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(new ToolStripSeparator());
            
            // 各个墙面的显示控制
            var northWallMenuItem = new ToolStripMenuItem("北墙(红色)(&N)");
            northWallMenuItem.CheckOnClick = true;
            northWallMenuItem.Checked = true; // 默认值
            northWallMenuItem.Click += (s, e) => { 
                if (wallRenderer != null) { 
                    wallRenderer.ShowNorthWall = !wallRenderer.ShowNorthWall; 
                    gl.Invalidate(); 
                } 
            };
            
            var southWallMenuItem = new ToolStripMenuItem("南墙(绿色)(&S)");
            southWallMenuItem.CheckOnClick = true;
            southWallMenuItem.Checked = true; // 默认值
            southWallMenuItem.Click += (s, e) => { 
                if (wallRenderer != null) { 
                    wallRenderer.ShowSouthWall = !wallRenderer.ShowSouthWall; 
                    gl.Invalidate(); 
                } 
            };
            
            var eastWallMenuItem = new ToolStripMenuItem("东墙(蓝色)(&E)");
            eastWallMenuItem.CheckOnClick = true;
            eastWallMenuItem.Checked = true; // 默认值
            eastWallMenuItem.Click += (s, e) => { 
                if (wallRenderer != null) { 
                    wallRenderer.ShowEastWall = !wallRenderer.ShowEastWall; 
                    gl.Invalidate(); 
                } 
            };
            
            var westWallMenuItem = new ToolStripMenuItem("西墙(黄色)(&W)");
            westWallMenuItem.CheckOnClick = true;
            westWallMenuItem.Checked = true; // 默认值
            westWallMenuItem.Click += (s, e) => { 
                if (wallRenderer != null) { 
                    wallRenderer.ShowWestWall = !wallRenderer.ShowWestWall; 
                    gl.Invalidate(); 
                } 
            };
            
            var horizontalMenuItem = new ToolStripMenuItem("水平面(&H)");
            horizontalMenuItem.CheckOnClick = true;
            horizontalMenuItem.Checked = false; // 默认值
            horizontalMenuItem.Click += (s, e) => { 
                if (wallRenderer != null) { 
                    wallRenderer.ShowHorizontalSurfaces = !wallRenderer.ShowHorizontalSurfaces; 
                    gl.Invalidate(); 
                } 
            };
            
            wallDisplaySubMenu.DropDownItems.Add(northWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(southWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(eastWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(westWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(horizontalMenuItem);
            
            displayMenu.DropDownItems.Add(pointSizeSubMenu);
            displayMenu.DropDownItems.Add(new ToolStripSeparator());
            displayMenu.DropDownItems.Add(wallDisplaySubMenu);
            
            // 导出菜单
            var exportMenu = new ToolStripMenuItem("导出(&E)");
            
            var exportWallsItem = new ToolStripMenuItem("导出检测墙面为PLY");
            exportWallsItem.ShortcutKeys = Keys.Control | Keys.E;
            exportWallsItem.Click += (s, e) => ExportDetectedWalls();
            
            var exportWaypointsItem = new ToolStripMenuItem("导出航点文件(&W)");
            exportWaypointsItem.ToolTipText = "按每个检测墙面分别导出，Z由上到下，沿墙蛇形排序";
            exportWaypointsItem.Click += (s, e) => ExportDetectedFacesWaypoints();
            
            var exportSeparatedWallsItem = new ToolStripMenuItem("导出分离墙面(&S)");
            exportSeparatedWallsItem.Click += (s, e) => ExportSeparatedWalls();
            
            var exportWallReportItem = new ToolStripMenuItem("导出墙面分析报告(&R)");
            exportWallReportItem.Click += (s, e) => ExportWallAnalysisReport();
            
            exportMenu.DropDownItems.Add(exportWallsItem);
            exportMenu.DropDownItems.Add(new ToolStripSeparator());
            exportMenu.DropDownItems.Add(exportWaypointsItem);
            exportMenu.DropDownItems.Add(exportSeparatedWallsItem);
            exportMenu.DropDownItems.Add(exportWallReportItem);
            
            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(toolsMenu);
            menuStrip.Items.Add(displayMenu);
            menuStrip.Items.Add(exportMenu);
            
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        /// <summary>
        /// OpenGL初始化
        /// </summary>
        private void Gl_Load(object sender, EventArgs e)
        {
            if (!gl.Context.IsCurrent) gl.MakeCurrent();
            
            // 输出OpenGL基本信息
            System.Diagnostics.Debug.WriteLine("=== OpenGL 初始化 ===");
            System.Diagnostics.Debug.WriteLine($"OpenGL版本: {GL.GetString(StringName.Version)}");
            System.Diagnostics.Debug.WriteLine($"渲染器: {GL.GetString(StringName.Renderer)}");
            System.Diagnostics.Debug.WriteLine($"厂商: {GL.GetString(StringName.Vendor)}");
            System.Diagnostics.Debug.WriteLine($"OpenGL上下文当前状态: {gl.Context.IsCurrent}");
            System.Diagnostics.Debug.WriteLine($"GL控件大小: {gl.Width}x{gl.Height}");
            
            renderer.Initialize();
            renderer.SetupProjection(gl.Width, gl.Height);
            

            
            System.Diagnostics.Debug.WriteLine("OpenGL初始化完成");
            System.Diagnostics.Debug.WriteLine("==================");
        }

        /// <summary>
        /// 窗口大小改变
        /// </summary>
        private void Gl_Resize(object sender, EventArgs e)
        {
            if (!gl.Context.IsCurrent) gl.MakeCurrent();
            GL.Viewport(0, 0, gl.Width, gl.Height);
            renderer.SetupProjection(gl.Width, gl.Height);
            gl.Invalidate();
        }
        static bool firstRender = true;
        /// <summary>
        /// 渲染
        /// </summary>
        private void Gl_Paint(object sender, PaintEventArgs e)
        {
            if (!gl.Context.IsCurrent) gl.MakeCurrent();

            // 只在第一次渲染时输出调试信息
            if (firstRender)
            {
                System.Diagnostics.Debug.WriteLine("=== 开始渲染 ===");
                System.Diagnostics.Debug.WriteLine($"OpenGL上下文: {gl.Context.IsCurrent}");
                System.Diagnostics.Debug.WriteLine($"GL控件大小: {gl.Width}x{gl.Height}");
                
                if (pointCloudData.Points != null && pointCloudData.Points.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"首次渲染点云: {pointCloudData.Points.Count} 个点");
                    System.Diagnostics.Debug.WriteLine($"显示设置: ShowPoints={renderer.ShowPoints}, 点大小={renderer.PointSize}");
                    System.Diagnostics.Debug.WriteLine($"相机: 距离={camera.Distance:F2}, 缩放={camera.GlobalScale:F2}");
                    System.Diagnostics.Debug.WriteLine($"对象缩放: {pointCloudData.ObjectScale:F6}");
                    System.Diagnostics.Debug.WriteLine($"点云中心: ({pointCloudData.Center.X:F2}, {pointCloudData.Center.Y:F2}, {pointCloudData.Center.Z:F2})");
                    
                    // 输出前几个点的坐标作为参考
                    for (int i = 0; i < Math.Min(3, pointCloudData.Points.Count); i++)
                    {
                        var p = pointCloudData.Points[i];
                        System.Diagnostics.Debug.WriteLine($"点[{i}]: ({p.X:F2}, {p.Y:F2}, {p.Z:F2})");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("没有点云数据可渲染");
                }
                firstRender = false;
            }

            // 执行渲染
            try
            {
                // 渲染原始点云（如果启用）
                if (showOriginalPointCloud)
                {
                    renderer.RenderPointCloud(pointCloudData, camera, gl.Width, gl.Height);
                }
                else
                {
                    // 如果不显示原始点云，我们需要手动设置OpenGL状态
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    
                    // 设置基本的3D渲染状态
                    GL.Enable(EnableCap.DepthTest);
                    GL.DepthFunc(DepthFunction.Less);
                    
                    // 设置投影矩阵
                    GL.MatrixMode(MatrixMode.Projection);
                    GL.LoadIdentity();
                    float aspect = (float)gl.Width / gl.Height;
                    Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                        MathHelper.DegreesToRadians(45), aspect, 0.1f, 1000.0f);
                    GL.LoadMatrix(ref projection);
                    
                    // 设置模型视图矩阵
                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.LoadIdentity();
                    GL.Translate(camera.Pan.X, camera.Pan.Y, -camera.Distance);
                    GL.Rotate(camera.PointCloudPitch, 1, 0, 0);
                    GL.Rotate(camera.PointCloudYaw, 0, 1, 0);
                    float scale = camera.GlobalScale * pointCloudData.ObjectScale;
                    GL.Scale(scale, scale, scale);
                    GL.Translate(-pointCloudData.Center.X, -pointCloudData.Center.Y, -pointCloudData.Center.Z);
                }
                
                // 渲染墙面
                if (showWalls && detectedWalls != null && detectedWalls.Count > 0)
                {
                    wallRenderer.RenderWalls(detectedWalls);
                }
                
                gl.SwapBuffers();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"渲染过程出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 鼠标按下
        /// </summary>
        private void Gl_MouseDown(object sender, MouseEventArgs e)
        {
            lastMouse = e.Location;
            
            if (e.Button == MouseButtons.Left) 
            {
                camera.IsRotating = true;
                renderer.SetTrackballVisibility(true); // 开始拖拽时显示轨迹球
                gl.Invalidate();
            }
            if (e.Button == MouseButtons.Right) 
            {
                camera.IsPanning = true;
            }
        }

        /// <summary>
        /// 鼠标抬起
        /// </summary>
        private void Gl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) 
            {
                camera.IsRotating = false;
                renderer.SetTrackballVisibility(false); // 停止拖拽时隐藏轨迹球
                gl.Invalidate();
            }
            if (e.Button == MouseButtons.Right) 
            {
                camera.IsPanning = false;
            }
            if (e.Button == MouseButtons.Middle)
            {
                // 中键：显示点大小调整菜单
                ShowPointSizeMenu(e.Location);
            }
        }

        /// <summary>
        /// 鼠标移动
        /// </summary>
        private void Gl_MouseMove(object sender, MouseEventArgs e)
        {
            var dx = e.X - lastMouse.X;
            var dy = e.Y - lastMouse.Y;

            if (camera.IsRotating)
            {
                camera.HandleRotation(dx, dy);
                gl.Invalidate();
            }
            else if (camera.IsPanning)
            {
                camera.HandlePanning(dx, dy, camera.Distance, pointCloudData.ObjectScale);
                gl.Invalidate();
            }

            lastMouse = e.Location;
        }

        /// <summary>
        /// 鼠标滚轮
        /// </summary>
        private void Gl_MouseWheel(object sender, MouseEventArgs e)
        {
            // 检查组合键
            if (Control.ModifierKeys == Keys.Control)
            {
                // Ctrl + 滚轮：调整点大小
                if (e.Delta > 0)
                {
                    IncreasePointSize();
                }
                else
                {
                    DecreasePointSize();
                }
            }
            else
            {
                // 普通滚轮：缩放
                camera.HandleZoom(e.Delta);
                UpdateTitle();
                gl.Invalidate();
            }
        }

        /// <summary>
        /// 键盘事件
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.R:
                    ResetView();
                    break;
                case Keys.Home:
                case Keys.Space:
                    // Home键或空格键：重置到中心并确保可见
                    CenterAndFitPointCloud();
                    break;
                case Keys.F:
                    // F键：强制回到原点
                    ForceToOrigin();
                    break;
                case Keys.C:
                    CycleCoordinateMapping();
                    break;
                case Keys.D1:
                case Keys.D2:
                case Keys.D3:
                case Keys.D4:
                case Keys.D5:
                case Keys.D6:
                case Keys.D7:
                case Keys.D8:
                    int mappingIndex = e.KeyCode - Keys.D1;
                    if (mappingIndex < Enum.GetValues(typeof(CoordinateMapping)).Length)
                    {
                        SetCoordinateMapping((CoordinateMapping)mappingIndex);
                    }
                    break;
                case Keys.Oemplus:
                case Keys.Add:
                    AdjustPointSize(1.5f);
                    break;
                case Keys.OemMinus:
                case Keys.Subtract:
                    AdjustPointSize(0.67f);
                    break;
                case Keys.X:
                    // X键：切换颜色模式
                    renderer.CycleColorMode(pointCloudData.HasColors);
                    UpdateTitle();
                    gl.Invalidate();
                    break;
                case Keys.M:
                    // M键：切换3D网格显示
                    renderer.ToggleMesh();
                    gl.Invalidate();
                    break;
                case Keys.P:
                    // P键：切换点云显示
                    if (!renderer.ShowPoints)
                    {
                        // 要显示原始点云时，确保颜色模式不是Facade（因为Facade模式不渲染原始点云）
                        if (renderer.ColorManager.CurrentColorMode == Rendering.ColorMode.Facade)
                        {
                            renderer.ColorManager.CurrentColorMode = pointCloudData.HasColors ? Rendering.ColorMode.OriginalRGB : Rendering.ColorMode.HeightBased;
                        }
                    }
                    renderer.TogglePoints();
                    UpdateTitle();
                    gl.Invalidate();
                    break;
                case Keys.Z:
                    // G键：生成3D网格
                    renderer.GenerateMesh(pointCloudData.Points);
                    gl.Invalidate();
                    break;
                case Keys.T:
                    // T键：切换轨迹球显示
                    renderer.ToggleTrackball();
                    gl.Invalidate();
                    break;
                case Keys.B:
                    // B键：切换边界框显示
                    renderer.ToggleBoundingBox();
                    gl.Invalidate();
                    break;
                case Keys.D0:
                    // 0键：重置点大小
                    ResetPointSize();
                    break;
                case Keys.F1:
                    // F1：切换主方向正面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.MainPositive);
                    break;
                case Keys.F2:
                    // F2：切换主方向负面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.MainNegative);
                    break;
                case Keys.F3:
                    // F3：切换垂直方向正面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.PerpPositive);
                    break;
                case Keys.F4:
                    // F4：切换垂直方向负面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.PerpNegative);
                    break;
                
                case Keys.G:
                    // G：切换生成立面显示/隐藏
                    ToggleGeneratedFacades();
                    break;
                case Keys.H:
                    // H：切换检测墙面显示/隐藏
                    renderer.ToggleDetectedWalls();
                    UpdateTitle();
                    gl.Invalidate();
                    break;
                case Keys.OemOpenBrackets: // [
                    AdjustOutwardBias(-0.05f);
                    break;
                case Keys.OemCloseBrackets: // ]
                    AdjustOutwardBias(+0.05f);
                    break;
            }
        }

        /// <summary>
        /// 显示文件选择对话框
        /// </summary>
        private void ShowFileDialog()
        {
            using (var dlg = new OpenFileDialog()
            {
                Title = "选择点云文件",
                Filter = "所有支持的格式 (*.ply;*.txt)|*.ply;*.txt|PLY文件 (*.ply)|*.ply|CloudCompare ASCII (*.txt)|*.txt|所有文件 (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadPointCloudFile(dlg.FileName);
                }
            }
        }

        /// <summary>
        /// 加载点云文件
        /// </summary>
        private void LoadPointCloudFile(string path)
        {
            if (PointCloudLoader.LoadPointCloud(path, pointCloudData))
            {
                if (pointCloudData.OriginalPoints.Count == 0)
                {
                    MessageBox.Show("点云为空或加载失败。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 应用坐标映射
                CoordinateMapper.ApplyMapping(pointCloudData, currentMapping);
                
                // 使用原版本的简单方式：保持默认值，不自动计算中心
                pointCloudData.Center = new OpenTK.Vector3(0, 0, 0);    // 原版本默认
                pointCloudData.ObjectScale = 1.0f;                       // 原版本默认
                camera.Distance = 10f;                                   // 原版本默认
                
                // 重置相机到默认状态
                camera.ResetToDefault();

                UpdateTitle();
                gl.Invalidate();
            }
        }

        /// <summary>
        /// 重新加载当前文件
        /// </summary>
        private void ReloadCurrentFile()
        {
            if (!string.IsNullOrEmpty(pointCloudData.CurrentFilePath))
            {
                LoadPointCloudFile(pointCloudData.CurrentFilePath);
            }
        }

        /// <summary>
        /// 重置视图
        /// </summary>
        private void ResetView()
        {
            if (pointCloudData.Points == null || pointCloudData.Points.Count == 0) return;
            
            // 使用原版本的简单重置方式
            pointCloudData.Center = new OpenTK.Vector3(0, 0, 0);
            pointCloudData.ObjectScale = 1.0f;
            camera.Distance = 10f;
            camera.ResetToDefault();
            
            gl.Invalidate();
        }

        /// <summary>
        /// 居中并适配点云视图
        /// </summary>
        private void CenterAndFitPointCloud()
        {
            if (pointCloudData.Points == null || pointCloudData.Points.Count == 0) return;

            // 简单重置，不计算中心
            pointCloudData.Center = new OpenTK.Vector3(0, 0, 0);
            pointCloudData.ObjectScale = 1.0f;
            camera.Distance = 10f;

            // 完全重置相机状态
            camera.ResetToDefault();
            
            // 强制清零所有偏移
            camera.Pan = new OpenTK.Vector2(0, 0);
            camera.PointCloudYaw = 0f;
            camera.PointCloudPitch = 0f;
            camera.GlobalScale = 1.0f;

            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 强制将视角移到坐标原点，忽略点云计算的中心
        /// </summary>
        private void ForceToOrigin()
        {
            if (pointCloudData.Points == null || pointCloudData.Points.Count == 0) return;

            // 强制设置中心为原点
            pointCloudData.Center = new OpenTK.Vector3(0, 0, 0);
            pointCloudData.ObjectScale = 1.0f;  // 使用固定缩放
            
            // 重置所有相机参数
            camera.ResetToDefault();
            camera.Distance = 10f;  // 与原版本一致的距离
            camera.Pan = new OpenTK.Vector2(0, 0);
            camera.PointCloudYaw = 0f;
            camera.PointCloudPitch = 0f;
            camera.GlobalScale = 1.0f;
            
            // 设置合适的点大小
            renderer.PointSize = 2.0f;

            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 增大点大小
        /// </summary>
        private void IncreasePointSize()
        {
            float currentSize = renderer.PointSize;
            float newSize = Math.Min(currentSize * 1.3f, 50.0f);  // 最大50像素
            renderer.PointSize = newSize;
            
            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 减小点大小
        /// </summary>
        private void DecreasePointSize()
        {
            float currentSize = renderer.PointSize;
            float newSize = Math.Max(currentSize / 1.3f, 0.5f);  // 最小0.5像素
            renderer.PointSize = newSize;
            
            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 重置点大小到默认值
        /// </summary>
        private void ResetPointSize()
        {
            float currentSize = renderer.PointSize;
            float defaultSize = 2.0f;
            renderer.PointSize = defaultSize;
            
            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 切换立面显示/隐藏
        /// </summary>
        private void ToggleFacade(Analysis.FacadeManager.FacadeType facade)
        {
            // 确保有点云数据
            if (pointCloudData?.Points == null || pointCloudData.Points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("错误: 没有点云数据可供立面分析");
                return;
            }

            // 切换到立面分析模式，但不影响原始点云显示状态
            if (renderer.ColorManager.CurrentColorMode != Rendering.ColorMode.Facade)
            {
                System.Diagnostics.Debug.WriteLine("切换到立面分析模式");
                renderer.ColorManager.CurrentColorMode = Rendering.ColorMode.Facade;
            }

            // 若尚未生成，则进行一次分析；仅控制生成立面自身的显示
            if (!renderer.ColorManager.FacadeManager.IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("开始立面分析...");
                renderer.ColorManager.FacadeManager.AnalyzeFacades(pointCloudData.Points);
                System.Diagnostics.Debug.WriteLine("立面分析完成");
            }

            // 切换立面显示状态
            bool isVisible = renderer.ColorManager.FacadeManager.ToggleFacadeVisibility(facade);
            string facadeName = renderer.ColorManager.FacadeManager.GetFacadeName(facade);
            
            System.Diagnostics.Debug.WriteLine($"{facadeName}: {(isVisible ? "显示" : "隐藏")}");
            
            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 显示所有立面
        /// </summary>
        private void ShowAllFacades()
        {
            // 确保有点云数据
            if (pointCloudData?.Points == null || pointCloudData.Points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("错误: 没有点云数据可供立面分析");
                return;
            }

            // 切换到立面分析模式
            if (renderer.ColorManager.CurrentColorMode != Rendering.ColorMode.Facade)
            {
                System.Diagnostics.Debug.WriteLine("切换到立面分析模式");
                renderer.ColorManager.CurrentColorMode = Rendering.ColorMode.Facade;
                
                // 首次切换时执行立面分析
                System.Diagnostics.Debug.WriteLine("开始立面分析...");
                renderer.ColorManager.FacadeManager.AnalyzeFacades(pointCloudData.Points);
                System.Diagnostics.Debug.WriteLine("立面分析完成");
            }

            // 显示所有立面
            renderer.ColorManager.FacadeManager.ShowAllFacades();
            System.Diagnostics.Debug.WriteLine("显示所有立面");
            
            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 创建点大小调整的右键菜单
        /// </summary>
        private void ShowPointSizeMenu(Point location)
        {
            var contextMenu = new ContextMenuStrip();
            
            // 当前点大小显示
            var currentSizeItem = new ToolStripMenuItem($"当前点大小: {renderer.PointSize:F1}px");
            currentSizeItem.Enabled = false;
            contextMenu.Items.Add(currentSizeItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            // 预设大小选项
            var sizes = new float[] { 0.5f, 1.0f, 2.0f, 3.0f, 5.0f, 8.0f, 12.0f, 20.0f };
            foreach (var size in sizes)
            {
                var item = new ToolStripMenuItem($"设为 {size:F1}px");
                item.Click += (s, e) => {
                    renderer.PointSize = size;
                    UpdateTitle();
                    gl.Invalidate();
                };
                contextMenu.Items.Add(item);
            }
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            // 调整选项
            var increaseItem = new ToolStripMenuItem("增大 (+30%)");
            increaseItem.Click += (s, e) => IncreasePointSize();
            contextMenu.Items.Add(increaseItem);
            
            var decreaseItem = new ToolStripMenuItem("减小 (-30%)");
            decreaseItem.Click += (s, e) => DecreasePointSize();
            contextMenu.Items.Add(decreaseItem);
            
            var resetItem = new ToolStripMenuItem("重置到默认 (2.0px)");
            resetItem.Click += (s, e) => ResetPointSize();
            contextMenu.Items.Add(resetItem);
            
            contextMenu.Show(gl, location);
        }



        /// <summary>
        /// 循环切换坐标映射
        /// </summary>
        private void CycleCoordinateMapping()
        {
            int nextMapping = ((int)currentMapping + 1) % Enum.GetValues(typeof(CoordinateMapping)).Length;
            SetCoordinateMapping((CoordinateMapping)nextMapping);
        }

        /// <summary>
        /// 设置坐标映射
        /// </summary>
        private void SetCoordinateMapping(CoordinateMapping mapping)
        {
            currentMapping = mapping;
            
            if (pointCloudData.OriginalPoints != null && pointCloudData.OriginalPoints.Count > 0)
            {
                CoordinateMapper.ApplyMapping(pointCloudData, currentMapping);
                UpdateTitle();
                gl.Invalidate();
                
                string mappingName = CoordinateMapper.GetMappingName(currentMapping);
                string mappingFormula = CoordinateMapper.GetMappingFormula(currentMapping);
                System.Diagnostics.Debug.WriteLine($"坐标映射切换为: {mappingName} {mappingFormula}");
            }
        }

        /// <summary>
        /// 调整点大小
        /// </summary>
        private void AdjustPointSize(float factor)
        {
            renderer.PointSize *= factor;
            UpdateTitle();
            gl.Invalidate();
            
            System.Diagnostics.Debug.WriteLine($"点大小: {renderer.PointSize:F1}");
        }

        /// <summary>
        /// 从墙面分离结果计算凸包多边形
        /// </summary>
        private List<Vector2> CalculateWallBoundaries()
        {
            if (detectedWalls == null || detectedWalls.Count == 0)
                return null;

            // 只考虑垂直墙面（排除水平面）
            var verticalWalls = detectedWalls.Where(w => w.Direction != WallSeparationAnalyzer.WallDirection.Horizontal).ToList();
            
            if (verticalWalls.Count == 0)
                return null;

            System.Diagnostics.Debug.WriteLine($"计算墙面凸包多边形：共 {verticalWalls.Count} 面垂直墙");

            // 收集所有垂直墙面的点，压缩Z轴到XY平面
            var allWallPoints2D = new List<Vector2>();
            
            foreach (var wall in verticalWalls)
            {
                foreach (var point in wall.Points)
                {
                    // 压缩Z轴，只保留XY坐标
                    allWallPoints2D.Add(new Vector2(point.X, point.Y));
                }
                System.Diagnostics.Debug.WriteLine($"{wall.Name}: {wall.Points.Count} 个点压缩到XY平面");
            }

            // 大数据下先降采样，避免UI阻塞
            if (allWallPoints2D.Count > 20000)
            {
                allWallPoints2D = DownsamplePoints2D(allWallPoints2D, 20000);
                System.Diagnostics.Debug.WriteLine($"墙面点降采样到 {allWallPoints2D.Count} 个用于凸包");
            }

            if (allWallPoints2D.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine("点数不足，无法计算凸包");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"总共 {allWallPoints2D.Count} 个2D点用于计算凸包");

            // 计算凸包
            var convexHull = CalculateConvexHull(allWallPoints2D);
            
            if (convexHull.Count >= 3)
            {
                System.Diagnostics.Debug.WriteLine($"成功计算出 {convexHull.Count} 个凸包顶点");
                for (int i = 0; i < convexHull.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"  顶点{i}: ({convexHull[i].X:F2}, {convexHull[i].Y:F2})");
                }

                // 目标角点数 = 检出的垂直墙面方向数量（通常为4）
                int targetCorners = detectedWalls
                    .Where(w => w.Direction != WallSeparationAnalyzer.WallDirection.Horizontal)
                    .Select(w => w.Direction)
                    .Distinct()
                    .Count();
                targetCorners = Math.Max(3, Math.Min(4, targetCorners)); // 至少3，最多4

                var simplified = SimplifyHullToNCorners(convexHull, targetCorners);
                System.Diagnostics.Debug.WriteLine($"简化到 {simplified.Count} 个角点 (目标 {targetCorners})");
                return simplified;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("凸包计算失败，使用简化边界");
                return CalculateSimpleBoundary(allWallPoints2D);
            }
        }

        /// <summary>
        /// 使用Graham扫描算法计算凸包
        /// </summary>
        private List<Vector2> CalculateConvexHull(List<Vector2> points)
        {
            if (points.Count < 3) return new List<Vector2>(points);

            // 去除重复点和相邻点
            var uniquePoints = RemoveDuplicatePoints(points);
            if (uniquePoints.Count < 3) return uniquePoints;

            // 找到最底部的点（Y最小，如果Y相同则X最小）
            var bottomPoint = uniquePoints.OrderBy(p => p.Y).ThenBy(p => p.X).First();
            
            // 按相对底部点的极角排序
            var sortedPoints = uniquePoints.Where(p => p != bottomPoint)
                .OrderBy(p => Math.Atan2(p.Y - bottomPoint.Y, p.X - bottomPoint.X))
                .ToList();
            
            // 构建凸包
            var hull = new List<Vector2> { bottomPoint };
            
            foreach (var point in sortedPoints)
            {
                // 移除不满足左转条件的点
                while (hull.Count >= 2 && !IsLeftTurn(hull[hull.Count - 2], hull[hull.Count - 1], point))
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(point);
            }

            return hull;
        }

        /// <summary>
        /// 去除重复和过近的点
        /// </summary>
        private List<Vector2> RemoveDuplicatePoints(List<Vector2> points)
        {
            var uniquePoints = new List<Vector2>();
            const float tolerance = 0.01f; // 1cm容差
            
            foreach (var point in points)
            {
                bool isDuplicate = false;
                foreach (var existing in uniquePoints)
                {
                    if (Vector2.Distance(point, existing) < tolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                
                if (!isDuplicate)
                {
                    uniquePoints.Add(point);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"去重后: {uniquePoints.Count} 个独特点 (原始: {points.Count} 个)");
            return uniquePoints;
        }

        /// <summary>
        /// 判断三个点是否构成左转
        /// </summary>
        private bool IsLeftTurn(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) > 0;
        }

        /// <summary>
        /// 简化边界计算（回退方案）
        /// </summary>
        private List<Vector2> CalculateSimpleBoundary(List<Vector2> points)
        {
            if (points.Count == 0) return new List<Vector2>();

            // 使用包围盒作为最简单的边界
            float minX = points.Min(p => p.X);
            float maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxY = points.Max(p => p.Y);

            System.Diagnostics.Debug.WriteLine($"简化边界: X[{minX:F2}, {maxX:F2}], Y[{minY:F2}, {maxY:F2}]");

            return new List<Vector2>
            {
                new Vector2(minX, minY), // 左下
                new Vector2(maxX, minY), // 右下
                new Vector2(maxX, maxY), // 右上
                new Vector2(minX, maxY)  // 左上
            };
        }

        /// <summary>
        /// 将凸包简化为指定角点数：
        /// 1) 先合并近似共线的连续顶点
        /// 2) 再按角度变化/边长贡献度迭代删除最不重要的顶点
        /// </summary>
        private List<Vector2> SimplifyHullToNCorners(List<Vector2> hull, int targetCount)
        {
            if (hull == null || hull.Count <= targetCount) return new List<Vector2>(hull);

            var simplified = MergeNearlyColinearVertices(hull, angleEpsDegrees: 5f, distanceEps: 0.02f);

            // 如果仍然多于目标角点，迭代删除“贡献度”最小的顶点
            while (simplified.Count > targetCount && simplified.Count > 3)
            {
                int removeIndex = FindLeastSignificantVertexIndex(simplified);
                simplified.RemoveAt(removeIndex);
            }

            return simplified;
        }

        private List<Vector2> MergeNearlyColinearVertices(List<Vector2> polygon, float angleEpsDegrees, float distanceEps)
        {
            if (polygon.Count <= 3) return new List<Vector2>(polygon);

            float angleEps = (float)(Math.PI * angleEpsDegrees / 180.0);
            var result = new List<Vector2>();

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[(i - 1 + polygon.Count) % polygon.Count];
                Vector2 b = polygon[i];
                Vector2 c = polygon[(i + 1) % polygon.Count];

                Vector2 ab = (b - a).Normalized();
                Vector2 bc = (c - b).Normalized();

                float angle = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, ab.X * bc.X + ab.Y * bc.Y)));
                bool almostColinear = Math.Abs(angle) < angleEps || Math.Abs(Math.PI - angle) < angleEps;

                bool tooClose = Vector2.Distance(a, b) < distanceEps || Vector2.Distance(b, c) < distanceEps;

                if (!(almostColinear && tooClose))
                {
                    result.Add(b);
                }
            }

            if (result.Count < 3) return new List<Vector2>(polygon);
            return result;
        }

        private int FindLeastSignificantVertexIndex(List<Vector2> polygon)
        {
            int n = polygon.Count;
            int bestIndex = 0;
            float bestScore = float.MaxValue; // 取更小的为更不重要

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = polygon[(i - 1 + n) % n];
                Vector2 curr = polygon[i];
                Vector2 next = polygon[(i + 1) % n];

                // 角度变化越小，点越不重要
                float angleScore = AngleChangeMagnitude(prev, curr, next);

                // 距离贡献：当前点到其相邻边直线的距离
                float distanceScore = PointToLineDistance(curr, prev, next);

                // 综合分数（可调权重）
                float score = angleScore * 0.7f + distanceScore * 0.3f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private float AngleChangeMagnitude(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ab = (b - a).Normalized();
            Vector2 bc = (c - b).Normalized();
            float dot = Math.Max(-1f, Math.Min(1f, ab.X * bc.X + ab.Y * bc.Y));
            float angle = (float)Math.Acos(dot); // 0 ~ pi
            return angle; // 越小越不重要
        }

        private float PointToLineDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.X * ab.X + ab.Y * ab.Y;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2;
            t = Math.Max(0f, Math.Min(1f, t));
            Vector2 proj = new Vector2(a.X + t * ab.X, a.Y + t * ab.Y);
            return Vector2.Distance(p, proj);
        }

        /// <summary>
        /// 按顶点法线近似将多边形向外偏移（米）
        /// 简化实现：
        ///  - 先计算每个顶点的单位外法线（相邻边法线求和）
        ///  - 沿该方向位移offsetMeters
        /// 注意：此为近似方法，凹多边形可能出现轻微自交，范围选择用途可接受
        /// </summary>
        private List<Vector2> OffsetPolygonOutward(List<Vector2> polygon, float offsetMeters)
        {
            if (polygon == null || polygon.Count < 3) return polygon;

            // 判断点序是否为逆时针；若为顺时针则反转，保证外法线方向一致
            if (ComputeSignedArea(polygon) < 0)
            {
                polygon = new List<Vector2>(polygon);
                polygon.Reverse();
            }

            var result = new List<Vector2>(polygon.Count);
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = polygon[(i - 1 + n) % n];
                Vector2 curr = polygon[i];
                Vector2 next = polygon[(i + 1) % n];

                // 边向量
                Vector2 e1 = (curr - prev).Normalized();
                Vector2 e2 = (next - curr).Normalized();

                // 外法线：对于CCW多边形，使用右法线 (e.Y, -e.X)
                Vector2 n1 = new Vector2(e1.Y, -e1.X);
                Vector2 n2 = new Vector2(e2.Y, -e2.X);

                // 顶点法线 = 相邻边外法线归一化
                Vector2 vn = (n1 + n2);
                float len = (float)Math.Sqrt(vn.X * vn.X + vn.Y * vn.Y);
                if (len < 1e-6f)
                {
                    // 退化情况，使用其中一条边法线
                    vn = n1;
                    len = (float)Math.Sqrt(vn.X * vn.X + vn.Y * vn.Y);
                }
                vn = new Vector2(vn.X / len, vn.Y / len);

                // 位移
                Vector2 moved = new Vector2(curr.X + vn.X * offsetMeters, curr.Y + vn.Y * offsetMeters);
                result.Add(moved);
            }

            return result;
        }

        private float ComputeSignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += (a.X * b.Y - b.X * a.Y);
            }
            return 0.5f * area;
        }

        /// <summary>
        /// 3D点降采样（均匀步进抽样）
        /// </summary>
        private List<Vector3> DownsamplePoints(List<Vector3> points, int maxPoints)
        {
            if (points == null || points.Count <= maxPoints) return points;
            var result = new List<Vector3>(maxPoints);
            double step = (double)points.Count / maxPoints;
            double idx = 0.0;
            for (int i = 0; i < maxPoints; i++)
            {
                int pos = (int)Math.Round(idx);
                if (pos >= points.Count) pos = points.Count - 1;
                result.Add(points[pos]);
                idx += step;
            }
            return result;
        }

        /// <summary>
        /// 2D点降采样（均匀步进抽样）
        /// </summary>
        private List<Vector2> DownsamplePoints2D(List<Vector2> points, int maxPoints)
        {
            if (points == null || points.Count <= maxPoints) return points;
            var result = new List<Vector2>(maxPoints);
            double step = (double)points.Count / maxPoints;
            double idx = 0.0;
            for (int i = 0; i < maxPoints; i++)
            {
                int pos = (int)Math.Round(idx);
                if (pos >= points.Count) pos = points.Count - 1;
                result.Add(points[pos]);
                idx += step;
            }
            return result;
        }

        /// <summary>
        /// 启动范围选择
        /// </summary>
        private void StartRangeSelection()
        {
            try
            {
                if (pointCloudData.OriginalPoints == null || pointCloudData.OriginalPoints.Count == 0)
                {
                    MessageBox.Show("请先加载点云数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 弹出墙面检测参数设置
                Analysis.WallDetectionSettings.Values wallCfg;
                if (!Analysis.WallDetectionSettings.TryGetValues(this, out wallCfg))
                {
                    System.Diagnostics.Debug.WriteLine("取消墙面检测参数设置");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("启动范围选择窗口");
                
                // 检查是否已经完成墙面分离，如果是则计算初始边界
                List<Vector2> initialBoundary = null;
                if (detectedWalls != null && detectedWalls.Count > 0)
                {
                    initialBoundary = CalculateWallBoundaries();
                    if (initialBoundary != null && initialBoundary.Count >= 3)
                    {
                        // 向外扩张1米，保证包裹住原数据
                        initialBoundary = OffsetPolygonOutward(initialBoundary, 1.0f);
                    }
                    if (initialBoundary != null)
                    {
                        System.Diagnostics.Debug.WriteLine("使用墙面分离结果作为初始边界");
                    }
                }
                
                using (var rangeWindow = new RangeSelectionWindow(pointCloudData.OriginalPoints, initialBoundary))
                {
                    rangeWindow.RangeSelected += OnRangeSelected;
                    
                    if (rangeWindow.ShowDialog(this) == DialogResult.OK)
                    {
                        System.Diagnostics.Debug.WriteLine("范围选择完成");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("范围选择已取消");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动范围选择失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 处理范围选择完成
        /// </summary>
        private void OnRangeSelected(object sender, List<Vector2> worldPolygon)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"收到范围选择结果，多边形有{worldPolygon.Count}个顶点");
                
                var filteredPoints = PointCloudFilter.FilterPointsInPolygon(pointCloudData.OriginalPoints, worldPolygon);
                
                if (filteredPoints.Count == 0)
                {
                    MessageBox.Show("选择的范围内没有点云数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // 应用过滤结果
                PointCloudFilter.ApplyFilteredPoints(pointCloudData, camera, filteredPoints);
                
                // 重新应用坐标映射
                CoordinateMapper.ApplyMapping(pointCloudData, currentMapping);
                
                // 保存过滤后的数据
                PointCloudFilter.SaveFilteredPointCloud(filteredPoints, pointCloudData.CurrentFilePath);

                // 生成并设置检测墙面点（内存中），并默认显示开关保持现状
                float zMin = filteredPoints.Min(p => p.Z);
                float zMax = filteredPoints.Max(p => p.Z);
                List<Analysis.WallRefinement.WallFace> faces = null;
                try
                {

                    // 使用最新设置
                    float width = Analysis.WallDetectionSettings.Defaults.InitialWidthMeters;
                    float step = Analysis.WallDetectionSettings.Defaults.StepMeters;
                    int minBaseline = Analysis.WallDetectionSettings.Defaults.MinBaselinePoints;
                    bool afterDrop = Analysis.WallDetectionSettings.Defaults.ChooseAfterDrop;
                    float bias = Analysis.WallDetectionSettings.Defaults.OutwardBiasMeters;
                    float ratioTh = Analysis.WallDetectionSettings.Defaults.UseRatioThreshold ? (Analysis.WallDetectionSettings.Defaults.RatioThresholdPercent / 100f) : -1f;

                    faces = Analysis.WallRefinement.DetectFacesByEdgeSweep(
                        filteredPoints,
                        worldPolygon,
                        initialWidthMeters: width,
                        stepMeters: step,
                        minBaselinePoints: minBaseline,
                        chooseAfterDrop: afterDrop,
                        outwardBiasMeters: bias,
                        ratioThreshold: ratioTh);

                    // 保存上下文以用于偏置调整与导出
                    lastDetectedFaces = faces;
                    lastZMin = zMin;
                    lastZMax = zMax;
                    lastPolygon = new List<Vector2>(worldPolygon);
                    lastFilteredPoints = new List<Vector3>(filteredPoints);
                    currentOutwardBias = bias;

                    float along = Analysis.WallDetectionSettings.Defaults.AlongSpacingMeters;
                    float zspace = Analysis.WallDetectionSettings.Defaults.ZSpacingMeters;
                    renderer.DetectedWallPoints = Analysis.WallRefinement.GenerateWallPoints(
                        faces, zMin, zMax, alongSpacing: along, zSpacing: zspace);

                    // 首次输出选定拐点的占比
                    if (!Analysis.WallRefinement.HasReportedOnce && faces != null && faces.Count > 0)
                    {
                        Analysis.WallRefinement.HasReportedOnce = true;
                        var sb = new System.Text.StringBuilder();
                        for (int i = 0; i < faces.Count; i++)
                        {
                            var ff = faces[i];
                            float ratioFace = ff.BaselineCount > 0 ? (float)ff.BestCount / ff.BaselineCount : 0f;
                            sb.AppendLine($"边{i + 1}: {ratioFace * 100f:F1}% (基线{ff.BaselineCount}, 命中{ff.BestCount}, 偏移{ff.BestOffset:F2}m)");
                        }
                        MessageBox.Show("各墙面拐点占比:\n\n" + sb.ToString(), "墙面检测", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    System.Diagnostics.Debug.WriteLine($"检测墙面点: {renderer.DetectedWallPoints.Count} 个");
                }
                catch (System.Exception exWalls)
                {
                    System.Diagnostics.Debug.WriteLine($"生成检测墙面点失败: {exWalls.Message}");
                }

                UpdateTitle();
                gl.Invalidate();
                
                MessageBox.Show($"范围过滤完成！\n\n过滤后: {filteredPoints.Count:N0} 个点", 
                    "过滤完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用范围过滤失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 更新窗口标题
        /// </summary>
        private void UpdateTitle()
        {
            if (pointCloudData.Points != null && pointCloudData.Points.Count > 0)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(pointCloudData.CurrentFilePath);
                string mappingName = CoordinateMapper.GetMappingName(currentMapping);
                string colorMode = renderer.GetColorModeName();
                
                string facadeInfo = "";
                if (renderer.ColorManager.CurrentColorMode == Rendering.ColorMode.Facade)
                {
                    facadeInfo = $" - {renderer.ColorManager.FacadeManager.GetDisplaySummary()}";
                }
                
                // 生成立面状态
                string generatedFacadeInfo = renderer.ShowGeneratedFacades ? " [生成立面:开]" : " [生成立面:关]";
                string biasInfo = renderer.ShowDetectedWalls ? $" [外推偏置:{currentOutwardBias:F2}m]" : "";
                
                // 墙面分离状态
                string wallSeparationInfo = "";
                if (detectedWalls != null && detectedWalls.Count > 0)
                {
                    var verticalWalls = detectedWalls.Where(w => w.Direction != WallSeparationAnalyzer.WallDirection.Horizontal).Count();
                    var horizontalWalls = detectedWalls.Count - verticalWalls;
                    wallSeparationInfo = $" [墙面分离: {verticalWalls}垂直/{horizontalWalls}水平]";
                    
                    if (showWalls)
                    {
                        wallSeparationInfo += " [显示:开]";
                    }
                    else
                    {
                        wallSeparationInfo += " [显示:关]";
                    }
                }
                
                Text = $"点云查看器 增强版 - {fileName} ({pointCloudData.Points.Count:N0} 个点) - " +
                       $"缩放:{camera.GlobalScale:F1}x 点大小:{renderer.PointSize:F1} - " +
                       $"{mappingName} - {colorMode}{facadeInfo}{generatedFacadeInfo}{biasInfo}{wallSeparationInfo} [Ctrl+W:墙面分离 Ctrl+E:导出]";
            }
            else
            {
                Text = "点云查看器 增强版 - 简化版";
            }
        }

        /// <summary>
        /// 切换生成立面显示/隐藏
        /// </summary>
        private void ToggleGeneratedFacades()
        {
            if (pointCloudData?.Points == null || pointCloudData.Points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("错误: 没有点云数据");
                return;
            }

            // 确保立面已分析和生成
            if (!renderer.ColorManager.FacadeManager.IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("开始立面分析和生成...");
                renderer.ColorManager.FacadeManager.AnalyzeFacades(pointCloudData.Points);
                System.Diagnostics.Debug.WriteLine("立面分析和生成完成");

                // 立面生成后，直接导出到程序所在目录
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory?.TrimEnd('\\', '/');
                    string basePath = Path.Combine(baseDir ?? ".", "facades");
                    renderer.ColorManager.FacadeManager.ExportFacadesToQgc(basePath, 0.6f);
                    System.Diagnostics.Debug.WriteLine($"航点导出完成，目录: {baseDir}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"导出航点失败: {ex.Message}");
                }
            }

            // 切换生成立面显示（不影响原始点云 ShowPoints）
            renderer.ToggleGeneratedFacades();
            UpdateTitle();
            gl.Invalidate();
        }

        /// <summary>
        /// 调整外推偏置并重算墙面
        /// </summary>
        private bool isBiasRecomputing = false;
        private DateTime lastBiasRequest = DateTime.MinValue;
        private async void AdjustOutwardBias(float delta)
        {
            if (lastFilteredPoints == null || lastFilteredPoints.Count == 0 || lastPolygon == null || lastPolygon.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine("没有可用于重算的最近范围选择上下文");
                return;
            }

            // 目标偏置（步长最小 0.005m，避免过小变化看不出效果）
            float step = Math.Abs(delta) < 0.005f ? (delta >= 0 ? 0.005f : -0.005f) : delta;
            float targetBias = Math.Max(0f, currentOutwardBias + step);
            if (Math.Abs(targetBias - currentOutwardBias) < 1e-4f)
            {
                return;
            }
            currentOutwardBias = targetBias;
            Analysis.WallDetectionSettings.Defaults.OutwardBiasMeters = currentOutwardBias;

            lastBiasRequest = DateTime.UtcNow;
            if (isBiasRecomputing) return; // 已有重算在进行，等它批次处理

            isBiasRecomputing = true;
            try
            {
                // 防抖：等待 60ms 聚合连续按键
                await Task.Delay(60);
                // 若期间有更新，继续等到稳定 60ms
                while ((DateTime.UtcNow - lastBiasRequest).TotalMilliseconds < 55)
                {
                    await Task.Delay(20);
                }

                // 读取参数
                float width = Analysis.WallDetectionSettings.Defaults.InitialWidthMeters;
                float stepMeters = Analysis.WallDetectionSettings.Defaults.StepMeters;
                int minBaseline = Analysis.WallDetectionSettings.Defaults.MinBaselinePoints;
                bool afterDrop = Analysis.WallDetectionSettings.Defaults.ChooseAfterDrop;
                float ratioTh = Analysis.WallDetectionSettings.Defaults.UseRatioThreshold ? (Analysis.WallDetectionSettings.Defaults.RatioThresholdPercent / 100f) : -1f;
                float along = Analysis.WallDetectionSettings.Defaults.AlongSpacingMeters;
                float zspace = Analysis.WallDetectionSettings.Defaults.ZSpacingMeters;
                float biasNow = currentOutwardBias;

                // 后台重算，避免卡顿
                var faces = await Task.Run(() => Analysis.WallRefinement.DetectFacesByEdgeSweep(
                    lastFilteredPoints,
                    lastPolygon,
                    initialWidthMeters: width,
                    stepMeters: stepMeters,
                    minBaselinePoints: minBaseline,
                    chooseAfterDrop: afterDrop,
                    outwardBiasMeters: biasNow,
                    ratioThreshold: ratioTh));

                // 生成显示点
                var pts = await Task.Run(() => Analysis.WallRefinement.GenerateWallPoints(
                    faces, lastZMin, lastZMax, alongSpacing: along, zSpacing: zspace));

                // 回到UI线程更新
                if (!IsDisposed)
                {
                    try
                    {
                        lastDetectedFaces = faces;
                        renderer.DetectedWallPoints = pts;
                        renderer.ShowDetectedWalls = true;
                        UpdateTitle();
                        gl.Invalidate();
                        gl.Refresh();
                    }
                    catch { }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"外推偏置调整失败: {ex.Message}");
            }
            finally
            {
                isBiasRecomputing = false;
            }
        }

        /// <summary>
        /// 导出当前检测墙面为 PLY
        /// </summary>
        private void ExportDetectedWalls()
        {
            if (lastDetectedFaces == null || lastDetectedFaces.Count == 0)
            {
                MessageBox.Show("当前没有检测到的墙面可导出，请先完成范围选择和检测。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string dir = System.IO.Path.GetDirectoryName(pointCloudData.CurrentFilePath) ?? ".";
                string outPath = System.IO.Path.Combine(dir, $"detected_walls_{DateTime.Now:yyyyMMdd_HHmmss}.ply");

                float along = Analysis.WallDetectionSettings.Defaults.AlongSpacingMeters;
                float zspace = Analysis.WallDetectionSettings.Defaults.ZSpacingMeters;
                Analysis.WallRefinement.ExportFacesAsPly(lastDetectedFaces, outPath, lastZMin, lastZMax, alongSpacing: along, zSpacing: zspace);
                System.Diagnostics.Debug.WriteLine($"墙面已导出: {outPath}");
                MessageBox.Show($"已导出: {outPath}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出墙面检测后的航点文件（每个面一个.waypoints，蛇形排序）
        /// </summary>
        private void ExportDetectedFacesWaypoints()
        {
            try
            {
                if (lastDetectedFaces == null || lastDetectedFaces.Count == 0)
                {
                    MessageBox.Show("当前没有检测到的墙面可导出，请先完成范围选择和检测。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "选择航点文件基名（将为每个面单独生成）";
                    dlg.Filter = "Waypoints (*.waypoints)|*.waypoints|All Files (*.*)|*.*";
                    dlg.FileName = "waypoints";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    // 基名（不含扩展名）
                    var baseNoExt = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(dlg.FileName),
                        System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));

                    // 基于当前点云生成四个立面的规律点云，并按蛇形排序导出
                    var facadeMgr = new Analysis.FacadeManager();
                    facadeMgr.AnalyzeFacades(pointCloudData.OriginalPoints);
                    facadeMgr.ExportFacadesToQgc(baseNoExt);

                    MessageBox.Show("航点文件已按面分别导出。", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出航点失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region 墙面分离功能

        /// <summary>
        /// 执行墙面分离分析
        /// </summary>
        private async void PerformWallSeparation()
        {
            if (pointCloudData.Points == null || pointCloudData.Points.Count == 0)
            {
                MessageBox.Show("请先加载点云数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 显示进度对话框
                using (var progressForm = new Form())
                {
                    progressForm.Text = "墙面分离分析";
                    progressForm.Size = new Size(300, 100);
                    progressForm.StartPosition = FormStartPosition.CenterParent;
                    progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    progressForm.MaximizeBox = false;
                    progressForm.MinimizeBox = false;
                    
                    var label = new Label()
                    {
                        Text = "正在分析墙面，请稍候...",
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    progressForm.Controls.Add(label);
                    
                    progressForm.Show(this);
                    Application.DoEvents();

                    // 在后台线程执行墙面分析
                    var walls = await Task.Run(() => wallAnalyzer.AnalyzeWalls(pointCloudData.Points));
                    
                    progressForm.Close();

                    if (walls != null && walls.Count > 0)
                    {
                        detectedWalls = walls;
                        showWalls = true;
                        
                        // 显示分析结果
                        var report = wallAnalyzer.GenerateWallReport(walls);
                        MessageBox.Show(report, "墙面分析完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        // 更新标题和重新渲染
                        UpdateTitle();
                        gl.Invalidate();
                    }
                    else
                    {
                        MessageBox.Show("未检测到有效的墙面结构", "分析结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"墙面分离分析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"墙面分离分析异常: {ex}");
            }
        }

        /// <summary>
        /// 显示墙面检测参数设置对话框
        /// </summary>
        private void ShowWallDetectionSettings()
        {
            var settingsForm = new WallSeparationSettings(wallAnalyzer);
            if (settingsForm.ShowDialog(this) == DialogResult.OK)
            {
                // 参数已在对话框中直接修改，这里可以选择重新执行分析
                if (detectedWalls != null && MessageBox.Show("参数已更新，是否重新执行墙面分离分析？", "确认", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    PerformWallSeparation();
                }
            }
        }

        /// <summary>
        /// 切换墙面显示状态
        /// </summary>
        private void ToggleWallsVisibility()
        {
            showWalls = !showWalls;
            gl.Invalidate();
        }

        /// <summary>
        /// 切换原始点云显示状态
        /// </summary>
        private void ToggleOriginalPointCloudVisibility()
        {
            showOriginalPointCloud = !showOriginalPointCloud;
            gl.Invalidate();
        }

        /// <summary>
        /// 切换边界框显示
        /// </summary>
        private void ToggleBoundingBoxes()
        {
            // 这里可以添加边界框显示逻辑
            // 暂时显示墙面统计信息
            if (detectedWalls != null && detectedWalls.Count > 0)
            {
                wallRenderer.RenderWallStatistics(detectedWalls, Vector3.Zero);
            }
        }

        /// <summary>
        /// 导出分离的墙面数据
        /// </summary>
        private void ExportSeparatedWalls()
        {
            if (detectedWalls == null || detectedWalls.Count == 0)
            {
                MessageBox.Show("没有可导出的墙面数据，请先执行墙面分离分析", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "PLY文件|*.ply|所有文件|*.*";
                    saveDialog.Title = "导出分离墙面";
                    saveDialog.FileName = $"separated_walls_{DateTime.Now:yyyyMMdd_HHmmss}.ply";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        ExportWallsToPly(detectedWalls, saveDialog.FileName);
                        MessageBox.Show($"墙面数据已导出到: {saveDialog.FileName}", "导出成功", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出墙面分析报告
        /// </summary>
        private void ExportWallAnalysisReport()
        {
            if (detectedWalls == null || detectedWalls.Count == 0)
            {
                MessageBox.Show("没有可导出的分析报告，请先执行墙面分离分析", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "文本文件|*.txt|所有文件|*.*";
                    saveDialog.Title = "导出墙面分析报告";
                    saveDialog.FileName = $"wall_analysis_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var report = wallAnalyzer.GenerateWallReport(detectedWalls);
                        File.WriteAllText(saveDialog.FileName, report, System.Text.Encoding.UTF8);
                        MessageBox.Show($"分析报告已导出到: {saveDialog.FileName}", "导出成功", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 将墙面数据导出为PLY格式
        /// </summary>
        private void ExportWallsToPly(List<WallSeparationAnalyzer.Wall> walls, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                // 计算总点数
                int totalPoints = walls.Sum(w => w.Points.Count);
                
                // 写入PLY头部
                writer.WriteLine("ply");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine($"element vertex {totalPoints}");
                writer.WriteLine("property float x");
                writer.WriteLine("property float y");
                writer.WriteLine("property float z");
                writer.WriteLine("property uchar red");
                writer.WriteLine("property uchar green");
                writer.WriteLine("property uchar blue");
                writer.WriteLine("end_header");

                // 写入点数据
                foreach (var wall in walls)
                {
                    var color = wall.Color;
                    byte r = (byte)(color.X * 255);
                    byte g = (byte)(color.Y * 255);
                    byte b = (byte)(color.Z * 255);

                    foreach (var point in wall.Points)
                    {
                        writer.WriteLine($"{point.X:F6} {point.Y:F6} {point.Z:F6} {r} {g} {b}");
                    }
                }
            }
        }

        #endregion
    }
}
