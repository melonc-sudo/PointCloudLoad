using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Windows.Forms;
using LoadPCDtest.Core;
using LoadPCDtest.Rendering;
using LoadPCDtest.IO;
using LoadPCDtest.Filtering;
using System.Collections.Generic;

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
            
            toolsMenu.DropDownItems.Add(rangeSelectionMenuItem);
            
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
                    // F1：切换东面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.East);
                    break;
                case Keys.F2:
                    // F2：切换西面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.West);
                    break;
                case Keys.F3:
                    // F3：切换南面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.South);
                    break;
                case Keys.F4:
                    // F4：切换北面显示/隐藏
                    ToggleFacade(Analysis.FacadeManager.FacadeType.North);
                    break;
                case Keys.F5:
                    // F5：显示所有立面
                    ShowAllFacades();
                    break;
                case Keys.F6:
                    // F6：切换立面模式（规律立面 ↔ 原始立面）
                    ToggleFacadeMode();
                    break;
                case Keys.G:
                    // G：切换生成立面显示/隐藏
                    ToggleGeneratedFacades();
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
                
                string facadeInfo = "";
                if (renderer.ColorManager.CurrentColorMode == Rendering.ColorMode.Facade)
                {
                    facadeInfo = $" - {renderer.ColorManager.FacadeManager.GetDisplaySummary()}";
                }
                
                // 生成立面状态
                string generatedFacadeInfo = renderer.ShowGeneratedFacades ? " [生成立面:开]" : " [生成立面:关]";
                
                Text = $"点云查看器 - {fileName} ({pointCloudData.Points.Count:N0} 个点) - " +
                       $"缩放:{camera.GlobalScale:F1}x 点大小:{renderer.PointSize:F1} - " +
                       $"{mappingName} - {colorMode}{facadeInfo}{generatedFacadeInfo} [P:原始点云 G:生成立面 F1-F4:立面控制]";
            }
            else
            {
                Text = "点云查看器 - 简化版";
            }
        }

        /// <summary>
        /// 切换立面模式（规律立面 ↔ 原始立面）
        /// </summary>
        private void ToggleFacadeMode()
        {
            if (pointCloudData?.Points == null || pointCloudData.Points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("错误: 没有点云数据可供立面分析");
                return;
            }

            // 确保立面已分析
            System.Diagnostics.Debug.WriteLine("开始立面分析...");
            renderer.ColorManager.FacadeManager.AnalyzeFacades(pointCloudData.Points);
            System.Diagnostics.Debug.WriteLine("立面分析完成");

            // 在两种立面模式之间切换
            if (renderer.ColorManager.CurrentColorMode == Rendering.ColorMode.Facade)
            {
                renderer.ColorManager.CurrentColorMode = Rendering.ColorMode.OriginalFacade;
                System.Diagnostics.Debug.WriteLine("切换到原始立面模式");
            }
            else if (renderer.ColorManager.CurrentColorMode == Rendering.ColorMode.OriginalFacade)
            {
                renderer.ColorManager.CurrentColorMode = Rendering.ColorMode.Facade;
                System.Diagnostics.Debug.WriteLine("切换到规律立面模式");
            }
            else
            {
                // 如果当前不在立面模式，则切换到规律立面模式
                renderer.ColorManager.CurrentColorMode = Rendering.ColorMode.Facade;
                System.Diagnostics.Debug.WriteLine("切换到规律立面模式");
            }

            UpdateTitle();
            gl.Invalidate();
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
            }

            // 切换生成立面显示
            renderer.ToggleGeneratedFacades();
            UpdateTitle();
            gl.Invalidate();
        }
    }
}
