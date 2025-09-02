using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenTK;

namespace LoadPCDtest
{
    /// <summary>
    /// 2D范围选择窗口 - 使用纯2D绘图进行点云范围选择
    /// </summary>
    public partial class RangeSelectionWindow : Form
    {
        private List<Vector3> pointCloud;  // 原始3D点云数据
        private List<PointF> points2D;     // 转换后的2D点
        private List<PointF> selectedPoints; // 用户选择的多边形顶点
        private Rectangle viewBounds;      // 2D视图边界
        private float scale;               // 缩放因子
        private PointF offset;             // 偏移量
        private bool isSelecting = false;  // 是否正在选择
        private int dragIndex = -1;        // 当前拖拽的点索引
        
        // 缩放和平移相关
        private float zoomFactor = 1.0f;   // 当前缩放倍数
        private PointF panOffset = PointF.Empty; // 平移偏移
        private bool isPanning = false;     // 是否正在平移
        private Point lastPanPoint;        // 上次平移位置
        
        // 绘图参数
        private const int ICON_SIZE = 8;
        private const int POINT_SIZE = 1;
        
        // 绘图画笔和画刷
        private Pen pointPen = new Pen(Color.White, 1);
        private Pen polygonPen = new Pen(Color.Yellow, 2);
        private Pen selectedPen = new Pen(Color.Red, 2);
        private Brush iconBrush = new SolidBrush(Color.Red);
        private Brush selectedIconBrush = new SolidBrush(Color.Yellow);
        private Brush polygonBrush = new SolidBrush(Color.FromArgb(50, Color.Green));
        
        public event EventHandler<List<Vector2>> RangeSelected;
        
        public RangeSelectionWindow(List<Vector3> pointCloudData)
        {
            InitializeComponent();
            this.pointCloud = pointCloudData;
            this.selectedPoints = new List<PointF>();
            
            // 设置窗口属性
            this.Text = "2D范围选择 - 在XY平面上选择点";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;
            
            // 转换3D点云到2D
            ConvertTo2D();
            
            // 绑定事件
            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.MouseWheel += OnMouseWheel;  // 滚轮缩放
            this.Paint += OnPaint;
            this.KeyDown += OnKeyDown;
            this.Resize += OnResize;
            
            // 设置键盘焦点
            this.KeyPreview = true;
            this.Focus();
        }
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);
        }
        
        /// <summary>
        /// 将3D点云转换为2D显示
        /// </summary>
        private void ConvertTo2D()
        {
            if (pointCloud == null || pointCloud.Count == 0)
                return;
                
            points2D = new List<PointF>();
            
            // 计算点云的边界框
            float minX = pointCloud.Min(p => p.X);
            float maxX = pointCloud.Max(p => p.X);
            float minY = pointCloud.Min(p => p.Y);
            float maxY = pointCloud.Max(p => p.Y);
            
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float maxRange = Math.Max(rangeX, rangeY);
            
            // 计算视图区域（留边距）
            int margin = 50;
            viewBounds = new Rectangle(margin, margin, 
                this.ClientSize.Width - 2 * margin, 
                this.ClientSize.Height - 2 * margin);
            
            // 计算缩放和偏移
            scale = Math.Min(viewBounds.Width / rangeX, viewBounds.Height / rangeY) * 0.9f;
            
            float centerWorldX = (minX + maxX) / 2;
            float centerWorldY = (minY + maxY) / 2;
            float centerScreenX = viewBounds.X + viewBounds.Width / 2f;
            float centerScreenY = viewBounds.Y + viewBounds.Height / 2f;
            
            offset = new PointF(
                centerScreenX - centerWorldX * scale,
                centerScreenY + centerWorldY * scale  // Y轴翻转
            );
            
            // 转换所有点
            foreach (var p3d in pointCloud)
            {
                float x = p3d.X * scale + offset.X;
                float y = -p3d.Y * scale + offset.Y;  // Y轴翻转
                points2D.Add(new PointF(x, y));
            }
            
            System.Diagnostics.Debug.WriteLine($"2D转换完成: {pointCloud.Count} 个点");
            System.Diagnostics.Debug.WriteLine($"世界范围: X[{minX:F2}, {maxX:F2}], Y[{minY:F2}, {maxY:F2}]");
            System.Diagnostics.Debug.WriteLine($"缩放: {scale:F6}, 偏移: ({offset.X:F2}, {offset.Y:F2})");
        }
        
        /// <summary>
        /// 将屏幕坐标转换为世界坐标
        /// </summary>
        private Vector2 ScreenToWorld(PointF screenPoint)
        {
            // 考虑缩放和平移
            float adjustedX = (screenPoint.X - panOffset.X) / zoomFactor;
            float adjustedY = (screenPoint.Y - panOffset.Y) / zoomFactor;
            
            float worldX = (adjustedX - offset.X) / scale;
            float worldY = -(adjustedY - offset.Y) / scale;  // Y轴翻转
            return new Vector2(worldX, worldY);
        }
        
        /// <summary>
        /// 将世界坐标转换为屏幕坐标
        /// </summary>
        private PointF WorldToScreen(Vector2 worldPoint)
        {
            float baseX = worldPoint.X * scale + offset.X;
            float baseY = -worldPoint.Y * scale + offset.Y;  // Y轴翻转
            
            // 应用缩放和平移
            float screenX = baseX * zoomFactor + panOffset.X;
            float screenY = baseY * zoomFactor + panOffset.Y;
            
            return new PointF(screenX, screenY);
        }
        
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 检查是否点击了现有的选择点
                dragIndex = GetClickedPointIndex(e.Location);
                
                if (dragIndex >= 0)
                {
                    // 开始拖拽现有点
                    isSelecting = false;
                }
                else
                {
                    // 将屏幕坐标转换为世界坐标进行存储
                    Vector2 worldPoint = ScreenToWorld(e.Location);
                    
                    // 存储世界坐标而不是屏幕坐标
                    selectedPoints.Add(new PointF(worldPoint.X, worldPoint.Y));
                    isSelecting = true;
                    
                    System.Diagnostics.Debug.WriteLine($"添加选择点: 屏幕({e.X}, {e.Y}) -> 世界({worldPoint.X:F2}, {worldPoint.Y:F2})");
                }
                
                this.Invalidate();
            }
            else if (e.Button == MouseButtons.Middle)
            {
                // 中键开始平移
                isPanning = true;
                lastPanPoint = e.Location;
                this.Cursor = Cursors.SizeAll;
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右键完成选择
                CompleteSelection();
            }
        }
        
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (dragIndex >= 0 && e.Button == MouseButtons.Left)
            {
                // 拖拽选择点，更新为世界坐标
                Vector2 worldPoint = ScreenToWorld(e.Location);
                selectedPoints[dragIndex] = new PointF(worldPoint.X, worldPoint.Y);
                
                System.Diagnostics.Debug.WriteLine($"拖拽点{dragIndex}: 屏幕({e.X}, {e.Y}) -> 世界({worldPoint.X:F2}, {worldPoint.Y:F2})");
                
                this.Invalidate();
            }
            else if (isPanning && e.Button == MouseButtons.Middle)
            {
                // 平移视图
                float deltaX = e.X - lastPanPoint.X;
                float deltaY = e.Y - lastPanPoint.Y;
                
                panOffset.X += deltaX;
                panOffset.Y += deltaY;
                
                lastPanPoint = e.Location;
                this.Invalidate();
            }
        }
        
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragIndex = -1;
                isSelecting = false;
            }
            else if (e.Button == MouseButtons.Middle)
            {
                isPanning = false;
                this.Cursor = Cursors.Default;
            }
        }
        
        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            // 滚轮缩放
            float zoomDelta = e.Delta > 0 ? 1.1f : 0.9f;
            float newZoomFactor = zoomFactor * zoomDelta;
            
            // 限制缩放范围
            if (newZoomFactor < 0.1f) newZoomFactor = 0.1f;
            if (newZoomFactor > 10.0f) newZoomFactor = 10.0f;
            
            // 计算缩放中心（鼠标位置）
            PointF mousePos = new PointF(e.X, e.Y);
            
            // 调整平移偏移以实现以鼠标为中心的缩放
            panOffset.X = mousePos.X - (mousePos.X - panOffset.X) * (newZoomFactor / zoomFactor);
            panOffset.Y = mousePos.Y - (mousePos.Y - panOffset.Y) * (newZoomFactor / zoomFactor);
            
            zoomFactor = newZoomFactor;
            
            System.Diagnostics.Debug.WriteLine($"缩放: {zoomFactor:F2}x, 平移: ({panOffset.X:F1}, {panOffset.Y:F1})");
            
            this.Invalidate();
        }
        
        private void OnPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // 不使用Graphics变换，直接在绘制方法中处理坐标
            
            // 绘制背景网格
            DrawGrid(g);
            
            // 绘制点云
            DrawPointCloud(g);
            
            // 绘制选择的多边形
            DrawSelection(g);
            
            // 绘制说明文字
            DrawInstructions(g);
        }
        
        private void DrawGrid(Graphics g)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(30, Color.Gray), 1))
            {
                // 绘制网格线
                int gridSpacing = 50;
                for (int x = viewBounds.Left; x <= viewBounds.Right; x += gridSpacing)
                {
                    g.DrawLine(gridPen, x, viewBounds.Top, x, viewBounds.Bottom);
                }
                for (int y = viewBounds.Top; y <= viewBounds.Bottom; y += gridSpacing)
                {
                    g.DrawLine(gridPen, viewBounds.Left, y, viewBounds.Right, y);
                }
            }
        }
        
        private void DrawPointCloud(Graphics g)
        {
            if (points2D == null) return;
            
            // 计算可见区域，只绘制在视窗内的点以提高性能
            RectangleF visibleRect = new RectangleF(
                -panOffset.X / zoomFactor, 
                -panOffset.Y / zoomFactor,
                this.ClientSize.Width / zoomFactor, 
                this.ClientSize.Height / zoomFactor);
            
            using (Brush pointBrush = new SolidBrush(Color.White))
            {
                int drawnCount = 0;
                foreach (var point in points2D)
                {
                    // 应用缩放和平移变换
                    float screenX = point.X * zoomFactor + panOffset.X;
                    float screenY = point.Y * zoomFactor + panOffset.Y;
                    
                    // 只绘制在屏幕范围内的点
                    if (screenX >= -10 && screenX <= this.ClientSize.Width + 10 &&
                        screenY >= -10 && screenY <= this.ClientSize.Height + 10)
                    {
                        g.FillEllipse(pointBrush, screenX - POINT_SIZE/2, screenY - POINT_SIZE/2, POINT_SIZE, POINT_SIZE);
                        drawnCount++;
                    }
                }
                
                // 限制绘制的点数以避免卡顿
                if (drawnCount > 10000)
                {
                    System.Diagnostics.Debug.WriteLine($"绘制了太多点: {drawnCount}，可能影响性能");
                }
            }
        }
        
        private void DrawSelection(Graphics g)
        {
            if (selectedPoints.Count == 0) return;
            
            // 将世界坐标转换为屏幕坐标进行绘制
            var screenPoints = selectedPoints.Select(wp => {
                float screenX = wp.X * scale * zoomFactor + offset.X * zoomFactor + panOffset.X;
                float screenY = -wp.Y * scale * zoomFactor + offset.Y * zoomFactor + panOffset.Y;
                return new PointF(screenX, screenY);
            }).ToArray();
            
            // 绘制多边形填充
            if (screenPoints.Length >= 3)
            {
                g.FillPolygon(polygonBrush, screenPoints);
            }
            
            // 绘制多边形边框
            if (screenPoints.Length >= 2)
            {
                g.DrawPolygon(polygonPen, screenPoints);
            }
            
            // 绘制选择点图标
            for (int i = 0; i < screenPoints.Length; i++)
            {
                var point = screenPoints[i];
                var brush = (dragIndex == i) ? selectedIconBrush : iconBrush;
                
                // 根据缩放调整图标大小
                float iconSize = ICON_SIZE * Math.Max(0.5f, Math.Min(2.0f, zoomFactor));
                
                g.FillEllipse(brush, point.X - iconSize/2, point.Y - iconSize/2, iconSize, iconSize);
                g.DrawEllipse(Pens.Black, point.X - iconSize/2, point.Y - iconSize/2, iconSize, iconSize);
                
                // 绘制点序号
                using (Font font = new Font("Arial", Math.Max(6, Math.Min(12, (int)(8 * zoomFactor)))))
                {
                    g.DrawString(i.ToString(), font, Brushes.White, point.X + iconSize, point.Y - iconSize);
                }
            }
        }
        
        private void DrawInstructions(Graphics g)
        {
            using (Font font = new Font("Arial", 10))
            using (Brush textBrush = new SolidBrush(Color.Yellow))
            {
                string[] instructions = {
                    "左键: 添加选择点",
                    "拖拽: 移动选择点", 
                    "中键拖拽: 平移视图",
                    "滚轮: 缩放 (0.1x - 10x)",
                    "F/R键: 重置视图",
                    "右键: 完成选择",
                    "ESC: 取消选择",
                    "Delete: 删除最后一个点"
                };
                
                for (int i = 0; i < instructions.Length; i++)
                {
                    g.DrawString(instructions[i], font, textBrush, 10, 10 + i * 18);
                }
                
                // 显示当前状态
                int statusY = 10 + instructions.Length * 18 + 10;
                if (selectedPoints.Count > 0)
                {
                    g.DrawString($"已选择 {selectedPoints.Count} 个点", font, textBrush, 10, statusY);
                    statusY += 18;
                }
                
                // 显示缩放和平移信息
                g.DrawString($"缩放: {zoomFactor:F1}x", font, textBrush, 10, statusY);
                g.DrawString($"平移: ({panOffset.X:F0}, {panOffset.Y:F0})", font, textBrush, 10, statusY + 18);
            }
        }
        
        private int GetClickedPointIndex(Point location)
        {
            for (int i = 0; i < selectedPoints.Count; i++)
            {
                // 将世界坐标转换为屏幕坐标进行点击检测
                var worldPoint = selectedPoints[i];
                float screenX = worldPoint.X * scale * zoomFactor + offset.X * zoomFactor + panOffset.X;
                float screenY = -worldPoint.Y * scale * zoomFactor + offset.Y * zoomFactor + panOffset.Y;
                
                float distance = (float)Math.Sqrt(
                    Math.Pow(location.X - screenX, 2) + 
                    Math.Pow(location.Y - screenY, 2));
                
                // 根据缩放调整点击区域
                float iconSize = ICON_SIZE * Math.Max(0.5f, Math.Min(2.0f, zoomFactor));
                
                if (distance <= iconSize)
                {
                    return i;
                }
            }
            return -1;
        }
        
        private void CompleteSelection()
        {
            if (selectedPoints.Count < 3)
            {
                MessageBox.Show("请至少选择3个点来形成一个多边形区域", "选择不完整", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // selectedPoints已经存储了世界坐标，直接使用
            List<Vector2> worldPolygon = new List<Vector2>();
            foreach (var worldPoint in selectedPoints)
            {
                worldPolygon.Add(new Vector2(worldPoint.X, worldPoint.Y));
            }
            
            System.Diagnostics.Debug.WriteLine($"完成选择，共{worldPolygon.Count}个点:");
            for (int i = 0; i < worldPolygon.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"  点{i}: ({worldPolygon[i].X:F2}, {worldPolygon[i].Y:F2})");
            }
            
            // 触发选择完成事件
            RangeSelected?.Invoke(this, worldPolygon);
            
            // 关闭窗口
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                // ESC取消选择
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
            else if (e.KeyCode == Keys.Delete && selectedPoints.Count > 0)
            {
                // Delete删除最后一个点
                selectedPoints.RemoveAt(selectedPoints.Count - 1);
                this.Invalidate();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // Enter完成选择
                CompleteSelection();
            }
            else if (e.KeyCode == Keys.F)
            {
                // F键适应窗口 (Fit to window)
                ResetView();
            }
            else if (e.KeyCode == Keys.R)
            {
                // R键重置视图
                ResetView();
            }
        }
        
        /// <summary>
        /// 重置视图到初始状态
        /// </summary>
        private void ResetView()
        {
            zoomFactor = 1.0f;
            panOffset = PointF.Empty;
            this.Invalidate();
            System.Diagnostics.Debug.WriteLine("视图已重置");
        }
        
        private void OnResize(object sender, EventArgs e)
        {
            // 窗口大小改变时重新计算2D坐标
            ConvertTo2D();
            this.Invalidate();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pointPen?.Dispose();
                polygonPen?.Dispose();
                selectedPen?.Dispose();
                iconBrush?.Dispose();
                selectedIconBrush?.Dispose();
                polygonBrush?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
