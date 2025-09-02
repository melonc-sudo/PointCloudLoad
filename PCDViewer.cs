using Kitware.VTK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Q_PclSharp;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.IO;

namespace LoadPCDtest
{
    /// <summary>
    /// 原始的大型PCDViewer类 - 已废弃，请使用PCDViewer_New
    /// </summary>
    [Obsolete("此类已废弃，请使用PCDViewer_New")]
    public partial class PCDViewer_Old : Form
    {
        // ---- OpenGL 控件 ----
        private GLControl gl;

        // ---- 点云数据 ----
        private List<Vector3> originalPoints = new List<Vector3>(); // 原始加载的点
        private List<Vector3> points = new List<Vector3>();        // 应用坐标映射后的点
        private List<PLYLoader.PLYPoint> originalPLYPoints = new List<PLYLoader.PLYPoint>(); // 原始PLY点（含颜色）
        private List<PLYLoader.PLYPoint> plyPoints = new List<PLYLoader.PLYPoint>();        // 映射后的PLY点
        private bool hasColors = false;                 // 是否包含颜色信息
        private Vector3 center = Vector3.Zero;      // 点云重心
        private Vector3 boundingBoxSize = Vector3.One; // 点云边界框尺寸
        private float objectScale = 1.0f;           // 自动缩放系数（把点云缩到合适大小）
        //private bool useLidarAxisMap = true;        // 是否使用 LiDAR -> OpenTK 坐标映射 (X,Y,Z)->(X,Z,-Y)

        // ---- 相机/交互 ----
        private float yaw = 0f;                     // 左右旋转（度）
        private float pitch = 0f;                   // 上下旋转（度）
        private float distance = 10f;               // 相机距离
        private Vector2 pan = Vector2.Zero;         // 平移
        
        // ---- 点云旋转 ----
        private float pointCloudYaw = 0f;           // 点云左右旋转
        private float pointCloudPitch = 0f;         // 点云上下旋转
        private Point lastMouse;                    // 上次鼠标位置
        private bool isRotating = false;
        private bool isPanning = false;
        
        // ---- 显示控制 ----
        private float pointSize = 2.0f;            // 点的大小
        private float globalScale = 1.0f;          // 全局缩放系数
        private bool showAxes = true;              // 是否显示坐标轴
        private bool showBoundingBox = false;      // 是否显示边界框
        private bool showTrackball = false;        // 是否显示轨迹球（默认隐藏）
        private bool isInteracting = false;        // 是否正在交互中
        
        // ---- 3D模型显示 ----
        private bool showMesh = false;             // 是否显示3D网格
        private bool showPoints = true;            // 是否显示点云
        private SurfaceReconstruction.Mesh currentMesh = null;  // 当前的3D网格
        
        // ---- 范围选择模式 ----
        private bool isRangeSelectionMode = false; // 是否处于范围选择模式
        private bool is2DViewMode = false;         // 是否处于2D视角模式
        private List<Vector2> selectedPoints = new List<Vector2>();  // 用户选择的点（世界坐标）
        private List<Vector2> point2DCoords = new List<Vector2>();   // 点云在2D下的坐标
        private Vector2 savedCameraState_pan;      // 保存的相机状态
        private float savedCameraState_yaw, savedCameraState_pitch, savedCameraState_distance;
        private string currentPLYFilePath = "";    // 当前PLY文件路径
        
        // ---- 可拖动图标系统 ----
        private int selectedIconIndex = -1;        // 当前选中的图标索引（-1表示未选中）
        private bool isDraggingIcon = false;       // 是否正在拖动图标
        private float iconSize = 8.0f;             // 图标大小（像素）
        
        // ---- 颜色显示模式 ----
        private enum ColorMode
        {
            HeightBased,    // 基于高度的颜色 (蓝→绿→红)
            OriginalRGB,    // 使用原始RGB颜色
            White           // 白色显示
        }
        private ColorMode currentColorMode = ColorMode.HeightBased;
        
        // ---- 轨迹球参数 ----
        private float trackballRadius = 5.0f;     // 轨迹球半径（固定大小）
        private int trackballSegments = 48;       // 轨迹球分段数（更光滑）
        
        // ---- 坐标映射模式 ----
        public enum CoordinateMapping
        {
            Original,       // (X, Y, Z) - 原始坐标
            FlipZ,          // (X, Y, -Z) - Z轴翻转
            YToZ,           // (X, Z, -Y) - Y变Z，Z变-Y
            ZToY,           // (X, -Z, Y) - Z变-Y，Y变Z
            FlipXZ,         // (-X, Y, -Z) - X和Z轴翻转
            FlipYZ,         // (X, -Y, -Z) - Y和Z轴翻转
            RotateXY,       // (-Y, X, Z) - XY平面旋转90度
            RotateXYFlipZ   // (-Y, X, -Z) - XY旋转+Z翻转
        }
        
        private CoordinateMapping currentMapping = CoordinateMapping.Original;
        private bool useLidarAxisMap = false; // 保留兼容性，但现在用currentMapping

        // ---- 构造函数 ----
        public PCDViewer(string pcdPath = null)
        {
            Text = "点云查看器 - 支持PLY/TXT格式";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            // 创建菜单栏
            CreateMenuBar();

            // 创建 GLControl
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
            
            // 添加键盘事件
            this.KeyPreview = true;
            this.KeyDown += Gl_KeyDown;

            // 如果构造时就给了路径，直接加载；否则窗体显示后弹文件选择框
            Shown += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(pcdPath))
                {
                    TryLoadPointCloud(pcdPath);
                }
                else
                {
                    using (var dlg = new OpenFileDialog()
                    {
                        Title = "选择点云文件",
                        Filter = "所有支持的格式 (*.ply;*.txt)|*.ply;*.txt|PLY文件 (*.ply)|*.ply|CloudCompare ASCII (*.txt)|*.txt|所有文件 (*.*)|*.*"
                    })
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            TryLoadPointCloud(dlg.FileName);
                        }
                    }
                }
            };

            // 键盘快捷键
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.R)
                {
                    ResetView();
                    gl.Invalidate();
                }
                else if (e.KeyCode == Keys.C)
                {
                    // C键循环切换坐标映射模式
                    CycleCoordinateMapping();
                }
                else if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D8)
                {
                    // 数字键1-8直接选择坐标映射模式
                    int mappingIndex = e.KeyCode - Keys.D1;
                    SetCoordinateMapping((CoordinateMapping)mappingIndex);
                }
                else if (e.KeyCode == Keys.F1)
                {
                    // F1显示帮助
                    ShowHelp();
                }
                else if (e.KeyCode == Keys.E && e.Control)
                {
                    // Ctrl+E 手动导出点云数据
                    ManualExportPoints();
                }
                else if (e.KeyCode == Keys.D && e.Control)
                {
                    // Ctrl+D 显示文件详细信息
                    ShowFileDetails();
                }
                else if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                {
                    // + 键：增大点云
                    AdjustPointCloudScale(1.2f);
                }
                else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                {
                    // - 键：缩小点云
                    AdjustPointCloudScale(0.8f);
                }
                else if (e.KeyCode == Keys.PageUp)
                {
                    // Page Up：增大点的大小
                    AdjustPointSize(1.5f);
                }
                else if (e.KeyCode == Keys.PageDown)
                {
                    // Page Down：减小点的大小
                    AdjustPointSize(0.67f);
                }
                else if (e.KeyCode == Keys.Home)
                {
                    // Home：重置所有缩放
                    ResetScaling();
                }
                else if (e.KeyCode == Keys.A)
                {
                    // A键：切换坐标轴显示
                    ToggleAxes();
                }
                else if (e.KeyCode == Keys.B)
                {
                    // B键：切换边界框显示
                    ToggleBoundingBox();
                }
                else if (e.KeyCode == Keys.T)
                {
                    // T键：强制切换轨迹球显示（调试用）
                    ToggleTrackball();
                }
                else if (e.KeyCode == Keys.M)
                {
                    // M键：切换3D网格显示
                    ToggleMesh();
                }
                else if (e.KeyCode == Keys.P)
                {
                    // P键：切换点云显示
                    TogglePoints();
                }
                else if (e.KeyCode == Keys.G)
                {
                    // G键：生成3D网格
                    GenerateMesh();
                }
                else if (e.KeyCode == Keys.X)
                {
                    // X键：切换颜色模式
                    CycleColorMode();
                }
            };
        }

        // ---- 尝试加载并准备可视化 ----
        private void TryLoadPointCloud(string path)
        {
            currentFilePath = path; // 保存当前文件路径
            
            try
            {
                // 智能加载策略：根据文件扩展名和内容自动选择加载器
                string extension = Path.GetExtension(path).ToLower();
                
                if (extension == ".ply" || PLYLoader.IsPLYFile(path))
                {
                    // 保存PLY文件路径用于范围选择
                    currentPLYFilePath = path;
                    
                    // PLY格式文件 - 提供多种加载选项
                    var result = MessageBox.Show(
                        "请选择PLY文件加载方式：\n\n是 = 可视化选择范围（推荐）\n否 = 手动输入范围\n取消 = 原始加载（无优化）",
                        "PLY加载选项", 
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                        
                    bool useVisualSelection = (result == DialogResult.Yes);
                    bool useManualInput = (result == DialogResult.No);
                    bool useOriginalLoading = (result == DialogResult.Cancel);

                    try
                    {
                        if (useVisualSelection)
                        {
                            // 可视化范围选择模式 - 先加载原始数据
                            System.Diagnostics.Debug.WriteLine("可视化范围选择模式 - 加载原始点云...");
                            
                            // 使用原始模式加载所有数据
                            originalPLYPoints = PLYLoader.LoadPLYWithColors(path);
                            
                            // 从PLY点提取坐标
                            originalPoints = new List<Vector3>();
                            foreach (var plyPoint in originalPLYPoints)
                            {
                                originalPoints.Add(plyPoint.Position);
                            }
                            
                            // 检查是否有颜色信息
                            hasColors = false;
                            foreach (var plyPoint in originalPLYPoints)
                            {
                                if (plyPoint.HasColor)
                                {
                                    hasColors = true;
                                    break;
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"原始点云加载完成: {originalPoints.Count:N0} 个点，包含颜色: {hasColors}");
                            System.Diagnostics.Debug.WriteLine("提示: 加载完成后可以使用 '工具' -> '选择范围' 来进行可视化范围选择");
                        }
                        else if (useManualInput)
                        {
                            // 手动输入范围模式
                            System.Diagnostics.Debug.WriteLine("手动输入范围模式...");
                            
                            // 首先显示点云信息
                            PLYLoader.ShowPointCloudInfo(path);
                            
                            // 让用户输入范围
                            string input = ShowInputDialog(
                                "请输入XY范围 (格式: minX,maxX,minY,maxY):\n\n" +
                                "提示: 查看Debug输出了解点云范围信息\n" +
                                "例如: 10.5,45.2,8.1,35.6",
                                "手动指定范围");
                                
                            if (!string.IsNullOrEmpty(input))
                            {
                                var parts = input.Split(',');
                                if (parts.Length == 4 &&
                                    float.TryParse(parts[0], out float minX) &&
                                    float.TryParse(parts[1], out float maxX) &&
                                    float.TryParse(parts[2], out float minY) &&
                                    float.TryParse(parts[3], out float maxY))
                                {
                                    originalPoints = PLYLoader.LoadBuildingPLYWithManualBounds(path, minX, maxX, minY, maxY, 0.5f);
                                    originalPLYPoints.Clear();
                                    hasColors = false;
                                    System.Diagnostics.Debug.WriteLine("手动范围加载成功");
                                }
                                else
                                {
                                    MessageBox.Show("输入格式错误，加载原始数据", "格式错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    // 加载原始数据
                                    originalPLYPoints = PLYLoader.LoadPLYWithColors(path);
                                    
                                    // 从PLY点提取坐标
                                    originalPoints = new List<Vector3>();
                                    foreach (var plyPoint in originalPLYPoints)
                                    {
                                        originalPoints.Add(plyPoint.Position);
                                    }
                                    
                                    // 检查是否有颜色信息
                                    hasColors = false;
                                    foreach (var plyPoint in originalPLYPoints)
                                    {
                                        if (plyPoint.HasColor)
                                        {
                                            hasColors = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // 用户取消了输入，加载原始数据
                                originalPLYPoints = PLYLoader.LoadPLYWithColors(path);
                                
                                // 从PLY点提取坐标
                                originalPoints = new List<Vector3>();
                                foreach (var plyPoint in originalPLYPoints)
                                {
                                    originalPoints.Add(plyPoint.Position);
                                }
                                
                                // 检查是否有颜色信息
                                hasColors = false;
                                foreach (var plyPoint in originalPLYPoints)
                                {
                                    if (plyPoint.HasColor)
                                    {
                                        hasColors = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 使用原始模式加载（含颜色）
                            originalPLYPoints = PLYLoader.LoadPLYWithColors(path);
                            
                            // 从PLY点提取坐标
                            originalPoints = new List<Vector3>();
                            foreach (var plyPoint in originalPLYPoints)
                            {
                                originalPoints.Add(plyPoint.Position);
                            }
                            
                            // 检查是否有颜色信息
                            hasColors = false;
                            foreach (var plyPoint in originalPLYPoints)
                            {
                                if (plyPoint.HasColor)
                                {
                                    hasColors = true;
                                    break;
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"使用PLY原始加载器成功加载文件，包含颜色: {hasColors}");
                        }
                    }
                    catch
                    {
                        // 如果优化加载失败，回退到标准加载
                        originalPoints = PLYLoader.LoadPLY(path);
                        originalPLYPoints.Clear();
                        hasColors = false;
                        System.Diagnostics.Debug.WriteLine("使用PLY加载器成功加载文件（回退模式）");
                    }
                }
                else if (extension == ".txt" || CloudCompareASCIILoader.IsCloudCompareASCII(path))
                {
                    // CloudCompare ASCII格式 (.txt文件或检测到的ASCII格式)
                    originalPoints = CloudCompareASCIILoader.LoadCloudCompareASCII(path);
                    System.Diagnostics.Debug.WriteLine("使用CloudCompare ASCII加载器成功加载文件");
                }
                else
                {
                    throw new NotSupportedException($"不支持的文件格式: {extension}。请使用PLY或TXT格式。");
                }
                
                if (originalPoints == null || originalPoints.Count == 0)
                {
                    MessageBox.Show("点云为空或加载失败。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 应用坐标映射
                ApplyCoordinateMapping();

                //ComputeCenterAndScale(out center, out objectScale, out distance);

                // 更新窗体标题显示点云信息
                UpdateTitle();
                
                gl.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- 计算重心、缩放系数、初始相机距离 ----
        private void ComputeCenterAndScale(out Vector3 c, out float scale, out float camDist)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var p in points)
            {
                min = Vector3.ComponentMin(min, p);
                max = Vector3.ComponentMax(max, p);
            }

            c = (min + max) / 2f;
            float sizeX = max.X - min.X;
            float sizeY = max.Y - min.Y;
            float sizeZ = max.Z - min.Z;
            
            // 设置边界框尺寸
            boundingBoxSize = new Vector3(sizeX, sizeY, sizeZ);
            
            float maxSize = Math.Max(sizeX, Math.Max(sizeY, sizeZ));

            if (maxSize < 1e-6f) { scale = 1f; camDist = 5f; return; }

            // 把点云归一化到 ~ [-5,5] 范围（大致 10 个单位宽）
            scale = 10f / maxSize;

            // 相机距离放到“视锥体”里舒服的位置（越大越远）
            camDist = 18f; // 初始给一个合适的距离；也可以与 maxSize 成比例
            pan = Vector2.Zero;
            yaw = 0f;
            pitch = 0f;
        }

        private void ResetView()
        {
            if (points == null || points.Count == 0) return;
            ComputeCenterAndScale(out center, out objectScale, out distance);
            
            // 重置点云旋转
            pointCloudYaw = 0f;
            pointCloudPitch = 0f;
            pan = Vector2.Zero;
        }

        private void AdjustPointCloudScale(float factor)
        {
            globalScale *= factor;
            globalScale = Math.Max(0.01f, Math.Min(100f, globalScale)); // 限制范围
            
            UpdateTitle();
            gl.Invalidate();
            
            System.Diagnostics.Debug.WriteLine($"点云缩放: {globalScale:F2}x");
        }

        private void AdjustPointSize(float factor)
        {
            pointSize *= factor;
            pointSize = Math.Max(0.1f, Math.Min(20f, pointSize)); // 限制范围
            
            UpdateTitle();
            gl.Invalidate();
            
            System.Diagnostics.Debug.WriteLine($"点大小: {pointSize:F1}");
        }

        private void ResetScaling()
        {
            globalScale = 1.0f;
            pointSize = 2.0f;
            
            UpdateTitle();
            gl.Invalidate();
            
            System.Diagnostics.Debug.WriteLine("重置所有缩放设置");
        }

        private void ToggleAxes()
        {
            showAxes = !showAxes;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"坐标轴显示: {(showAxes ? "开启" : "关闭")}");
        }

        private void ToggleBoundingBox()
        {
            showBoundingBox = !showBoundingBox;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"边界框显示: {(showBoundingBox ? "开启" : "关闭")}");
        }

        private void ToggleTrackball()
        {
            showTrackball = !showTrackball;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"轨迹球显示: {(showTrackball ? "开启" : "关闭")}");
        }

        private void ToggleMesh()
        {
            showMesh = !showMesh;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"3D网格显示: {(showMesh ? "开启" : "关闭")}");
        }

        private void TogglePoints()
        {
            showPoints = !showPoints;
            gl.Invalidate();
            System.Diagnostics.Debug.WriteLine($"点云显示: {(showPoints ? "开启" : "关闭")}");
        }

        private void GenerateMesh()
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
                currentMesh = SurfaceReconstruction.CreateSimpleGridMesh(points, 0.1f);
                
                if (currentMesh.Triangles.Count > 0)
                {
                    showMesh = true;
                    System.Diagnostics.Debug.WriteLine($"3D网格生成成功: {currentMesh.Triangles.Count} 个三角形");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("3D网格生成失败: 没有生成三角形");
                }
                
                gl.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"3D网格生成异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换颜色模式
        /// </summary>
        private void CycleColorMode()
        {
            // 切换到下一个颜色模式
            switch (currentColorMode)
            {
                case ColorMode.HeightBased:
                    currentColorMode = hasColors ? ColorMode.OriginalRGB : ColorMode.White;
                    break;
                case ColorMode.OriginalRGB:
                    currentColorMode = ColorMode.White;
                    break;
                case ColorMode.White:
                    currentColorMode = ColorMode.HeightBased;
                    break;
            }

            string modeName = "";
            switch (currentColorMode)
            {
                case ColorMode.HeightBased:
                    modeName = "高度着色 (蓝→绿→红)";
                    break;
                case ColorMode.OriginalRGB:
                    modeName = "原始RGB颜色";
                    break;
                case ColorMode.White:
                    modeName = "白色显示";
                    break;
            }

            System.Diagnostics.Debug.WriteLine($"颜色模式切换为: {modeName}");
            
            gl.Invalidate(); // 重新绘制
        }

        private void UpdateTitle()
        {
            if (points != null && points.Count > 0 && !string.IsNullOrEmpty(currentFilePath))
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(currentFilePath);
                string extension = System.IO.Path.GetExtension(currentFilePath).ToUpper();
                string mappingName = GetMappingName(currentMapping);
                string mappingFormula = GetMappingFormula(currentMapping);
                
                Text = $"点云查看器 - {fileName}{extension} ({points.Count:N0} 个点) - " +
                       $"缩放:{globalScale:F1}x 点大小:{pointSize:F1} - " +
                       $"{mappingName} {mappingFormula}";
            }
        }

        private void CycleCoordinateMapping()
        {
            // 循环切换到下一个映射模式
            int nextMapping = ((int)currentMapping + 1) % Enum.GetValues(typeof(CoordinateMapping)).Length;
            SetCoordinateMapping((CoordinateMapping)nextMapping);
        }

        private void SetCoordinateMapping(CoordinateMapping mapping)
        {
            currentMapping = mapping;
            
            // 如果有原始数据，立即应用坐标映射
            if (originalPoints != null && originalPoints.Count > 0)
            {
                ApplyCoordinateMapping();
                UpdateTitle();
                gl.Invalidate();
                
                string mappingName = GetMappingName(currentMapping);
                string mappingFormula = GetMappingFormula(currentMapping);
                System.Diagnostics.Debug.WriteLine($"坐标映射切换为: {mappingName} {mappingFormula}");
                
                // 显示当前映射模式
                ShowMappingInfo();
            }
        }

        private void ShowMappingInfo()
        {
            string mappingName = GetMappingName(currentMapping);
            string mappingFormula = GetMappingFormula(currentMapping);
            
            // 在状态栏或临时提示中显示
            this.Text = $"点云查看器 - {mappingName} {mappingFormula}";
            
            // 2秒后恢复正常标题
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 2000;
            timer.Tick += (s, e) => 
            { 
                UpdateTitle(); 
                timer.Stop(); 
                timer.Dispose(); 
            };
            timer.Start();
        }

        private string GetMappingName(CoordinateMapping mapping)
        {
            switch (mapping)
            {
                case CoordinateMapping.Original: return "原始坐标";
                case CoordinateMapping.FlipZ: return "Z轴翻转";
                case CoordinateMapping.YToZ: return "Y→Z变换";
                case CoordinateMapping.ZToY: return "Z→Y变换";
                case CoordinateMapping.FlipXZ: return "XZ轴翻转";
                case CoordinateMapping.FlipYZ: return "YZ轴翻转";
                case CoordinateMapping.RotateXY: return "XY旋转";
                case CoordinateMapping.RotateXYFlipZ: return "XY旋转+Z翻转";
                default: return "未知映射";
            }
        }

        private string GetMappingFormula(CoordinateMapping mapping)
        {
            switch (mapping)
            {
                case CoordinateMapping.Original: return "(X,Y,Z)";
                case CoordinateMapping.FlipZ: return "(X,Y,-Z)";
                case CoordinateMapping.YToZ: return "(X,Z,-Y)";
                case CoordinateMapping.ZToY: return "(X,-Z,Y)";
                case CoordinateMapping.FlipXZ: return "(-X,Y,-Z)";
                case CoordinateMapping.FlipYZ: return "(X,-Y,-Z)";
                case CoordinateMapping.RotateXY: return "(-Y,X,Z)";
                case CoordinateMapping.RotateXYFlipZ: return "(-Y,X,-Z)";
                default: return "(?,?,?)";
            }
        }

        private void ApplyCoordinateMapping()
        {
            if (originalPoints == null || originalPoints.Count == 0) return;
            
            points = new List<Vector3>(originalPoints.Count);
            
            // 同时处理PLY点（如果有的话）
            if (hasColors && originalPLYPoints.Count == originalPoints.Count)
            {
                plyPoints = new List<PLYLoader.PLYPoint>(originalPLYPoints.Count);
            }
            else
            {
                plyPoints.Clear();
            }
            
            for (int i = 0; i < originalPoints.Count; i++)
            {
                var p = originalPoints[i];
                Vector3 transformedPoint;
                
                switch (currentMapping)
                {
                    case CoordinateMapping.Original:
                        transformedPoint = new Vector3(p.X, p.Y, p.Z);
                        break;
                    case CoordinateMapping.FlipZ:
                        transformedPoint = new Vector3(p.X, p.Y, -p.Z);
                        break;
                    case CoordinateMapping.YToZ:
                        transformedPoint = new Vector3(p.X, p.Z, -p.Y);
                        break;
                    case CoordinateMapping.ZToY:
                        transformedPoint = new Vector3(p.X, -p.Z, p.Y);
                        break;
                    case CoordinateMapping.FlipXZ:
                        transformedPoint = new Vector3(-p.X, p.Y, -p.Z);
                        break;
                    case CoordinateMapping.FlipYZ:
                        transformedPoint = new Vector3(p.X, -p.Y, -p.Z);
                        break;
                    case CoordinateMapping.RotateXY:
                        transformedPoint = new Vector3(-p.Y, p.X, p.Z);
                        break;
                    case CoordinateMapping.RotateXYFlipZ:
                        transformedPoint = new Vector3(-p.Y, p.X, -p.Z);
                        break;
                    default:
                        transformedPoint = new Vector3(p.X, p.Y, p.Z);
                        break;
                }
                
                points.Add(transformedPoint);
                
                // 同时处理PLY点的坐标映射
                if (hasColors && i < originalPLYPoints.Count)
                {
                    var plyPoint = new PLYLoader.PLYPoint
                    {
                        Position = transformedPoint,
                        Color = originalPLYPoints[i].Color,
                        HasColor = originalPLYPoints[i].HasColor,
                        Intensity = originalPLYPoints[i].Intensity
                    };
                    plyPoints.Add(plyPoint);
                }
            }
            
            string mappingFormula = GetMappingFormula(currentMapping);
            System.Diagnostics.Debug.WriteLine($"应用坐标映射: {mappingFormula}");
        }

        private void ShowHelp()
        {
            string helpText = @"点云查看器 - 快捷键帮助:

支持的格式:
• PLY格式 (*.ply) - 推荐，CloudCompare导出
• TXT格式 (*.txt) - CloudCompare ASCII导出

鼠标操作:
• 左键拖动: 旋转视图
• 右键拖动: 平移视图  
• 滚轮: 缩放视图

键盘快捷键:
• R: 重置视图到初始状态
• C: 循环切换坐标映射模式
• X: 切换颜色模式 (高度着色/原始RGB/白色)
• 1-8: 直接选择坐标映射模式 (见下方列表)
• +/-: 放大/缩小点云 (或数字键盘的+/-)
• Page Up/Down: 增大/减小点的大小
• Home: 重置所有缩放设置
• A: 切换坐标轴显示/隐藏
• B: 切换边界框显示/隐藏
• T: 强制切换轨迹球显示（调试用，正常情况下拖拽时自动显示）
• G: 生成3D网格模型
• M: 切换3D网格显示/隐藏
• P: 切换点云显示/隐藏
• Ctrl+E: 手动导出点云数据
• Ctrl+D: 显示文件详细信息
• F1: 显示此帮助信息

导出功能:
• 自动导出: 加载文件时自动导出到同目录
• 手动导出: 按Ctrl+E选择保存位置和格式
• 支持格式: TXT (带注释), CSV (纯数据)

调试功能:
• Ctrl+D: 查看文件头部信息和详情
• 控制台输出: 详细的解析过程和统计信息

坐标映射模式 (按数字键直接选择):
• 1: 原始坐标 (X,Y,Z) - 保持文件中的原始坐标
• 2: Z轴翻转 (X,Y,-Z) - 常用于深度翻转
• 3: Y→Z变换 (X,Z,-Y) - 您提到的模式，适合某些LiDAR数据
• 4: Z→Y变换 (X,-Z,Y) - Y和Z轴交换
• 5: XZ轴翻转 (-X,Y,-Z) - X和Z同时翻转
• 6: YZ轴翻转 (X,-Y,-Z) - Y和Z同时翻转
• 7: XY旋转 (-Y,X,Z) - XY平面旋转90度
• 8: XY旋转+Z翻转 (-Y,X,-Z) - 组合变换

使用方法:
• 按C键循环切换所有映射模式
• 按1-8数字键直接选择对应的映射模式
• 实时切换，无需重新加载文件
• 标题栏显示当前映射模式和公式
• 切换时会临时显示映射信息2秒钟";

            MessageBox.Show(helpText, "帮助", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ManualExportPoints()
        {
            if (points == null || points.Count == 0)
            {
                MessageBox.Show("没有加载的点云数据可以导出", "提示", 
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Title = "导出点云数据";
                saveDialog.Filter = "文本文件 (*.txt)|*.txt|CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                saveDialog.FilterIndex = 1;
                saveDialog.FileName = "points_export.txt";

                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    ExportPointsToFile(saveDialog.FileName, points);
                }
            }
        }

        private void ExportPointsToTextFile(string originalPcdPath, List<Vector3> pointsToExport)
        {
            try
            {
                // 生成输出文件路径
                string directory = System.IO.Path.GetDirectoryName(originalPcdPath);
                string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(originalPcdPath);
                string outputPath = System.IO.Path.Combine(directory, $"{fileNameWithoutExt}_points.txt");
                
                ExportPointsToFile(outputPath, pointsToExport);
                
                System.Diagnostics.Debug.WriteLine($"点云数据已自动导出到: {outputPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自动导出点云数据时发生错误:\n{ex.Message}", "导出失败", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"自动导出点云数据失败: {ex.Message}");
            }
        }

        private string currentFilePath = "";

        private void ShowFileDetails()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                MessageBox.Show("没有加载的文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var details = new StringBuilder();
                string extension = Path.GetExtension(currentFilePath).ToLower();
                details.AppendLine($"点云文件详细信息");
                details.AppendLine($"文件路径: {currentFilePath}");
                details.AppendLine($"文件格式: {extension.ToUpper()}");
                details.AppendLine();

                // 读取文件头部
                using (var reader = new StreamReader(currentFilePath, System.Text.Encoding.ASCII))
                {
                    details.AppendLine("文件头部内容:");
                    int maxLines = extension == ".ply" ? 15 : 10; // PLY文件头部通常更长
                    
                    for (int i = 0; i < maxLines; i++)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        details.AppendLine($"  {line}");
                        
                        // PLY格式在end_header后停止，TXT格式显示前几行数据
                        if (line.Trim() == "end_header") break;
                    }
                }

                // 文件大小信息
                var fileInfo = new System.IO.FileInfo(currentFilePath);
                details.AppendLine();
                details.AppendLine($"文件大小: {fileInfo.Length:N0} 字节 ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

                MessageBox.Show(details.ToString(), "点云文件详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取文件详情失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportPointsToFile(string filePath, List<Vector3> pointsToExport)
        {
            try
            {
                string extension = System.IO.Path.GetExtension(filePath).ToLower();
                
                using (var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    if (extension == ".csv")
                    {
                        // CSV格式
                        writer.WriteLine("X,Y,Z");
                        foreach (var point in pointsToExport)
                        {
                            writer.WriteLine($"{point.X:F6},{point.Y:F6},{point.Z:F6}");
                        }
                    }
                    else
                    {
                        // 文本格式
                        writer.WriteLine("# PCD点云数据导出");
                        writer.WriteLine($"# 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"# 点数量: {pointsToExport.Count:N0}");
                        writer.WriteLine($"# 坐标映射: {(useLidarAxisMap ? "已应用 (X,Y,Z) → (X,Z,-Y)" : "原始坐标")}");
                        writer.WriteLine("# 格式: X Y Z");
                        writer.WriteLine();
                        
                        foreach (var point in pointsToExport)
                        {
                            writer.WriteLine($"{point.X:F6} {point.Y:F6} {point.Z:F6}");
                        }
                    }
                }
                
                MessageBox.Show($"点云数据已成功导出到:\n{filePath}\n\n共导出 {pointsToExport.Count:N0} 个点", 
                                "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                System.Diagnostics.Debug.WriteLine($"点云数据已导出到: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出点云数据时发生错误:\n{ex.Message}", "导出失败", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"导出点云数据失败: {ex.Message}");
            }
        }

        // ---- GL 事件 ----
        private void Gl_Load(object sender, EventArgs e)
        {
            if (!gl.Context.IsCurrent) gl.MakeCurrent();

            GL.ClearColor(Color.Black);
            GL.Enable(EnableCap.DepthTest);
            GL.PointSize(pointSize); // 使用可调节的点大小
            
            // 启用点平滑以获得更好的视觉效果
            GL.Enable(EnableCap.PointSmooth);
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);
            
            // 启用线平滑（用于坐标轴）
            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            
            // 启用抗锯齿
            GL.Enable(EnableCap.Multisample);

            SetupProjection();
        }

        private void Gl_Resize(object sender, EventArgs e)
        {
            if (!gl.Context.IsCurrent) gl.MakeCurrent();
            GL.Viewport(0, 0, gl.Width, gl.Height);
            SetupProjection();
            gl.Invalidate();
        }

        private void SetupProjection()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            float aspect = Math.Max(1, gl.Width) / (float)Math.Max(1, gl.Height);
            
            if (is2DViewMode)
            {
                // 2D模式：使用正交投影，创建真正的俯视平面图
                float viewSize = distance * 0.5f; // 视图大小基于距离
                float left = -viewSize * aspect;
                float right = viewSize * aspect;
                float bottom = -viewSize;
                float top = viewSize;
                
                var orthoProj = Matrix4.CreateOrthographic(right - left, top - bottom, 0.01f, 2000f);
                GL.LoadMatrix(ref orthoProj);
                
                System.Diagnostics.Debug.WriteLine($"2D正交投影: 视图大小={viewSize:F2}, 宽高比={aspect:F2}");
            }
            else
            {
                // 3D模式：使用透视投影
                var proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.01f, 2000f);
                GL.LoadMatrix(ref proj);
            }

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }

        private void Gl_Paint(object sender, PaintEventArgs e)
        {
            if (!gl.Context.IsCurrent) gl.MakeCurrent();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // --- 相机/视图变换 ---
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            // 相机空间：距离 + 平移（不旋转相机，保持固定视角）
            GL.Translate(pan.X, pan.Y, -distance);

            // 对象空间：居中 + 点云旋转 + 缩放
            GL.Translate(-center.X, -center.Y, -center.Z);
            GL.Rotate(pointCloudPitch, 1, 0, 0);  // 应用点云旋转
            GL.Rotate(pointCloudYaw, 0, 1, 0);    // 应用点云旋转
            GL.Scale(objectScale * globalScale, objectScale * globalScale, objectScale * globalScale);

            // 坐标轴（先画，方便观察旋转/方向）
            if (showAxes)
            {
                DrawAxes(3.0f);
            }

            // 边界框
            if (showBoundingBox && points != null && points.Count > 0)
            {
                DrawBoundingBox();
            }

            // 轨迹球（在点云中心位置绘制，跟随点云位置和旋转，但不缩放）
            if (showTrackball)
            {
                // 保存当前缩放前的矩阵状态
                GL.PushMatrix();
                // 取消缩放，只保留位置和旋转变换
                GL.Scale(1.0f / (objectScale * globalScale), 1.0f / (objectScale * globalScale), 1.0f / (objectScale * globalScale));
                DrawTrackball();
                GL.PopMatrix();
            }

            // 3D网格
            if (showMesh && currentMesh != null && currentMesh.Triangles.Count > 0)
            {
                DrawMesh();
            }

            // 点云
            if (showPoints && points != null && points.Count > 0)
            {
                // 添加调试信息：检查点云渲染状态
                if (points.Count < 200000) // 只对小数据集输出调试信息
                {
                    System.Diagnostics.Debug.WriteLine($"开始渲染点云: {points.Count} 个点");
                    System.Diagnostics.Debug.WriteLine($"点大小: {pointSize}, 显示点云: {showPoints}");
                    System.Diagnostics.Debug.WriteLine($"前几个点的坐标:");
                    for (int i = 0; i < Math.Min(5, points.Count); i++)
                    {
                        System.Diagnostics.Debug.WriteLine($"  点{i}: ({points[i].X:F2}, {points[i].Y:F2}, {points[i].Z:F2})");
                    }
                }
                
                GL.PointSize(pointSize); // 确保点大小是最新的
                GL.Begin(PrimitiveType.Points);
                
                if (currentColorMode == ColorMode.OriginalRGB && hasColors && plyPoints.Count == points.Count)
                {
                    // 使用原始RGB颜色
                    for (int i = 0; i < plyPoints.Count; i++)
                    {
                        var plyPoint = plyPoints[i];
                        var p = points[i];
                        
                        if (plyPoint.HasColor)
                        {
                            GL.Color3(plyPoint.Color.X, plyPoint.Color.Y, plyPoint.Color.Z);
                        }
                        else
                        {
                            GL.Color3(1.0f, 1.0f, 1.0f); // 白色作为默认
                        }
                        
                        GL.Vertex3(p.X, p.Y, p.Z);
                    }
                }
                else if (currentColorMode == ColorMode.White)
                {
                    // 白色显示
                    GL.Color3(1.0f, 1.0f, 1.0f);
                    foreach (var p in points)
                    {
                        GL.Vertex3(p.X, p.Y, p.Z);
                    }
                }
                else
                {
                    // 基于高度的颜色映射：蓝色(低) -> 绿色(中) -> 红色(高)
                    float minZ = float.MaxValue, maxZ = float.MinValue;
                    foreach (var p in points)
                    {
                        if (p.Z < minZ) minZ = p.Z;
                        if (p.Z > maxZ) maxZ = p.Z;
                    }
                    
                    float zRange = maxZ - minZ;
                    if (zRange < 1e-6f) zRange = 1.0f;
                    
                    foreach (var p in points)
                    {
                        float normalizedZ = (p.Z - minZ) / zRange;
                        
                        float r, g, b;
                        if (normalizedZ < 0.5f)
                        {
                            // 蓝色到绿色
                            r = 0.0f;
                            g = normalizedZ * 2.0f;
                            b = 1.0f - normalizedZ * 2.0f;
                        }
                        else
                        {
                            // 绿色到红色
                            r = (normalizedZ - 0.5f) * 2.0f;
                            g = 1.0f - (normalizedZ - 0.5f) * 2.0f;
                            b = 0.0f;
                        }
                        
                        GL.Color3(r, g, b);
                        GL.Vertex3(p.X, p.Y, p.Z);
                    }
                }
                
                GL.End();
                
                // 添加渲染完成的调试信息
                if (points.Count < 200000)
                {
                    System.Diagnostics.Debug.WriteLine("点云渲染完成");
                }
            }
            else
            {
                if (points == null)
                {
                    System.Diagnostics.Debug.WriteLine("没有点云数据可渲染 (points = null)");
                }
                else if (points.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("没有点云数据可渲染 (points.Count = 0)");
                }
                else if (!showPoints)
                {
                    System.Diagnostics.Debug.WriteLine($"点云显示已关闭 (showPoints = {showPoints})");
                }
            }

            gl.SwapBuffers();
        }

        // ---- 绘制坐标轴 (X=红, Y=绿, Z=蓝) ----
        private void DrawAxes(float len)
        {
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

        // ---- 绘制边界框 ----
        private void DrawBoundingBox()
        {
            if (points == null || points.Count == 0) return;

            // 计算边界框
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var p in points)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            // 绘制边界框线条
            GL.LineWidth(1f);
            GL.Color3(1.0f, 1.0f, 0.0f); // 黄色边界框
            GL.Begin(PrimitiveType.Lines);

            // 底面 (Z = minZ)
            GL.Vertex3(minX, minY, minZ); GL.Vertex3(maxX, minY, minZ);
            GL.Vertex3(maxX, minY, minZ); GL.Vertex3(maxX, maxY, minZ);
            GL.Vertex3(maxX, maxY, minZ); GL.Vertex3(minX, maxY, minZ);
            GL.Vertex3(minX, maxY, minZ); GL.Vertex3(minX, minY, minZ);

            // 顶面 (Z = maxZ)
            GL.Vertex3(minX, minY, maxZ); GL.Vertex3(maxX, minY, maxZ);
            GL.Vertex3(maxX, minY, maxZ); GL.Vertex3(maxX, maxY, maxZ);
            GL.Vertex3(maxX, maxY, maxZ); GL.Vertex3(minX, maxY, maxZ);
            GL.Vertex3(minX, maxY, maxZ); GL.Vertex3(minX, minY, maxZ);

            // 垂直边 (连接底面和顶面)
            GL.Vertex3(minX, minY, minZ); GL.Vertex3(minX, minY, maxZ);
            GL.Vertex3(maxX, minY, minZ); GL.Vertex3(maxX, minY, maxZ);
            GL.Vertex3(maxX, maxY, minZ); GL.Vertex3(maxX, maxY, maxZ);
            GL.Vertex3(minX, maxY, minZ); GL.Vertex3(minX, maxY, maxZ);

            GL.End();
        }

        // ---- 绘制轨迹球 ----
        private void DrawTrackball()
        {
            // 轨迹球大小设为包围点云的球体
            float radius = CalculatePointCloudRadius();
            int segments = trackballSegments;
            
            // 启用混合以实现半透明效果
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            // 绘制三个主要的圆圈（XY, XZ, YZ平面）
            GL.LineWidth(1.0f);  // 更细的线条
            
            // XY平面圆圈 (红色)
            GL.Color4(1.0f, 0.2f, 0.2f, 0.7f);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < segments; i++)
            {
                float angle = 2.0f * (float)Math.PI * i / segments;
                float x = radius * (float)Math.Cos(angle);
                float y = radius * (float)Math.Sin(angle);
                GL.Vertex3(x, y, 0);
            }
            GL.End();
            
            // XZ平面圆圈 (绿色)
            GL.Color4(0.2f, 1.0f, 0.2f, 0.7f);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < segments; i++)
            {
                float angle = 2.0f * (float)Math.PI * i / segments;
                float x = radius * (float)Math.Cos(angle);
                float z = radius * (float)Math.Sin(angle);
                GL.Vertex3(x, 0, z);
            }
            GL.End();
            
            // YZ平面圆圈 (蓝色)
            GL.Color4(0.2f, 0.2f, 1.0f, 0.7f);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < segments; i++)
            {
                float angle = 2.0f * (float)Math.PI * i / segments;
                float y = radius * (float)Math.Cos(angle);
                float z = radius * (float)Math.Sin(angle);
                GL.Vertex3(0, y, z);
            }
            GL.End();
            
            // 绘制中心点
            GL.PointSize(4.0f);  // 稍小一些的中心点
            GL.Color4(1.0f, 1.0f, 1.0f, 0.8f);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(0, 0, 0);
            GL.End();
            
            GL.Disable(EnableCap.Blend);
        }

        // ---- 绘制3D网格 ----
        private void DrawMesh()
        {
            if (currentMesh == null || currentMesh.Triangles.Count == 0) return;

            // 启用光照
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            
            // 设置光源
            float[] lightPos = { 0.0f, 0.0f, 10.0f, 1.0f };
            float[] lightAmbient = { 0.3f, 0.3f, 0.3f, 1.0f };
            float[] lightDiffuse = { 0.8f, 0.8f, 0.8f, 1.0f };
            
            GL.Light(LightName.Light0, LightParameter.Position, lightPos);
            GL.Light(LightName.Light0, LightParameter.Ambient, lightAmbient);
            GL.Light(LightName.Light0, LightParameter.Diffuse, lightDiffuse);
            
            // 设置材质
            float[] materialAmbient = { 0.2f, 0.6f, 0.8f, 1.0f };
            float[] materialDiffuse = { 0.3f, 0.7f, 0.9f, 1.0f };
            float[] materialSpecular = { 1.0f, 1.0f, 1.0f, 1.0f };
            
            GL.Material(MaterialFace.Front, MaterialParameter.Ambient, materialAmbient);
            GL.Material(MaterialFace.Front, MaterialParameter.Diffuse, materialDiffuse);
            GL.Material(MaterialFace.Front, MaterialParameter.Specular, materialSpecular);
            GL.Material(MaterialFace.Front, MaterialParameter.Shininess, 50.0f);
            
            // 绘制三角形
            GL.Begin(PrimitiveType.Triangles);
            
            foreach (var triangle in currentMesh.Triangles)
            {
                // 设置法向量
                GL.Normal3(triangle.Normal.X, triangle.Normal.Y, triangle.Normal.Z);
                
                // 绘制三个顶点
                GL.Vertex3(triangle.V1.X, triangle.V1.Y, triangle.V1.Z);
                GL.Vertex3(triangle.V2.X, triangle.V2.Y, triangle.V2.Z);
                GL.Vertex3(triangle.V3.X, triangle.V3.Y, triangle.V3.Z);
            }
            
            GL.End();
            
            // 可选：绘制线框
            if (currentMesh.Triangles.Count < 1000) // 避免太多线框影响性能
            {
                GL.Disable(EnableCap.Lighting);
                GL.Color3(0.2f, 0.2f, 0.2f); // 深灰色线框
                GL.LineWidth(1.0f);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                
                GL.Begin(PrimitiveType.Triangles);
                foreach (var triangle in currentMesh.Triangles)
                {
                    GL.Vertex3(triangle.V1.X, triangle.V1.Y, triangle.V1.Z);
                    GL.Vertex3(triangle.V2.X, triangle.V2.Y, triangle.V2.Z);
                    GL.Vertex3(triangle.V3.X, triangle.V3.Y, triangle.V3.Z);
                }
                GL.End();
                
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            }
            
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Light0);
            
            // 范围选择可视化（在2D模式下显示）
            if (isRangeSelectionMode && is2DViewMode)
            {
                System.Diagnostics.Debug.WriteLine($"Gl_Paint: 调用DrawRangeSelection，选择点数量={selectedPoints.Count}");
                DrawRangeSelection();
            }
            else if (isRangeSelectionMode)
            {
                System.Diagnostics.Debug.WriteLine($"Gl_Paint: 范围选择模式但不是2D视图，is2DViewMode={is2DViewMode}");
            }
        }

        // ---- 计算轨迹球半径（基于窗体大小） ----
        private float CalculatePointCloudRadius()
        {
            if (gl == null) return trackballRadius;
            
            // 基于窗体大小和相机距离计算合适的轨迹球半径
            float viewportSize = Math.Min(gl.Width, gl.Height);
            
            // 轨迹球应该占据视口的80-90%
            float viewportRatio = 0.85f;
            
            // 根据透视投影计算3D空间中的半径
            // 假设FOV约为45度，计算在当前距离下对应的3D半径
            float fovRadians = (float)(Math.PI / 4); // 45度转弧度
            float radius = (float)(Math.Tan(fovRadians / 2) * distance * viewportRatio);
            
            // 设置合理的范围
            radius = Math.Max(2.0f, Math.Min(50.0f, radius));
            
            return radius;
        }

        // ---- 鼠标交互 ----
        private void Gl_MouseDown(object sender, MouseEventArgs e)
        {
            lastMouse = e.Location;
            
            // 范围选择模式的特殊处理
            if (isRangeSelectionMode)
            {
                HandleRangeSelectionMouseDown(e);
                return;
            }
            
            // 普通模式的鼠标处理
            if (e.Button == MouseButtons.Left) 
            {
                isRotating = true;
                isInteracting = true;
                showTrackball = true;  // 开始拖拽时显示轨迹球
                gl.Invalidate();
            }
            if (e.Button == MouseButtons.Right) 
            {
                isPanning = true;
                isInteracting = true;
            }
        }

        private void Gl_MouseUp(object sender, MouseEventArgs e)
        {
            // 范围选择模式的特殊处理
            if (isRangeSelectionMode && e.Button == MouseButtons.Left)
            {
                if (isDraggingIcon)
                {
                    isDraggingIcon = false;
                    selectedIconIndex = -1;
                    System.Diagnostics.Debug.WriteLine("停止拖动图标");
                    gl.Invalidate();
                }
                return;
            }
            
            // 普通模式的鼠标处理
            if (e.Button == MouseButtons.Left) 
            {
                isRotating = false;
                showTrackball = false;  // 停止拖拽时隐藏轨迹球
                gl.Invalidate();
            }
            if (e.Button == MouseButtons.Right) 
            {
                isPanning = false;
            }
            
            // 检查是否还有其他交互
            if (!isRotating && !isPanning)
            {
                isInteracting = false;
            }
        }

        private void Gl_MouseMove(object sender, MouseEventArgs e)
        {
            // 范围选择模式的特殊处理
            if (isRangeSelectionMode && isDraggingIcon && selectedIconIndex >= 0)
            {
                // 拖动图标，更新选择点的位置
                Vector2 newWorldPoint = ScreenToWorld2D_Orthographic(new Point(e.X, e.Y));
                selectedPoints[selectedIconIndex] = newWorldPoint;
                
                System.Diagnostics.Debug.WriteLine($"拖动图标 {selectedIconIndex} 到世界坐标: ({newWorldPoint.X:F2}, {newWorldPoint.Y:F2})");
                
                gl.Invalidate();
                lastMouse = e.Location;
                return;
            }
            
            // 普通模式的鼠标处理
            var dx = e.X - lastMouse.X;
            var dy = e.Y - lastMouse.Y;

            if (isRotating)
            {
                // 旋转点云数据，而不是相机
                pointCloudYaw += dx * 0.3f;          // 左右旋转点云
                pointCloudPitch += dy * 0.3f;        // 上下旋转点云
                pointCloudPitch = Math.Max(-89f, Math.Min(89f, pointCloudPitch));
                gl.Invalidate();
            }
            else if (isPanning)
            {
                // 平移在屏幕空间转换为世界空间
                // 使用固定的灵敏度，不完全依赖objectScale避免过滤后灵敏度变化
                float panSpeed = 0.01f * distance;  // 基础速度基于距离
                
                // 计算一个稳定的缩放因子，避免过滤后剧烈变化
                float effectiveScale = Math.Max(objectScale * globalScale, 0.001f); // 防止除零
                float normalizedScale = Math.Min(effectiveScale, 1.0f); // 限制最大影响
                
                // 使用更稳定的平移计算
                float panFactor = panSpeed / (normalizedScale * 10f + 0.1f); // 添加基础值防止过度敏感
                
                pan.X += dx * panFactor;
                pan.Y -= dy * panFactor;
                
                System.Diagnostics.Debug.WriteLine($"平移: dx={dx}, dy={dy}, panFactor={panFactor:F6}, effectiveScale={effectiveScale:F6}");
                
                gl.Invalidate();
            }

            lastMouse = e.Location;
        }

        private void Gl_MouseWheel(object sender, MouseEventArgs e)
        {
            // 滚轮缩放：只缩放点云，不改变相机距离（这样轨迹球大小保持不变）
            float zoomFactor = (e.Delta > 0) ? 1.1f : 0.9f;
            globalScale *= zoomFactor;
            globalScale = Math.Max(0.001f, Math.Min(1000f, globalScale));
            
            UpdateTitle();
            gl.Invalidate();
        }
        
        /// <summary>
        /// 显示简单的输入对话框
        /// </summary>
        private static string ShowInputDialog(string message, string title)
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };
            
            Label textLabel = new Label() { Left = 20, Top = 20, Width = 350, Height = 60, Text = message };
            TextBox textBox = new TextBox() { Left = 20, Top = 90, Width = 350 };
            Button confirmation = new Button() { Text = "确定", Left = 230, Width = 70, Top = 120, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "取消", Left = 310, Width = 70, Top = 120, DialogResult = DialogResult.Cancel };
            
            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };
            
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;
            
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        /// <summary>
        /// 创建菜单栏
        /// </summary>
        private void CreateMenuBar()
        {
            var menuStrip = new MenuStrip();
            
            // 工具菜单
            var toolsMenu = new ToolStripMenuItem("工具(&T)");
            
            // 范围选择菜单项
            var rangeSelectionMenuItem = new ToolStripMenuItem("选择范围(&R)");
            rangeSelectionMenuItem.ShortcutKeys = Keys.Control | Keys.R;
            rangeSelectionMenuItem.Click += (sender, e) => StartRangeSelectionMode_New();
            rangeSelectionMenuItem.ToolTipText = "切换到2D视角并通过点击选择要保留的点云范围";
            
            // 退出范围选择模式菜单项
            var exitRangeSelectionMenuItem = new ToolStripMenuItem("退出范围选择(&E)");
            exitRangeSelectionMenuItem.ShortcutKeys = Keys.F6;
            exitRangeSelectionMenuItem.Click += (sender, e) => ExitRangeSelectionMode();
            exitRangeSelectionMenuItem.ToolTipText = "退出范围选择模式，返回3D视角";
            exitRangeSelectionMenuItem.Enabled = false; // 初始状态禁用
            
            // 分隔符
            var separator = new ToolStripSeparator();
            
            // 重新加载原始文件菜单项
            var reloadOriginalMenuItem = new ToolStripMenuItem("重新加载原始文件(&O)");
            reloadOriginalMenuItem.ShortcutKeys = Keys.F5;
            reloadOriginalMenuItem.Click += (sender, e) => ReloadOriginalFile();
            reloadOriginalMenuItem.ToolTipText = "重新加载原始PLY文件的所有数据";
            
            toolsMenu.DropDownItems.Add(rangeSelectionMenuItem);
            toolsMenu.DropDownItems.Add(exitRangeSelectionMenuItem);
            toolsMenu.DropDownItems.Add(separator);
            toolsMenu.DropDownItems.Add(reloadOriginalMenuItem);
            
            menuStrip.Items.Add(toolsMenu);
            
            // 保存菜单项引用以便后续控制
            rangeSelectionMenuItem.Tag = "RangeSelection";
            exitRangeSelectionMenuItem.Tag = "ExitRangeSelection";
            
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }
        
        /// <summary>
        /// 启动范围选择模式
        /// </summary>
        private void StartRangeSelectionMode()
        {
            if (string.IsNullOrEmpty(currentPLYFilePath))
            {
                MessageBox.Show("请先加载一个PLY文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("启动范围选择模式，重新加载原始PLY数据...");
            
            try
            {
                // 重新加载原始PLY数据（不应用坐标映射）
                var rawPoints = PLYLoader.LoadPLYRaw(currentPLYFilePath);
                if (rawPoints == null || rawPoints.Count == 0)
                {
                    MessageBox.Show("无法加载原始点云数据", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // 使用原始数据
                points = rawPoints;
                originalPoints = new List<Vector3>(rawPoints);
                hasColors = false;
                
                // 计算原始数据的中心和缩放
                Vector3 rawMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 rawMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var p in rawPoints)
                {
                    rawMin = Vector3.ComponentMin(rawMin, p);
                    rawMax = Vector3.ComponentMax(rawMax, p);
                }
                
                center = (rawMin + rawMax) / 2f;
                float rawSizeX = rawMax.X - rawMin.X;
                float rawSizeY = rawMax.Y - rawMin.Y;
                float rawSizeZ = rawMax.Z - rawMin.Z;
                float rawMaxSize = Math.Max(rawSizeX, Math.Max(rawSizeY, rawSizeZ));
                
                boundingBoxSize = new Vector3(rawSizeX, rawSizeY, rawSizeZ);
                
                // 设置合适的缩放和相机参数
                if (rawMaxSize > 1e-6f)
                {
                    objectScale = 10000f / rawMaxSize;
                    if (rawMaxSize > 100f)
                    {
                        objectScale = 100f;
                    }
                }
                else
                {
                    objectScale = 1f;
                }
                
                distance = 5f;
                globalScale = 1.0f;
                
                System.Diagnostics.Debug.WriteLine($"原始数据加载完成: {rawPoints.Count:N0} 个点");
                System.Diagnostics.Debug.WriteLine($"数据范围: X[{rawMin.X:F2}, {rawMax.X:F2}], Y[{rawMin.Y:F2}, {rawMax.Y:F2}]");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载原始数据失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            // 保存当前视角和缩放状态
            savedCameraState_pan = pan;
            savedCameraState_yaw = yaw;
            savedCameraState_pitch = pitch;
            savedCameraState_distance = distance;
            
            // 切换到范围选择模式
            isRangeSelectionMode = true;
            selectedPoints.Clear();
            
            System.Diagnostics.Debug.WriteLine($"范围选择模式已启动: isRangeSelectionMode={isRangeSelectionMode}");
            
            // 切换到2D视角（俯视图）
            SwitchTo2DView();
            
            System.Diagnostics.Debug.WriteLine($"2D视图模式: is2DViewMode={is2DViewMode}");
            
            // 计算所有点在2D屏幕下的坐标
            Calculate2DCoordinates();
            
            // 更新菜单状态
            UpdateMenuState();
            
            // 更新窗口标题
            Text = "点云查看器 - 范围选择模式 (原始数据XYZ坐标)";
            
            System.Diagnostics.Debug.WriteLine("强制刷新显示");
            gl.Invalidate();
            
            MessageBox.Show(
                "范围选择模式已激活！\n\n" +
                "操作说明：\n" +
                "• 在2D视图中点击鼠标左键选择范围角点\n" +
                "• 至少需要选择3个点形成范围\n" +
                "• 点击鼠标右键完成选择并应用过滤\n" +
                "• 按ESC键或菜单退出范围选择模式",
                "范围选择模式",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        
        /// <summary>
        /// 退出范围选择模式
        /// </summary>
        private void ExitRangeSelectionMode()
        {
            if (!isRangeSelectionMode) return;
            
            System.Diagnostics.Debug.WriteLine("退出范围选择模式...");
            
            // 恢复视角状态
            pan = savedCameraState_pan;
            yaw = savedCameraState_yaw;
            pitch = savedCameraState_pitch;
            distance = savedCameraState_distance;
            
            // 退出模式
            isRangeSelectionMode = false;
            is2DViewMode = false;
            selectedPoints.Clear();
            
            // 恢复透视投影
            SetupProjection();
            
            // 更新菜单状态
            UpdateMenuState();
            
            // 恢复窗口标题
            Text = "点云查看器 - 支持PLY/TXT格式";
            
            gl.Invalidate();
        }
        
        /// <summary>
        /// 重新加载原始文件
        /// </summary>
        private void ReloadOriginalFile()
        {
            if (string.IsNullOrEmpty(currentPLYFilePath))
            {
                MessageBox.Show("没有可重新加载的文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            try
            {
                System.Diagnostics.Debug.WriteLine("重新加载原始文件...");
                TryLoadPointCloud(currentPLYFilePath);
                MessageBox.Show("原始文件重新加载完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新加载文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 更新菜单状态
        /// </summary>
        private void UpdateMenuState()
        {
            foreach (Control control in this.Controls)
            {
                if (control is MenuStrip menuStrip)
                {
                    foreach (ToolStripMenuItem topItem in menuStrip.Items)
                    {
                        foreach (ToolStripItem item in topItem.DropDownItems)
                        {
                            if (item.Tag?.ToString() == "RangeSelection")
                            {
                                item.Enabled = !isRangeSelectionMode;
                            }
                            else if (item.Tag?.ToString() == "ExitRangeSelection")
                            {
                                item.Enabled = isRangeSelectionMode;
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 切换到2D俯视图视角（真正的2D平面视图）
        /// </summary>
        private void SwitchTo2DView()
        {
            is2DViewMode = true;
            
            // 设置为完全的2D俯视图模式
            yaw = 0f;      
            pitch = -90f;  // 垂直向下看
            pointCloudYaw = 0f;    // 重置点云旋转
            pointCloudPitch = 0f;  // 重置点云旋转
            
            // 设置合适的距离和位置，确保能看到整个点云的XY平面投影
            // 考虑objectScale的影响来设置合理的distance
            float maxDimension = Math.Max(boundingBoxSize.X, boundingBoxSize.Y);
            distance = maxDimension * objectScale * globalScale * 2.0f;
            
            System.Diagnostics.Debug.WriteLine($"2D模式设置: maxDimension={maxDimension:F2}, objectScale={objectScale:F6}, distance={distance:F2}");
            pan = Vector2.Zero;
            
            // 重新设置投影矩阵为正交投影
            SetupProjection();
            
            System.Diagnostics.Debug.WriteLine("切换到2D俯视图模式（真正的平面俯视图）");
        }
        
        /// <summary>
        /// 计算所有点云在2D屏幕下的坐标（正交投影模式）
        /// </summary>
        private void Calculate2DCoordinates()
        {
            point2DCoords.Clear();
            
            // 获取视口信息
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            
            float screenCenterX = viewport[2] / 2.0f;
            float screenCenterY = viewport[3] / 2.0f;
            float aspect = viewport[2] / (float)viewport[3];
            
            // 在正交投影模式下的坐标转换
            float viewSize = distance * 0.5f;
            
            foreach (var point3D in originalPoints)
            {
                // 计算点在世界坐标中相对于中心的位置
                float worldX = (point3D.X - center.X - pan.X) * objectScale * globalScale;
                float worldY = (point3D.Y - center.Y - pan.Y) * objectScale * globalScale;
                
                // 在正交投影下，世界坐标直接映射到屏幕坐标
                float normalizedX = worldX / (viewSize * aspect);
                float normalizedY = worldY / viewSize;
                
                // 转换到屏幕像素坐标
                float screenX = screenCenterX + normalizedX * screenCenterX;
                float screenY = screenCenterY - normalizedY * screenCenterY; // Y轴翻转
                
                point2DCoords.Add(new Vector2(screenX, screenY));
            }
            
            System.Diagnostics.Debug.WriteLine($"2D坐标计算完成: {point2DCoords.Count:N0} 个点");
            System.Diagnostics.Debug.WriteLine($"屏幕尺寸: {viewport[2]} x {viewport[3]}, 视图大小: {viewSize:F2}");
        }
        
        /// <summary>
        /// 处理范围选择模式下的鼠标按下事件
        /// </summary>
        private void HandleRangeSelectionMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 首先检查是否点击了现有的图标
                int clickedIconIndex = GetClickedIconIndex(new Point(e.X, e.Y));
                
                if (clickedIconIndex >= 0)
                {
                    // 点击了现有图标，开始拖动
                    selectedIconIndex = clickedIconIndex;
                    isDraggingIcon = true;
                    System.Diagnostics.Debug.WriteLine($"开始拖动图标 {clickedIconIndex}");
                }
                else
                {
                    // 没有点击图标，添加新的选择点
                    Vector2 worldPoint = ScreenToWorld2D_Orthographic(new Point(e.X, e.Y));
                    selectedPoints.Add(worldPoint);
                    
                    System.Diagnostics.Debug.WriteLine($"添加选择点: 屏幕({e.X}, {e.Y}) -> 世界({worldPoint.X:F2}, {worldPoint.Y:F2}) - 总计 {selectedPoints.Count} 个点");
                    
                    // 验证坐标转换的准确性
                    VerifyCoordinateConversion(new Point(e.X, e.Y), worldPoint);
                    
                    System.Diagnostics.Debug.WriteLine($"当前选择点列表:");
                    for (int i = 0; i < selectedPoints.Count; i++)
                    {
                        System.Diagnostics.Debug.WriteLine($"  点{i}: ({selectedPoints[i].X:F2}, {selectedPoints[i].Y:F2})");
                    }
                    
                    // 显示一些附近的点云数据进行比较
                    ShowNearbyPointCloudData(worldPoint);
                }
                
                gl.Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右键完成选择
                CompleteRangeSelection();
            }
        }
        
        /// <summary>
        /// 将屏幕坐标转换为世界坐标（正交投影模式）
        /// </summary>
        private Vector2 ScreenToWorld2D_Orthographic(Point screenPoint)
        {
            // 获取视口信息
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            
            float screenCenterX = viewport[2] / 2.0f;
            float screenCenterY = viewport[3] / 2.0f;
            float aspect = viewport[2] / (float)viewport[3];
            
            // 计算屏幕相对于中心的偏移（归一化到[-1, 1]）
            float normalizedX = (screenPoint.X - screenCenterX) / screenCenterX;
            float normalizedY = (screenCenterY - screenPoint.Y) / screenCenterY; // Y轴翻转
            
            // 在正交投影下，计算对应的世界坐标
            // 这里的viewSize应该与SetupProjection中的计算保持一致
            float viewSize = distance * 0.5f;
            
            // 直接映射到世界坐标空间
            float worldOffsetX = normalizedX * viewSize * aspect;
            float worldOffsetY = normalizedY * viewSize;
            
            float worldX = worldOffsetX + center.X;
            float worldY = worldOffsetY + center.Y;
            
            System.Diagnostics.Debug.WriteLine($"正交投影坐标转换: 屏幕({screenPoint.X}, {screenPoint.Y}) -> 世界({worldX:F2}, {worldY:F2})");
            System.Diagnostics.Debug.WriteLine($"  归一化: ({normalizedX:F4}, {normalizedY:F4}), 视图大小: {viewSize:F2}");
            System.Diagnostics.Debug.WriteLine($"  中心: ({center.X:F2}, {center.Y:F2}), 视口: {viewport[2]}x{viewport[3]}");
            System.Diagnostics.Debug.WriteLine($"  缩放: objectScale={objectScale:F6}, globalScale={globalScale:F2}, 总缩放={objectScale * globalScale:F6}");
            
            return new Vector2(worldX, worldY);
        }
        
        /// <summary>
        /// 将世界坐标转换为屏幕坐标（正交投影模式）
        /// </summary>
        private Point WorldToScreen2D_Orthographic(Vector2 worldPoint)
        {
            // 获取视口信息
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            
            float screenCenterX = viewport[2] / 2.0f;
            float screenCenterY = viewport[3] / 2.0f;
            float aspect = viewport[2] / (float)viewport[3];
            
            // 计算世界坐标相对于视图中心的偏移
            float relativeWorldX = worldPoint.X - center.X;
            float relativeWorldY = worldPoint.Y - center.Y;
            
            // 在正交投影下，转换为归一化坐标
            float viewSize = distance * 0.5f;
            float normalizedX = relativeWorldX / (viewSize * aspect);
            float normalizedY = relativeWorldY / viewSize;
            
            // 转换到屏幕像素坐标
            int screenX = (int)(screenCenterX + normalizedX * screenCenterX);
            int screenY = (int)(screenCenterY - normalizedY * screenCenterY); // Y轴翻转
            
            return new Point(screenX, screenY);
        }
        
        /// <summary>
        /// 检查点击的屏幕坐标是否在某个图标上
        /// </summary>
        private int GetClickedIconIndex(Point screenPoint)
        {
            for (int i = 0; i < selectedPoints.Count; i++)
            {
                Point iconScreenPos = WorldToScreen2D_Orthographic(selectedPoints[i]);
                
                // 检查点击位置是否在图标范围内
                float dx = screenPoint.X - iconScreenPos.X;
                float dy = screenPoint.Y - iconScreenPos.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (distance <= iconSize)
                {
                    return i;
                }
            }
            
            return -1; // 没有点击到任何图标
        }
        
        /// <summary>
        /// 验证坐标转换的准确性
        /// </summary>
        private void VerifyCoordinateConversion(Point screenPoint, Vector2 worldPoint)
        {
            // 将世界坐标转换回屏幕坐标，看是否一致
            Point backToScreen = WorldToScreen2D_Orthographic(worldPoint);
            
            float screenDiff = (float)Math.Sqrt(
                Math.Pow(screenPoint.X - backToScreen.X, 2) + 
                Math.Pow(screenPoint.Y - backToScreen.Y, 2));
                
            System.Diagnostics.Debug.WriteLine($"坐标转换验证:");
            System.Diagnostics.Debug.WriteLine($"  原始屏幕: ({screenPoint.X}, {screenPoint.Y})");
            System.Diagnostics.Debug.WriteLine($"  转换世界: ({worldPoint.X:F2}, {worldPoint.Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  回转屏幕: ({backToScreen.X}, {backToScreen.Y})");
            System.Diagnostics.Debug.WriteLine($"  屏幕误差: {screenDiff:F2} 像素");
        }
        
        /// <summary>
        /// 显示点击位置附近的点云数据
        /// </summary>
        private void ShowNearbyPointCloudData(Vector2 worldPoint)
        {
            if (originalPoints == null || originalPoints.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("没有点云数据可供比较");
                return;
            }
            
            // 找到距离点击位置最近的几个点云数据
            var nearbyPoints = originalPoints
                .Select((point, index) => new { 
                    Point = point, 
                    Index = index,
                    Distance = Math.Sqrt(Math.Pow(point.X - worldPoint.X, 2) + Math.Pow(point.Y - worldPoint.Y, 2))
                })
                .OrderBy(x => x.Distance)
                .Take(5)
                .ToList();
                
            System.Diagnostics.Debug.WriteLine($"点击位置({worldPoint.X:F2}, {worldPoint.Y:F2})附近的点云数据:");
            foreach (var item in nearbyPoints)
            {
                System.Diagnostics.Debug.WriteLine($"  点云{item.Index}: ({item.Point.X:F2}, {item.Point.Y:F2}, {item.Point.Z:F2}) 距离={item.Distance:F2}");
            }
            
            // 显示点云数据的整体范围
            var minX = originalPoints.Min(p => p.X);
            var maxX = originalPoints.Max(p => p.X);
            var minY = originalPoints.Min(p => p.Y);
            var maxY = originalPoints.Max(p => p.Y);
            
            System.Diagnostics.Debug.WriteLine($"点云数据范围: X[{minX:F2}, {maxX:F2}], Y[{minY:F2}, {maxY:F2}]");
            System.Diagnostics.Debug.WriteLine($"点击位置是否在范围内: X={worldPoint.X >= minX && worldPoint.X <= maxX}, Y={worldPoint.Y >= minY && worldPoint.Y <= maxY}");
        }
        
        /// <summary>
        /// 为过滤后的数据重置相机参数，确保居中显示
        /// </summary>
        private void ResetCameraForFilteredData()
        {
            // 重置相机角度到默认视角
            yaw = 0f;
            pitch = -20f;
            pointCloudYaw = 0f;
            pointCloudPitch = 0f;
            
            // 重置平移
            pan = Vector2.Zero;
            
            // 重置全局缩放
            globalScale = 1.0f;
            
            // 确保退出2D模式
            is2DViewMode = false;
            
            // 重新设置投影矩阵
            SetupProjection();
            
            System.Diagnostics.Debug.WriteLine("相机参数已重置:");
            System.Diagnostics.Debug.WriteLine($"  Yaw: {yaw:F2}, Pitch: {pitch:F2}");
            System.Diagnostics.Debug.WriteLine($"  Distance: {distance:F2}");
            System.Diagnostics.Debug.WriteLine($"  GlobalScale: {globalScale:F2}");
            System.Diagnostics.Debug.WriteLine($"  Pan: ({pan.X:F2}, {pan.Y:F2})");
        }
        
        /// <summary>
        /// 确保过滤后的点云可见且大小合适
        /// </summary>
        private void EnsurePointCloudVisible()
        {
            if (points == null || points.Count == 0)
                return;
                
            // 计算过滤后点云的包围盒
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            var minZ = points.Min(p => p.Z);
            var maxZ = points.Max(p => p.Z);
            
            var filteredSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            var maxDimension = Math.Max(Math.Max(filteredSize.X, filteredSize.Y), filteredSize.Z);
            
            // 保存当前的objectScale用于比较
            float originalObjectScale = objectScale;
            
            // 重新计算合适的objectScale，但要保持合理的拖动灵敏度
            float targetScreenSize = 5.0f;
            float newObjectScale = targetScreenSize / Math.Max(maxDimension, 0.1f);
            
            // 如果新的缩放过大（会导致拖动过于敏感），则限制它
            float maxReasonableScale = originalObjectScale * 10.0f; // 最多放大10倍
            if (newObjectScale > maxReasonableScale && originalObjectScale > 0)
            {
                objectScale = maxReasonableScale;
                System.Diagnostics.Debug.WriteLine($"限制objectScale从 {newObjectScale:F6} 到 {objectScale:F6} 以保持合理的拖动灵敏度");
            }
            else
            {
                objectScale = newObjectScale;
            }
            
            // 调整相机距离以获得合适的视野
            distance = Math.Max(maxDimension * 1.5f, 10.0f);
            
            // 增加点的大小以确保可见性
            pointSize = Math.Max(3.0f, Math.Min(10.0f, maxDimension * objectScale * 0.1f));
            
            // 重置全局缩放
            globalScale = 1.0f;
            
            // 强制刷新投影矩阵
            SetupProjection();
            
            System.Diagnostics.Debug.WriteLine($"调整显示参数:");
            System.Diagnostics.Debug.WriteLine($"  过滤后包围盒: {filteredSize.X:F2} x {filteredSize.Y:F2} x {filteredSize.Z:F2}");
            System.Diagnostics.Debug.WriteLine($"  最大维度: {maxDimension:F2}");
            System.Diagnostics.Debug.WriteLine($"  原始缩放: {originalObjectScale:F6}");
            System.Diagnostics.Debug.WriteLine($"  最终缩放: {objectScale:F6}");
            System.Diagnostics.Debug.WriteLine($"  调整后距离: {distance:F2}");
            System.Diagnostics.Debug.WriteLine($"  点大小: {pointSize:F2}");
            System.Diagnostics.Debug.WriteLine($"  中心位置: ({center.X:F2}, {center.Y:F2}, {center.Z:F2})");
        }
        
        /// <summary>
        /// 保存过滤后的点云数据到文件供检查
        /// </summary>
        private void SaveFilteredPointCloudForInspection(List<Vector3> filteredPoints)
        {
            try
            {
                string outputPath = Path.Combine(Path.GetDirectoryName(currentPLYFilePath) ?? ".", 
                    $"filtered_points_{DateTime.Now:yyyyMMdd_HHmmss}.ply");
                
                using (StreamWriter writer = new StreamWriter(outputPath))
                {
                    // PLY文件头
                    writer.WriteLine("ply");
                    writer.WriteLine("format ascii 1.0");
                    writer.WriteLine($"element vertex {filteredPoints.Count}");
                    writer.WriteLine("property float x");
                    writer.WriteLine("property float y");
                    writer.WriteLine("property float z");
                    writer.WriteLine("end_header");
                    
                    // 点云数据
                    foreach (var point in filteredPoints)
                    {
                        writer.WriteLine($"{point.X:F6} {point.Y:F6} {point.Z:F6}");
                    }
                }
                
                // 同时保存一个文本文件供直接查看
                string txtPath = Path.ChangeExtension(outputPath, ".txt");
                using (StreamWriter writer = new StreamWriter(txtPath))
                {
                    writer.WriteLine($"过滤后的点云数据 - 共 {filteredPoints.Count} 个点");
                    writer.WriteLine($"时间: {DateTime.Now}");
                    writer.WriteLine($"原始文件: {currentPLYFilePath}");
                    writer.WriteLine();
                    
                    // 数据范围统计
                    if (filteredPoints.Count > 0)
                    {
                        var minX = filteredPoints.Min(p => p.X);
                        var maxX = filteredPoints.Max(p => p.X);
                        var minY = filteredPoints.Min(p => p.Y);
                        var maxY = filteredPoints.Max(p => p.Y);
                        var minZ = filteredPoints.Min(p => p.Z);
                        var maxZ = filteredPoints.Max(p => p.Z);
                        
                        writer.WriteLine("数据范围:");
                        writer.WriteLine($"  X: [{minX:F6}, {maxX:F6}] 跨度: {maxX - minX:F6}");
                        writer.WriteLine($"  Y: [{minY:F6}, {maxY:F6}] 跨度: {maxY - minY:F6}");
                        writer.WriteLine($"  Z: [{minZ:F6}, {maxZ:F6}] 跨度: {maxZ - minZ:F6}");
                        writer.WriteLine();
                        
                        writer.WriteLine("中心位置:");
                        writer.WriteLine($"  X: {(minX + maxX) / 2:F6}");
                        writer.WriteLine($"  Y: {(minY + maxY) / 2:F6}");
                        writer.WriteLine($"  Z: {(minZ + maxZ) / 2:F6}");
                        writer.WriteLine();
                    }
                    
                    writer.WriteLine("前50个点的坐标:");
                    writer.WriteLine("索引\tX\t\tY\t\tZ");
                    for (int i = 0; i < Math.Min(50, filteredPoints.Count); i++)
                    {
                        var p = filteredPoints[i];
                        writer.WriteLine($"{i}\t{p.X:F6}\t{p.Y:F6}\t{p.Z:F6}");
                    }
                    
                    if (filteredPoints.Count > 50)
                    {
                        writer.WriteLine();
                        writer.WriteLine("最后10个点的坐标:");
                        for (int i = Math.Max(0, filteredPoints.Count - 10); i < filteredPoints.Count; i++)
                        {
                            var p = filteredPoints[i];
                            writer.WriteLine($"{i}\t{p.X:F6}\t{p.Y:F6}\t{p.Z:F6}");
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"过滤后数据已保存:");
                System.Diagnostics.Debug.WriteLine($"  PLY文件: {outputPath}");
                System.Diagnostics.Debug.WriteLine($"  文本文件: {txtPath}");
                
                // 可选：显示保存位置给用户
                // MessageBox.Show($"过滤后的数据已保存到:\n{txtPath}", "数据已保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存过滤后数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 将屏幕坐标转换为2D世界坐标（纯2D平面映射）
        /// </summary>
        private Vector2 ScreenToWorld2D(Point screenPoint)
        {
            // 获取视口信息
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            
            // 计算屏幕坐标相对于视口中心的偏移
            float screenCenterX = viewport[2] / 2.0f;
            float screenCenterY = viewport[3] / 2.0f;
            
            float offsetX = screenPoint.X - screenCenterX;
            float offsetY = screenCenterY - screenPoint.Y; // Y轴翻转
            
            // 计算当前视图在世界坐标中的实际像素比例
            // 在2D俯视图模式下，1个屏幕像素对应多少世界坐标单位
            float pixelToWorldX = distance / (objectScale * globalScale * screenCenterX);
            float pixelToWorldY = distance / (objectScale * globalScale * screenCenterY);
            
            // 将屏幕偏移转换为世界坐标偏移
            float worldOffsetX = offsetX * pixelToWorldX;
            float worldOffsetY = offsetY * pixelToWorldY;
            
            // 计算最终的世界坐标（相对于视图中心加上偏移）
            float worldX = center.X + pan.X + worldOffsetX;
            float worldY = center.Y + pan.Y + worldOffsetY;
            
            System.Diagnostics.Debug.WriteLine($"屏幕坐标 ({screenPoint.X}, {screenPoint.Y}) -> 世界坐标 ({worldX:F2}, {worldY:F2})");
            System.Diagnostics.Debug.WriteLine($"  屏幕中心: ({screenCenterX:F1}, {screenCenterY:F1})");
            System.Diagnostics.Debug.WriteLine($"  屏幕偏移: ({offsetX:F1}, {offsetY:F1})");
            System.Diagnostics.Debug.WriteLine($"  像素比例: ({pixelToWorldX:F6}, {pixelToWorldY:F6})");
            System.Diagnostics.Debug.WriteLine($"  世界偏移: ({worldOffsetX:F2}, {worldOffsetY:F2})");
            System.Diagnostics.Debug.WriteLine($"  视图中心: ({center.X + pan.X:F2}, {center.Y + pan.Y:F2})");
            
            return new Vector2(worldX, worldY);
        }
        
        /// <summary>
        /// 完成范围选择并应用过滤
        /// </summary>
        private void CompleteRangeSelection()
        {
            if (selectedPoints.Count < 3)
            {
                MessageBox.Show("至少需要选择3个点来形成有效范围", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            var result = MessageBox.Show(
                $"已选择 {selectedPoints.Count} 个点，是否应用范围过滤？\n\n" +
                "确定：应用过滤并显示范围内的点云\n" +
                "取消：继续选择更多点",
                "确认范围选择",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
                
            if (result == DialogResult.OK)
            {
                ApplyRangeFilter();
            }
        }
        
        /// <summary>
        /// 应用范围过滤
        /// </summary>
        private void ApplyRangeFilter()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始应用范围过滤...");
                
                // 使用世界坐标进行过滤
                var filteredPoints = FilterPointsInWorld2D(originalPoints, selectedPoints);
                
                System.Diagnostics.Debug.WriteLine($"过滤结果: {originalPoints.Count:N0} -> {filteredPoints.Count:N0} 个点");
                
                if (filteredPoints.Count == 0)
                {
                    MessageBox.Show("选择的范围内没有点云数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // 直接使用过滤后的原始数据，不应用任何优化或坐标映射
                int originalCount = originalPoints.Count;
                
                System.Diagnostics.Debug.WriteLine($"过滤前点云数量: {originalCount:N0}");
                System.Diagnostics.Debug.WriteLine($"过滤后点云数量: {filteredPoints.Count:N0}");
                
                // 显示过滤后的点云范围
                if (filteredPoints.Count > 0)
                {
                    var filteredMinX = filteredPoints.Min(p => p.X);
                    var filteredMaxX = filteredPoints.Max(p => p.X);
                    var filteredMinY = filteredPoints.Min(p => p.Y);
                    var filteredMaxY = filteredPoints.Max(p => p.Y);
                    System.Diagnostics.Debug.WriteLine($"过滤后数据范围: X[{filteredMinX:F2}, {filteredMaxX:F2}], Y[{filteredMinY:F2}, {filteredMaxY:F2}]");
                }
                
                points = filteredPoints;
                originalPoints = new List<Vector3>(filteredPoints);
                hasColors = false;
                
                // 重新计算中心和缩放（基于过滤后的数据）
                ComputeCenterAndScale(out center, out objectScale, out distance);
                
                // 重置相机参数以确保过滤后的点云居中显示
                ResetCameraForFilteredData();
                
                // 确保点云大小合适且可见
                EnsurePointCloudVisible();
                
                System.Diagnostics.Debug.WriteLine($"更新后的中心: ({center.X:F2}, {center.Y:F2}, {center.Z:F2})");
                System.Diagnostics.Debug.WriteLine($"更新后的缩放: {objectScale:F6}");
                System.Diagnostics.Debug.WriteLine($"更新后的距离: {distance:F2}");
                
                // 更新窗体标题显示点云信息
                this.Text = $"点云查看器 - 已过滤 ({filteredPoints.Count:N0} 个点)";
                
                // 保存过滤后的数据到文件供检查
                SaveFilteredPointCloudForInspection(filteredPoints);
                
                // 退出范围选择模式
                ExitRangeSelectionMode();
                
                // 强制刷新显示多次以确保显示正确
                gl.Invalidate();
                Application.DoEvents(); // 处理UI事件
                gl.Refresh();
                
                MessageBox.Show($"范围过滤完成！\n\n原始点数: {originalCount:N0}\n过滤后: {filteredPoints.Count:N0}", 
                    "过滤完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                // 再次强制刷新
                gl.Invalidate();
                gl.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用范围过滤失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"范围过滤错误: {ex}");
            }
        }
        
        /// <summary>
        /// 在世界坐标系下过滤多边形内的点
        /// </summary>
        private List<Vector3> FilterPointsInWorld2D(List<Vector3> points3D, List<Vector2> worldPolygon)
        {
            if (worldPolygon.Count < 3) return new List<Vector3>();
            
            // 打印选择的世界坐标多边形信息
            System.Diagnostics.Debug.WriteLine($"世界坐标多边形顶点数: {worldPolygon.Count}");
            for (int i = 0; i < worldPolygon.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"  世界顶点{i}: ({worldPolygon[i].X:F2}, {worldPolygon[i].Y:F2})");
            }
            
            // 计算世界坐标多边形的边界框
            float minX = worldPolygon.Min(p => p.X);
            float maxX = worldPolygon.Max(p => p.X);
            float minY = worldPolygon.Min(p => p.Y);
            float maxY = worldPolygon.Max(p => p.Y);
            System.Diagnostics.Debug.WriteLine($"世界坐标多边形边界框: X[{minX:F2}, {maxX:F2}], Y[{minY:F2}, {maxY:F2}]");
            
            var filteredPoints = new List<Vector3>();
            int insideCount = 0;
            int totalChecked = 0;
            
            foreach (var point3D in points3D)
            {
                totalChecked++;
                
                // 直接使用3D点的XY坐标与世界坐标多边形比较
                Vector2 pointXY = new Vector2(point3D.X, point3D.Y);
                
                if (IsPointInPolygon(pointXY, worldPolygon))
                {
                    filteredPoints.Add(point3D);
                    insideCount++;
                    
                    // 打印前几个在多边形内的点进行调试
                    if (insideCount <= 5)
                    {
                        System.Diagnostics.Debug.WriteLine($"  点{insideCount}在世界多边形内: ({point3D.X:F2}, {point3D.Y:F2}, {point3D.Z:F2})");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"世界坐标过滤结果: 检查了{totalChecked:N0}个点，{insideCount:N0}个在多边形内");
            
            return filteredPoints;
        }
        
        /// <summary>
        /// 在2D屏幕坐标下过滤多边形内的点
        /// </summary>
        private List<Vector3> FilterPointsInScreen2D(List<Vector3> points3D, List<Vector2> screenPolygon)
        {
            if (screenPolygon.Count < 3) return new List<Vector3>();
            
            // 打印选择的屏幕多边形信息
            System.Diagnostics.Debug.WriteLine($"选择的屏幕多边形顶点数: {screenPolygon.Count}");
            for (int i = 0; i < screenPolygon.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"  屏幕顶点{i}: ({screenPolygon[i].X:F1}, {screenPolygon[i].Y:F1})");
            }
            
            // 计算屏幕多边形的边界框
            float minX = screenPolygon.Min(p => p.X);
            float maxX = screenPolygon.Max(p => p.X);
            float minY = screenPolygon.Min(p => p.Y);
            float maxY = screenPolygon.Max(p => p.Y);
            System.Diagnostics.Debug.WriteLine($"屏幕多边形边界框: X[{minX:F1}, {maxX:F1}], Y[{minY:F1}, {maxY:F1}]");
            
            var filteredPoints = new List<Vector3>();
            int insideCount = 0;
            int totalChecked = 0;
            
            // 确保2D坐标数量与3D点数量一致
            if (point2DCoords.Count != points3D.Count)
            {
                System.Diagnostics.Debug.WriteLine("警告: 2D坐标数量与3D点数量不一致，重新计算2D坐标");
                Calculate2DCoordinates();
            }
            
            for (int i = 0; i < points3D.Count; i++)
            {
                totalChecked++;
                Vector2 point2D = point2DCoords[i];
                
                if (IsPointInPolygon(point2D, screenPolygon))
                {
                    filteredPoints.Add(points3D[i]);
                    insideCount++;
                    
                    // 打印前几个在多边形内的点进行调试
                    if (insideCount <= 5)
                    {
                        System.Diagnostics.Debug.WriteLine($"  点{insideCount}在多边形内: 3D({points3D[i].X:F2}, {points3D[i].Y:F2}, {points3D[i].Z:F2}) 屏幕({point2D.X:F1}, {point2D.Y:F1})");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"过滤结果: 检查了{totalChecked:N0}个点，{insideCount:N0}个在屏幕多边形内");
            
            return filteredPoints;
        }
        
        /// <summary>
        /// 判断点是否在多边形内（射线法）
        /// </summary>
        private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;
            
            for (int i = 0; i < polygon.Count; i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
                j = i;
            }
            
            return inside;
        }
        
        /// <summary>
        /// 绘制范围选择可视化
        /// </summary>
        private void DrawRangeSelection()
        {
            if (selectedPoints.Count == 0) 
            {
                System.Diagnostics.Debug.WriteLine("DrawRangeSelection: 没有选择点");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"DrawRangeSelection: 绘制 {selectedPoints.Count} 个选择点");
            
            // 保存当前状态
            GL.PushMatrix();
            
            // 重置变换矩阵，直接在世界坐标中绘制
            GL.LoadIdentity();
            GL.Translate(pan.X, pan.Y, -distance);
            GL.Translate(-center.X, -center.Y, -center.Z);
            GL.Rotate(pointCloudPitch, 1, 0, 0);
            GL.Rotate(pointCloudYaw, 0, 1, 0);
            GL.Scale(objectScale * globalScale, objectScale * globalScale, objectScale * globalScale);
            
            // 禁用深度测试，确保选择框总是可见
            GL.Disable(EnableCap.DepthTest);
            
            // 先绘制一个测试图标在(0,0,0)位置
            GL.Color3(0.0f, 0.0f, 1.0f); // 蓝色测试图标
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(-1.0f, -1.0f, 0.1f);
            GL.Vertex3(1.0f, -1.0f, 0.1f);
            GL.Vertex3(1.0f, 1.0f, 0.1f);
            GL.Vertex3(-1.0f, 1.0f, 0.1f);
            GL.End();
            System.Diagnostics.Debug.WriteLine("绘制了蓝色测试图标在原点");
            
            // 绘制可拖动的图标（用小方块代替点）
            GL.Disable(EnableCap.DepthTest);
            for (int i = 0; i < selectedPoints.Count; i++)
            {
                var point = selectedPoints[i];
                
                // 根据是否被选中设置颜色
                if (i == selectedIconIndex)
                {
                    GL.Color3(1.0f, 1.0f, 0.0f); // 选中时为黄色
                }
                else
                {
                    GL.Color3(1.0f, 0.0f, 0.0f); // 正常时为红色
                }
                
                // 绘制图标（小方块）
                float iconWorldSize = 0.5f; // 使用固定的世界坐标尺寸
                
                System.Diagnostics.Debug.WriteLine($"绘制图标 {i}: 世界坐标({point.X:F2}, {point.Y:F2}), 尺寸={iconWorldSize:F3}");
                
                GL.Begin(PrimitiveType.Quads);
                GL.Vertex3(point.X - iconWorldSize, point.Y - iconWorldSize, 0.1f);
                GL.Vertex3(point.X + iconWorldSize, point.Y - iconWorldSize, 0.1f);
                GL.Vertex3(point.X + iconWorldSize, point.Y + iconWorldSize, 0.1f);
                GL.Vertex3(point.X - iconWorldSize, point.Y + iconWorldSize, 0.1f);
                GL.End();
                
                // 绘制图标边框
                GL.Color3(0.0f, 0.0f, 0.0f); // 黑色边框
                GL.LineWidth(1.0f);
                GL.Begin(PrimitiveType.LineLoop);
                GL.Vertex3(point.X - iconWorldSize, point.Y - iconWorldSize, 0.1f);
                GL.Vertex3(point.X + iconWorldSize, point.Y - iconWorldSize, 0.1f);
                GL.Vertex3(point.X + iconWorldSize, point.Y + iconWorldSize, 0.1f);
                GL.Vertex3(point.X - iconWorldSize, point.Y + iconWorldSize, 0.1f);
                GL.End();
                
                // 绘制图标序号
                // (在简单实现中先跳过文字绘制，用颜色区分)
            }
            
            // 绘制连接线（如果有多个点）
            if (selectedPoints.Count > 1)
            {
                GL.LineWidth(2.0f);
                GL.Color3(1.0f, 1.0f, 0.0f); // 黄色
                GL.Begin(PrimitiveType.LineStrip);
                foreach (var point in selectedPoints)
                {
                    GL.Vertex3(point.X, point.Y, 0.0f);
                }
                // 如果有3个或更多点，闭合多边形
                if (selectedPoints.Count >= 3)
                {
                    GL.Vertex3(selectedPoints[0].X, selectedPoints[0].Y, 0.0f);
                }
                GL.End();
            }
            
            // 绘制半透明填充（如果形成了多边形）
            if (selectedPoints.Count >= 3)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Color4(0.0f, 1.0f, 0.0f, 0.3f); // 半透明绿色
                GL.Begin(PrimitiveType.Polygon);
                foreach (var point in selectedPoints)
                {
                    GL.Vertex3(point.X, point.Y, 0.0f);
                }
                GL.End();
                GL.Disable(EnableCap.Blend);
            }
            
            // 恢复状态
            GL.Enable(EnableCap.DepthTest);
            GL.PointSize(pointSize); // 恢复原始点大小
            GL.LineWidth(1.0f);
            GL.PopMatrix();
            
            // 在屏幕上显示提示信息
            DrawRangeSelectionHint();
        }
        
        /// <summary>
        /// 绘制范围选择提示信息
        /// </summary>
        private void DrawRangeSelectionHint()
        {
            // 这里可以添加2D文字提示，但为了简化，我们使用窗口标题显示信息
            // 实际的2D文字渲染需要更复杂的实现
            string hint = $"已选择 {selectedPoints.Count} 个点";
            if (selectedPoints.Count >= 3)
            {
                hint += " - 右键完成选择";
            }
            else
            {
                hint += " - 至少需要3个点";
            }
            
            // 更新窗口标题显示提示
            Text = $"点云查看器 - 范围选择模式 ({hint})";
        }
        
        /// <summary>
        /// 键盘事件处理
        /// </summary>
        private void Gl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && isRangeSelectionMode)
            {
                ExitRangeSelectionMode();
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 开始范围选择模式 - 使用新的2D选择窗口
        /// </summary>
        private void StartRangeSelectionMode_New()
        {
            try
            {
                // 确保有点云数据
                if (originalPoints == null || originalPoints.Count == 0)
                {
                    MessageBox.Show("请先加载点云数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // 确保不处于范围选择模式（重置所有标志位）
                isRangeSelectionMode = false;
                is2DViewMode = false;
                selectedPoints.Clear();
                
                System.Diagnostics.Debug.WriteLine("启动新的2D范围选择窗口");
                
                // 创建并显示2D选择窗口
                using (var rangeWindow = new RangeSelectionWindow(originalPoints))
                {
                    rangeWindow.RangeSelected += OnRangeSelected;
                    
                    if (rangeWindow.ShowDialog(this) == DialogResult.OK)
                    {
                        // 选择完成，过滤结果已在OnRangeSelected中处理
                        System.Diagnostics.Debug.WriteLine("2D范围选择完成");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("2D范围选择已取消");
                    }
                }
                
                // 确保窗口关闭后标志位正确
                isRangeSelectionMode = false;
                is2DViewMode = false;
                
                System.Diagnostics.Debug.WriteLine("2D选择窗口已关闭，标志位已重置");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动范围选择失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"范围选择错误: {ex}");
                
                // 出错时也要重置标志位
                isRangeSelectionMode = false;
                is2DViewMode = false;
            }
        }
        
        /// <summary>
        /// 处理范围选择完成事件
        /// </summary>
        private void OnRangeSelected(object sender, List<Vector2> worldPolygon)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"收到范围选择结果，多边形有{worldPolygon.Count}个顶点");
                
                // 使用选择的多边形过滤点云
                var filteredPoints = FilterPointsInWorldPolygon(originalPoints, worldPolygon);
                
                if (filteredPoints.Count == 0)
                {
                    MessageBox.Show("选择的范围内没有点云数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // 应用过滤结果
                ApplyFilteredPoints(filteredPoints);
                
                MessageBox.Show($"范围过滤完成！\n\n原始点数: {originalPoints.Count:N0}\n过滤后: {filteredPoints.Count:N0}", 
                    "过滤完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用范围过滤失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"范围过滤错误: {ex}");
            }
        }
        
        /// <summary>
        /// 使用多边形过滤点云数据
        /// </summary>
        private List<Vector3> FilterPointsInWorldPolygon(List<Vector3> points, List<Vector2> polygon)
        {
            var filteredPoints = new List<Vector3>();
            
            foreach (var point in points)
            {
                Vector2 point2D = new Vector2(point.X, point.Y);
                if (IsPointInPolygon_New(point2D, polygon))
                {
                    filteredPoints.Add(point);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"多边形过滤: {points.Count} -> {filteredPoints.Count} 个点");
            return filteredPoints;
        }
        
        /// <summary>
        /// 判断点是否在多边形内（射线法）
        /// </summary>
        private bool IsPointInPolygon_New(Vector2 point, List<Vector2> polygon)
        {
            if (polygon.Count < 3) return false;
            
            bool inside = false;
            int j = polygon.Count - 1;
            
            for (int i = 0; i < polygon.Count; i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
                j = i;
            }
            
            return inside;
        }
        
        /// <summary>
        /// 应用过滤后的点云数据
        /// </summary>
        private void ApplyFilteredPoints(List<Vector3> filteredPoints)
        {
            System.Diagnostics.Debug.WriteLine($"应用过滤后的点云数据: {filteredPoints.Count} 个点");
            
            // 更新点云数据
            points = filteredPoints;
            originalPoints = new List<Vector3>(filteredPoints);
            hasColors = false;
            
            // 重新计算中心和缩放
            ComputeCenterAndScale(out center, out objectScale, out distance);
            
            // 重置相机参数到一个合适的默认视角
            yaw = 0f;
            pitch = -20f;
            pointCloudYaw = 0f;
            pointCloudPitch = 0f;
            pan = Vector2.Zero;
            globalScale = 1.0f;
            
            // 确保退出任何特殊模式
            isRangeSelectionMode = false;
            is2DViewMode = false;
            selectedPoints.Clear();
            
            // 设置合适的显示参数
            EnsurePointCloudVisible();
            
            // 强制设置透视投影
            SetupProjection();
            
            // 更新标题
            this.Text = $"点云查看器 - 已过滤 ({filteredPoints.Count:N0} 个点)";
            
            // 保存过滤后的数据
            SaveFilteredPointCloudForInspection(filteredPoints);
            
            // 多次刷新显示确保更新
            gl.Invalidate();
            Application.DoEvents();
            gl.Refresh();
            
            System.Diagnostics.Debug.WriteLine($"过滤后点云应用完成，中心: ({center.X:F2}, {center.Y:F2}, {center.Z:F2}), 缩放: {objectScale:F6}");
        }
    }
}