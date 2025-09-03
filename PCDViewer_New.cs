using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Windows.Forms;
using LoadPCDtest.Core;
using LoadPCDtest.Rendering;
using LoadPCDtest.IO;
using LoadPCDtest.Filtering;
using LoadPCDtest.Analysis;
using System.Collections.Generic;
using System.Linq;

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
        private WallSeparationAnalyzer wallAnalyzer;
        
        // 交互状态
        private Point lastMouse;
        
        public PCDViewer_New(string filePath = null)
        {
            InitializeComponents();
            InitializeManagers();
            
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
            
            var wallSeparationAdvancedMenuItem = new ToolStripMenuItem("高级墙面分离(&A)");
            wallSeparationAdvancedMenuItem.Click += (s, e) => PerformAdvancedWallSeparation();
            
            var wallAnalysisDebugMenuItem = new ToolStripMenuItem("墙面分析调试(&D)");
            wallAnalysisDebugMenuItem.Click += (s, e) => ShowWallAnalysisDebug();
            
            toolsMenu.DropDownItems.Add(rangeSelectionMenuItem);
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            toolsMenu.DropDownItems.Add(wallSeparationMenuItem);
            toolsMenu.DropDownItems.Add(wallSeparationAdvancedMenuItem);
            toolsMenu.DropDownItems.Add(wallAnalysisDebugMenuItem);
            
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
            
            displayMenu.DropDownItems.Add(pointSizeSubMenu);
            displayMenu.DropDownItems.Add(new ToolStripSeparator());

            // 墙面显示选项
            var wallDisplaySubMenu = new ToolStripMenuItem("墙面显示(&W)");
            
            var showWallsMenuItem = new ToolStripMenuItem("显示墙面(&S)");
            showWallsMenuItem.CheckOnClick = true;
            showWallsMenuItem.Click += (s, e) => ToggleWallDisplay();
            
            var showOriginalPointsMenuItem = new ToolStripMenuItem("显示原始点云(&O)");
            showOriginalPointsMenuItem.CheckOnClick = true;
            showOriginalPointsMenuItem.Checked = true;
            showOriginalPointsMenuItem.Click += (s, e) => ToggleOriginalPointsDisplay();
            
            var showWallBoundingBoxMenuItem = new ToolStripMenuItem("显示墙面边界框(&B)");
            showWallBoundingBoxMenuItem.CheckOnClick = true;
            showWallBoundingBoxMenuItem.Click += (s, e) => ToggleWallBoundingBoxDisplay();
            
            var showWallFourSidedBoxMenuItem = new ToolStripMenuItem("显示墙体四侧包围(&Q)");
            showWallFourSidedBoxMenuItem.CheckOnClick = true;
            showWallFourSidedBoxMenuItem.Click += (s, e) => ToggleWallFourSidedBoxes();

            wallDisplaySubMenu.DropDownItems.Add(showWallsMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(showOriginalPointsMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(showWallBoundingBoxMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(showWallFourSidedBoxMenuItem);
            
            // 各个墙面的显示控制
            wallDisplaySubMenu.DropDownItems.Add(new ToolStripSeparator());
            var northWallMenuItem = new ToolStripMenuItem("北墙(红色)(&N)");
            northWallMenuItem.CheckOnClick = true;
            northWallMenuItem.Checked = true;
            northWallMenuItem.Click += (s, e) => ToggleNorthWall();
            
            var southWallMenuItem = new ToolStripMenuItem("南墙(绿色)(&S)");
            southWallMenuItem.CheckOnClick = true;
            southWallMenuItem.Checked = true;
            southWallMenuItem.Click += (s, e) => ToggleSouthWall();
            
            var eastWallMenuItem = new ToolStripMenuItem("东墙(蓝色)(&E)");
            eastWallMenuItem.CheckOnClick = true;
            eastWallMenuItem.Checked = true;
            eastWallMenuItem.Click += (s, e) => ToggleEastWall();
            
            var westWallMenuItem = new ToolStripMenuItem("西墙(黄色)(&W)");
            westWallMenuItem.CheckOnClick = true;
            westWallMenuItem.Checked = true;
            westWallMenuItem.Click += (s, e) => ToggleWestWall();

            var horizontalSurfaceMenuItem = new ToolStripMenuItem("水平面(&H)");
            horizontalSurfaceMenuItem.CheckOnClick = true;
            horizontalSurfaceMenuItem.Click += (s, e) => ToggleHorizontalSurface();

            wallDisplaySubMenu.DropDownItems.Add(northWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(southWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(eastWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(westWallMenuItem);
            wallDisplaySubMenu.DropDownItems.Add(horizontalSurfaceMenuItem);
            
            displayMenu.DropDownItems.Add(wallDisplaySubMenu);

            // 建筑包裹外立面（点云）
            displayMenu.DropDownItems.Add(new ToolStripSeparator());
            var generateEnclosureWallsMenuItem = new ToolStripMenuItem("生成建筑包裹外立面(&G)");
            generateEnclosureWallsMenuItem.Click += (s, e) => GenerateEnclosureWalls();
            generateEnclosureWallsMenuItem.ToolTipText = "根据建筑物轮廓生成完整的包裹性外立面点云";
            var showEnclosureWallsMenuItem = new ToolStripMenuItem("显示建筑包裹外立面(&V)");
            showEnclosureWallsMenuItem.CheckOnClick = true;
            showEnclosureWallsMenuItem.Click += (s, e) => ToggleEnclosureWalls();
            showEnclosureWallsMenuItem.ToolTipText = "切换显示/隐藏生成的包裹外立面点云";
            displayMenu.DropDownItems.Add(generateEnclosureWallsMenuItem);
            displayMenu.DropDownItems.Add(showEnclosureWallsMenuItem);

            // 导出包裹外立面为PLY
            var exportEnclosureMenuItem = new ToolStripMenuItem("导出包裹外立面为PLY(&E)");
            exportEnclosureMenuItem.Click += (s, e) => ExportEnclosureWallsAsPLY();
            displayMenu.DropDownItems.Add(exportEnclosureMenuItem);
            
            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(toolsMenu);
            menuStrip.Items.Add(displayMenu);
            
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
                renderer.RenderPointCloud(pointCloudData, camera, gl.Width, gl.Height);
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
                    renderer.TogglePoints();
                    gl.Invalidate();
                    break;
                case Keys.G:
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
                
                // 将原始点云引用传入渲染器，供外立面紧凑包裹算法使用
                renderer.SetOriginalPointCloudData(pointCloudData);

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
        /// 确保点云可见 - 调整相机和缩放参数
        /// </summary>
        private void EnsurePointCloudVisible()
        {
            if (pointCloudData.Points == null || pointCloudData.Points.Count == 0) return;

            // 简化：使用原版本的固定值
            camera.Distance = 10f;
            renderer.PointSize = 2.0f;
            camera.Pan = new OpenTK.Vector2(0, 0);
            camera.PointCloudYaw = 0f;
            camera.PointCloudPitch = 0f;


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

                System.Diagnostics.Debug.WriteLine("启动范围选择窗口");
                
                using (var rangeWindow = new RangeSelectionWindow(pointCloudData.OriginalPoints))
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
                
                Text = $"点云查看器 - {fileName} ({pointCloudData.Points.Count:N0} 个点) - " +
                       $"缩放:{camera.GlobalScale:F1}x 点大小:{renderer.PointSize:F1} - " +
                       $"{mappingName} - {colorMode} [空格:居中 +/-:点大小 Ctrl+滚轮:调整]";
            }
            else
            {
                Text = "点云查看器 - 简化版";
            }
        }

        #region 墙面分离功能

        /// <summary>
        /// 执行墙面分离分析
        /// </summary>
        private void PerformWallSeparation()
        {
            try
            {
                if (pointCloudData?.Points == null || pointCloudData.Points.Count < 1000)
                {
                    MessageBox.Show("请先加载足够的点云数据（至少1000个点）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("开始墙面分离分析...");
                this.Cursor = Cursors.WaitCursor;

                // 异步执行墙面分析，避免阻塞UI
                var ui = this;
                var pointsToAnalyze = new List<Vector3>(pointCloudData.Points); // 复制数据避免线程安全问题
                
                System.Threading.Tasks.Task.Run(() =>
                {
                    List<WallSeparationAnalyzer.Wall> walls = new List<WallSeparationAnalyzer.Wall>();
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"后台线程开始分析 {pointsToAnalyze.Count} 个点...");
                        walls = wallAnalyzer.AnalyzeWalls(pointsToAnalyze);
                        System.Diagnostics.Debug.WriteLine($"后台线程分析完成，找到 {walls.Count} 个墙面");
                    }
                    catch (Exception exAnalyze)
                    {
                        System.Diagnostics.Debug.WriteLine($"墙面分析线程异常: {exAnalyze}");
                        walls = new List<WallSeparationAnalyzer.Wall>();
                    }

                    // 回到UI线程更新界面
                    ui.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (walls == null || walls.Count == 0)
                            {
                                MessageBox.Show("未能检测到有效的墙面结构。\n可能原因：\n1. 点云数据不是建筑物结构\n2. 点云密度不足\n3. 需要调整检测参数", "分析结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            // 保存墙面数据到渲染器
                            renderer.CurrentWalls = walls;

                            // 生成分析报告
                            string report = wallAnalyzer.GenerateWallReport(walls);
                            System.Diagnostics.Debug.WriteLine(report);

                            // 自动启用墙面显示
                            renderer.ShowWalls = true;

                            // 刷新显示
                            gl.Invalidate();

                            // 显示分析结果
                            var verticalWalls = wallAnalyzer.GetVerticalWallsOnly(walls);
                            MessageBox.Show($"墙面分离完成！\n\n检测到 {walls.Count} 个平面\n其中垂直墙面 {verticalWalls.Count} 个\n\n请使用菜单'显示'->'墙面显示'来控制各墙面的显示。\n\n详细信息请查看Debug输出。", 
                                "墙面分析完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        finally
                        {
                            ui.Cursor = Cursors.Default;
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"墙面分离失败: {ex}");
                MessageBox.Show($"墙面分离失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 光标恢复现在在异步回调中处理
            }
        }

        /// <summary>
        /// 切换墙面显示
        /// </summary>
        private void ToggleWallDisplay()
        {
            renderer.ShowWalls = !renderer.ShowWalls;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"墙面显示: {(renderer.ShowWalls ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换原始点云显示
        /// </summary>
        private void ToggleOriginalPointsDisplay()
        {
            renderer.ShowPoints = !renderer.ShowPoints;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"原始点云显示: {(renderer.ShowPoints ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换墙面边界框显示
        /// </summary>
        private void ToggleWallBoundingBoxDisplay()
        {
            renderer.ShowWallBoundingBoxes = !renderer.ShowWallBoundingBoxes;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"墙面边界框显示: {(renderer.ShowWallBoundingBoxes ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换墙体四侧包围显示
        /// </summary>
        private void ToggleWallFourSidedBoxes()
        {
            renderer.ShowWallFourSidedBoxes = !renderer.ShowWallFourSidedBoxes;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"墙体四侧包围显示: {(renderer.ShowWallFourSidedBoxes ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 切换北墙显示
        /// </summary>
        private void ToggleNorthWall()
        {
            if (renderer.CurrentWalls != null)
            {
                var wallRenderer = GetWallRenderer();
                if (wallRenderer != null)
                {
                    wallRenderer.ShowNorthWall = !wallRenderer.ShowNorthWall;
                    gl.Invalidate();
                    System.Diagnostics.Debug.WriteLine($"北墙显示: {(wallRenderer.ShowNorthWall ? "开启" : "关闭")}");
                }
            }
        }

        /// <summary>
        /// 切换南墙显示
        /// </summary>
        private void ToggleSouthWall()
        {
            if (renderer.CurrentWalls != null)
            {
                var wallRenderer = GetWallRenderer();
                if (wallRenderer != null)
                {
                    wallRenderer.ShowSouthWall = !wallRenderer.ShowSouthWall;
                    gl.Invalidate();
                    System.Diagnostics.Debug.WriteLine($"南墙显示: {(wallRenderer.ShowSouthWall ? "开启" : "关闭")}");
                }
            }
        }

        /// <summary>
        /// 切换东墙显示
        /// </summary>
        private void ToggleEastWall()
        {
            if (renderer.CurrentWalls != null)
            {
                var wallRenderer = GetWallRenderer();
                if (wallRenderer != null)
                {
                    wallRenderer.ShowEastWall = !wallRenderer.ShowEastWall;
                    gl.Invalidate();
                    System.Diagnostics.Debug.WriteLine($"东墙显示: {(wallRenderer.ShowEastWall ? "开启" : "关闭")}");
                }
            }
        }

        /// <summary>
        /// 切换西墙显示
        /// </summary>
        private void ToggleWestWall()
        {
            if (renderer.CurrentWalls != null)
            {
                var wallRenderer = GetWallRenderer();
                if (wallRenderer != null)
                {
                    wallRenderer.ShowWestWall = !wallRenderer.ShowWestWall;
                    gl.Invalidate();
                    System.Diagnostics.Debug.WriteLine($"西墙显示: {(wallRenderer.ShowWestWall ? "开启" : "关闭")}");
                }
            }
        }

        /// <summary>
        /// 切换水平面显示
        /// </summary>
        private void ToggleHorizontalSurface()
        {
            if (renderer.CurrentWalls != null)
            {
                var wallRenderer = GetWallRenderer();
                if (wallRenderer != null)
                {
                    wallRenderer.ShowHorizontalSurfaces = !wallRenderer.ShowHorizontalSurfaces;
                    gl.Invalidate();
                    System.Diagnostics.Debug.WriteLine($"水平面显示: {(wallRenderer.ShowHorizontalSurfaces ? "开启" : "关闭")}");
                }
            }
        }

        /// <summary>
        /// 生成建筑包裹外立面点云（基于建筑物整体轮廓生成四个方向的完整外立面）
        /// </summary>
        private void GenerateEnclosureWalls()
        {
            if (renderer.CurrentWalls == null || renderer.CurrentWalls.Count == 0)
            {
                MessageBox.Show("请先执行墙面分离，检测到墙面后才能生成包裹外立面", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 输入参数：采样步长和扩展量
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "生成建筑包裹外立面点云参数设置:\n\n" +
                "采样步长,扩展距离(米) - 逗号分隔\n" +
                "• 步长: 点云密度控制(0.1-0.3推荐)\n" +
                "• 扩展: 外立面距离建筑的距离(0.2-0.5推荐)\n\n" +
                "例如: 0.2, 0.3",
                "生成建筑包裹外立面",
                "0.2, 0.3");

            if (string.IsNullOrWhiteSpace(input)) return;

            float step = 0.2f, expand = 0.3f;
            try
            {
                var parts = input.Split(',');
                if (parts.Length >= 1) float.TryParse(parts[0].Trim(), out step);
                if (parts.Length >= 2) float.TryParse(parts[1].Trim(), out expand);
            }
            catch { }

            this.Cursor = Cursors.WaitCursor;
            
            try
            {
                renderer.GenerateEnclosureWallsFromWalls(renderer.CurrentWalls, step, expand);
                renderer.ShowEnclosureWalls = true;
                gl.Invalidate();

                var verticalWalls = renderer.CurrentWalls.Where(w => w.Direction != WallSeparationAnalyzer.WallDirection.Horizontal).ToList();
                MessageBox.Show($"✅ 建筑包裹外立面生成完成！\n\n" +
                    $"📊 生成统计:\n" +
                    $"• 检测到墙面方向: {verticalWalls.Count}个\n" +
                    $"• 采样步长: {step:F2}m\n" +
                    $"• 扩展距离: {expand:F2}m\n\n" +
                    $"💡 现在您可以看到黄色的包裹外立面点云，它们形成一个完整的建筑外壳。", 
                    "生成完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// 切换建筑包裹外立面点云显示
        /// </summary>
        private void ToggleEnclosureWalls()
        {
            renderer.ShowEnclosureWalls = !renderer.ShowEnclosureWalls;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"建筑包裹外立面点云显示: {(renderer.ShowEnclosureWalls ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 导出当前生成的包裹外立面为PLY
        /// </summary>
        private void ExportEnclosureWallsAsPLY()
        {
            try
            {
                var pts = renderer.GetEnclosureWallPoints();
                if (pts == null || pts.Count == 0)
                {
                    MessageBox.Show("尚未生成包裹外立面点云，请先执行‘生成建筑包裹外立面’。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dlg = new SaveFileDialog()
                {
                    Title = "导出包裹外立面为PLY",
                    Filter = "PLY 文件 (*.ply)|*.ply",
                    FileName = "enclosure_facade.ply"
                })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        PLYLoader.SavePLY(dlg.FileName, new List<Vector3>(pts));
                        MessageBox.Show("导出完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 获取墙面渲染器的引用（通过反射）
        /// </summary>
        private WallRenderer GetWallRenderer()
        {
            // 使用反射获取PointCloudRenderer中的wallRenderer字段
            var field = typeof(PointCloudRenderer).GetField("wallRenderer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(renderer) as WallRenderer;
        }

        /// <summary>
        /// 执行高级墙面分离分析（带参数调整）
        /// </summary>
        private void PerformAdvancedWallSeparation()
        {
            try
            {
                if (pointCloudData?.Points == null || pointCloudData.Points.Count < 1000)
                {
                    MessageBox.Show("请先加载足够的点云数据（至少1000个点）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 显示参数调整对话框
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    "请输入墙面分离参数（用逗号分隔）：\n" +
                    "格式: 距离阈值(米), 最小点数, 角度阈值(度), 合并距离阈值(米)\n" +
                    "例如: 0.1, 100, 10, 0.5\n\n" +
                    "当前默认值: 0.1, 100, 10, 0.5", 
                    "高级墙面分离参数", 
                    "0.1, 100, 10, 0.5");

                if (string.IsNullOrWhiteSpace(input))
                    return;

                // 解析参数
                var parts = input.Split(',');
                if (parts.Length >= 4)
                {
                    if (float.TryParse(parts[0].Trim(), out float distanceThreshold) &&
                        int.TryParse(parts[1].Trim(), out int minPoints) &&
                        float.TryParse(parts[2].Trim(), out float angleThreshold) &&
                        float.TryParse(parts[3].Trim(), out float mergeDistanceThreshold))
                    {
                        // 应用用户自定义参数
                        wallAnalyzer.DistanceThreshold = distanceThreshold;
                        wallAnalyzer.MinPointsForPlane = minPoints;
                        wallAnalyzer.WallMergeAngleThreshold = angleThreshold;
                        wallAnalyzer.WallMergeDistanceThreshold = mergeDistanceThreshold;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"使用高级参数进行墙面分析:");
                System.Diagnostics.Debug.WriteLine($"  距离阈值: {wallAnalyzer.DistanceThreshold:F3}m");
                System.Diagnostics.Debug.WriteLine($"  最小点数: {wallAnalyzer.MinPointsForPlane}");
                System.Diagnostics.Debug.WriteLine($"  角度阈值: {wallAnalyzer.WallMergeAngleThreshold:F1}°");
                System.Diagnostics.Debug.WriteLine($"  合并距离阈值: {wallAnalyzer.WallMergeDistanceThreshold:F2}m");

                this.Cursor = Cursors.WaitCursor;

                // 异步执行高级墙面分析
                var ui = this;
                var pointsToAnalyze = new List<Vector3>(pointCloudData.Points); // 复制数据避免线程安全问题
                
                System.Threading.Tasks.Task.Run(() =>
                {
                    List<WallSeparationAnalyzer.Wall> walls = new List<WallSeparationAnalyzer.Wall>();
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"高级分析后台线程开始，分析 {pointsToAnalyze.Count} 个点...");
                        walls = wallAnalyzer.AnalyzeWalls(pointsToAnalyze);
                        System.Diagnostics.Debug.WriteLine($"高级分析后台线程完成，找到 {walls.Count} 个墙面");
                    }
                    catch (Exception exAnalyze)
                    {
                        System.Diagnostics.Debug.WriteLine($"高级墙面分析线程异常: {exAnalyze}");
                        walls = new List<WallSeparationAnalyzer.Wall>();
                    }

                    // 回到UI线程更新界面
                    ui.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (walls == null || walls.Count == 0)
                            {
                                MessageBox.Show("未能检测到有效的墙面结构。\n请尝试调整参数：\n- 增大距离阈值（噪声较多时）\n- 减少最小点数（墙面较小时）\n- 调整角度阈值（复杂结构时）", 
                                    "分析结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            // 保存墙面数据到渲染器
                            renderer.CurrentWalls = walls;

                            // 生成分析报告
                            string report = wallAnalyzer.GenerateWallReport(walls);
                            System.Diagnostics.Debug.WriteLine(report);

                            // 自动启用墙面显示
                            renderer.ShowWalls = true;

                            // 刷新显示
                            gl.Invalidate();

                            // 显示分析结果
                            var verticalWalls = wallAnalyzer.GetVerticalWallsOnly(walls);
                            MessageBox.Show($"高级墙面分离完成！\n\n" +
                                $"检测到 {walls.Count} 个平面\n" +
                                $"其中垂直墙面 {verticalWalls.Count} 个\n\n" +
                                $"使用参数:\n" +
                                $"- 距离阈值: {wallAnalyzer.DistanceThreshold:F3}m\n" +
                                $"- 最小点数: {wallAnalyzer.MinPointsForPlane}\n" +
                                $"- 角度阈值: {wallAnalyzer.WallMergeAngleThreshold:F1}°\n" +
                                $"- 合并距离: {wallAnalyzer.WallMergeDistanceThreshold:F2}m\n\n" +
                                $"详细信息请查看Debug输出。",
                                "高级墙面分析完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        finally
                        {
                            ui.Cursor = Cursors.Default;
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"高级墙面分离失败: {ex}");
                MessageBox.Show($"高级墙面分离失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 光标恢复现在在异步回调中处理
            }
        }

        /// <summary>
        /// 显示墙面分析调试信息
        /// </summary>
        private void ShowWallAnalysisDebug()
        {
            if (renderer.CurrentWalls == null || renderer.CurrentWalls.Count == 0)
            {
                MessageBox.Show("请先执行墙面分离分析！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine("=== 当前墙面分析结果 ===");
            debugInfo.AppendLine($"检测时间: {DateTime.Now:HH:mm:ss}");
            debugInfo.AppendLine($"总墙面数: {renderer.CurrentWalls.Count}");
            debugInfo.AppendLine();

            foreach (var wall in renderer.CurrentWalls.OrderByDescending(w => w.Points.Count))
            {
                debugInfo.AppendLine($"🏢 {wall.Name}:");
                debugInfo.AppendLine($"   点数: {wall.Points.Count:N0}");
                debugInfo.AppendLine($"   方向: {wall.Direction}");
                debugInfo.AppendLine($"   法向量: ({wall.Normal.X:F3}, {wall.Normal.Y:F3}, {wall.Normal.Z:F3})");
                debugInfo.AppendLine($"   平面距离: {wall.Distance:F3}");
                if (wall.Points.Count > 0)
                {
                    wall.UpdateCenterPoint();
                    debugInfo.AppendLine($"   中心点: ({wall.CenterPoint.X:F2}, {wall.CenterPoint.Y:F2}, {wall.CenterPoint.Z:F2})");
                }
                debugInfo.AppendLine();
            }

            debugInfo.AppendLine("=== 分析参数 ===");
            debugInfo.AppendLine($"距离阈值: {wallAnalyzer.DistanceThreshold:F3}m");
            debugInfo.AppendLine($"最小点数: {wallAnalyzer.MinPointsForPlane}");
            debugInfo.AppendLine($"角度阈值: {wallAnalyzer.WallMergeAngleThreshold:F1}°");
            debugInfo.AppendLine($"合并距离阈值: {wallAnalyzer.WallMergeDistanceThreshold:F2}m");

            debugInfo.AppendLine();
            debugInfo.AppendLine("=== 可能的问题和建议 ===");
            
            var verticalWalls = renderer.CurrentWalls.Where(w => w.Direction != WallSeparationAnalyzer.WallDirection.Horizontal).ToList();
            
            if (verticalWalls.Count < 4)
            {
                debugInfo.AppendLine("⚠️  垂直墙面少于4个，可能的原因:");
                debugInfo.AppendLine("   - 建筑物不是完整的矩形结构");
                debugInfo.AppendLine("   - 某些墙面点云数据不足");
                debugInfo.AppendLine("   - 距离阈值过小，尝试增大到0.15-0.2");
                debugInfo.AppendLine("   - 最小点数过大，尝试减小到50-80");
            }
            else if (verticalWalls.Count > 4)
            {
                debugInfo.AppendLine("⚠️  垂直墙面超过4个，可能的原因:");
                debugInfo.AppendLine("   - 建筑物有复杂结构（凹凸、转角等）");
                debugInfo.AppendLine("   - 墙面合并不够，尝试增大角度阈值到20-25°");
                debugInfo.AppendLine("   - 尝试增大合并距离阈值到0.5-0.8m");
            }
            else
            {
                debugInfo.AppendLine("✅ 检测到标准的4面墙结构！");
            }

            // 显示调试信息
            var debugForm = new Form
            {
                Text = "墙面分析调试信息",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent
            };

            var textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Text = debugInfo.ToString(),
                Font = new Font("Consolas", 9)
            };

            debugForm.Controls.Add(textBox);
            debugForm.ShowDialog(this);
        }

        #endregion
    }
}
