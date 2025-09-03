using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using LoadPCDtest.Analysis;

namespace LoadPCDtest.Rendering
{
    /// <summary>
    /// 墙面渲染器 - 专门用于渲染分离的墙面
    /// </summary>
    public class WallRenderer
    {
        private Dictionary<WallSeparationAnalyzer.WallDirection, bool> wallVisibility;
        private bool showWallLabels = true;
        private float wallPointSize = 3.0f;

        public bool ShowNorthWall { get; set; } = true;
        public bool ShowSouthWall { get; set; } = true;
        public bool ShowEastWall { get; set; } = true;
        public bool ShowWestWall { get; set; } = true;
        public bool ShowHorizontalSurfaces { get; set; } = false;
        public bool ShowWallLabels 
        { 
            get => showWallLabels; 
            set => showWallLabels = value; 
        }

        public float WallPointSize
        {
            get => wallPointSize;
            set => wallPointSize = Math.Max(1.0f, Math.Min(10.0f, value));
        }

        public WallRenderer()
        {
            wallVisibility = new Dictionary<WallSeparationAnalyzer.WallDirection, bool>
            {
                { WallSeparationAnalyzer.WallDirection.North, ShowNorthWall },
                { WallSeparationAnalyzer.WallDirection.South, ShowSouthWall },
                { WallSeparationAnalyzer.WallDirection.East, ShowEastWall },
                { WallSeparationAnalyzer.WallDirection.West, ShowWestWall },
                { WallSeparationAnalyzer.WallDirection.Horizontal, ShowHorizontalSurfaces }
            };
        }

        /// <summary>
        /// 渲染所有墙面
        /// </summary>
        public void RenderWalls(List<WallSeparationAnalyzer.Wall> walls)
        {
            if (walls == null || walls.Count == 0)
                return;

            // 更新可见性状态
            UpdateVisibilityState();

            GL.PointSize(WallPointSize);
            GL.Begin(PrimitiveType.Points);

            foreach (var wall in walls)
            {
                if (IsWallVisible(wall.Direction))
                {
                    GL.Color3(wall.Color);
                    
                    foreach (var point in wall.Points)
                    {
                        GL.Vertex3(point);
                    }
                }
            }

            GL.End();

            // 渲染墙面标签
            if (ShowWallLabels)
            {
                RenderWallLabels(walls);
            }
        }

        /// <summary>
        /// 只渲染指定方向的墙面
        /// </summary>
        public void RenderSpecificWall(WallSeparationAnalyzer.Wall wall)
        {
            if (wall == null || wall.Points.Count == 0)
                return;

            GL.PointSize(WallPointSize);
            GL.Color3(wall.Color);
            GL.Begin(PrimitiveType.Points);

            foreach (var point in wall.Points)
            {
                GL.Vertex3(point);
            }

            GL.End();
        }

        /// <summary>
        /// 渲染墙面边界框
        /// </summary>
        public void RenderWallBoundingBoxes(List<WallSeparationAnalyzer.Wall> walls)
        {
            if (walls == null || walls.Count == 0)
                return;

            GL.LineWidth(2.0f);

            foreach (var wall in walls)
            {
                if (!IsWallVisible(wall.Direction) || wall.Points.Count == 0)
                    continue;

                // 计算边界框
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);

                foreach (var point in wall.Points)
                {
                    if (point.X < min.X) min.X = point.X;
                    if (point.Y < min.Y) min.Y = point.Y;
                    if (point.Z < min.Z) min.Z = point.Z;
                    if (point.X > max.X) max.X = point.X;
                    if (point.Y > max.Y) max.Y = point.Y;
                    if (point.Z > max.Z) max.Z = point.Z;
                }

                // 渲染边界框
                GL.Color3(wall.Color.X * 0.7f, wall.Color.Y * 0.7f, wall.Color.Z * 0.7f);
                RenderBoundingBox(min, max);
            }

            GL.LineWidth(1.0f);
        }

        /// <summary>
        /// 在每个垂直墙面的主平面上渲染“四侧包围”矩形框（线框），用于强调墙体边界
        /// thickness: 线宽近似（像素），inset: 从点云边界向内收缩的距离（米）
        /// </summary>
        public void RenderWallFourSidedBoxes(List<WallSeparationAnalyzer.Wall> walls, float thickness, float inset)
        {
            if (walls == null || walls.Count == 0)
                return;

            float lineWidthPx = Math.Max(1.0f, thickness * 20.0f);
            GL.LineWidth(lineWidthPx);

            foreach (var wall in walls)
            {
                if (!IsWallVisible(wall.Direction) || wall.Points == null || wall.Points.Count == 0)
                    continue;
                if (wall.Direction == WallSeparationAnalyzer.WallDirection.Horizontal)
                    continue;

                // 计算边界
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var p in wall.Points)
                {
                    if (p.X < min.X) min.X = p.X;
                    if (p.Y < min.Y) min.Y = p.Y;
                    if (p.Z < min.Z) min.Z = p.Z;
                    if (p.X > max.X) max.X = p.X;
                    if (p.Y > max.Y) max.Y = p.Y;
                    if (p.Z > max.Z) max.Z = p.Z;
                }

                // 颜色
                GL.Color3(Math.Min(1.0f, wall.Color.X * 1.0f), Math.Min(1.0f, wall.Color.Y * 1.0f), Math.Min(1.0f, wall.Color.Z * 1.0f));

                GL.Begin(PrimitiveType.Lines);

                switch (wall.Direction)
                {
                    case WallSeparationAnalyzer.WallDirection.East:
                    case WallSeparationAnalyzer.WallDirection.West:
                    {
                        float x = wall.Direction == WallSeparationAnalyzer.WallDirection.East ? max.X : min.X;
                        float y1 = min.Y + inset;
                        float y2 = max.Y - inset;
                        float z1 = min.Z + inset;
                        float z2 = max.Z - inset;
                        if (y1 <= y2 && z1 <= z2)
                        {
                            GL.Vertex3(x, y1, z1); GL.Vertex3(x, y2, z1);
                            GL.Vertex3(x, y2, z1); GL.Vertex3(x, y2, z2);
                            GL.Vertex3(x, y2, z2); GL.Vertex3(x, y1, z2);
                            GL.Vertex3(x, y1, z2); GL.Vertex3(x, y1, z1);
                        }
                        break;
                    }
                    case WallSeparationAnalyzer.WallDirection.North:
                    case WallSeparationAnalyzer.WallDirection.South:
                    {
                        float y = wall.Direction == WallSeparationAnalyzer.WallDirection.North ? max.Y : min.Y;
                        float x1 = min.X + inset;
                        float x2 = max.X - inset;
                        float z1 = min.Z + inset;
                        float z2 = max.Z - inset;
                        if (x1 <= x2 && z1 <= z2)
                        {
                            GL.Vertex3(x1, y, z1); GL.Vertex3(x2, y, z1);
                            GL.Vertex3(x2, y, z1); GL.Vertex3(x2, y, z2);
                            GL.Vertex3(x2, y, z2); GL.Vertex3(x1, y, z2);
                            GL.Vertex3(x1, y, z2); GL.Vertex3(x1, y, z1);
                        }
                        break;
                    }
                }

                GL.End();
            }

            GL.LineWidth(1.0f);
        }

        /// <summary>
        /// 渲染墙面统计信息
        /// </summary>
        public void RenderWallStatistics(List<WallSeparationAnalyzer.Wall> walls, Vector3 basePosition)
        {
            if (walls == null || walls.Count == 0)
                return;

            var verticalWalls = walls.FindAll(w => w.Direction != WallSeparationAnalyzer.WallDirection.Horizontal);
            
            // 显示基本统计信息
            GL.Color3(1.0f, 1.0f, 1.0f);
            
            // 这里可以添加文本渲染，但需要引入文本渲染库
            // 暂时通过控制台输出统计信息
            System.Diagnostics.Debug.WriteLine($"当前显示墙面统计:");
            foreach (var wall in verticalWalls)
            {
                if (IsWallVisible(wall.Direction))
                {
                    System.Diagnostics.Debug.WriteLine($"  {wall.Name}: {wall.Points.Count:N0} 个点");
                }
            }
        }

        /// <summary>
        /// 高亮显示特定墙面
        /// </summary>
        public void HighlightWall(WallSeparationAnalyzer.Wall wall, float intensity = 1.5f)
        {
            if (wall == null || wall.Points.Count == 0)
                return;

            GL.PointSize(WallPointSize * intensity);
            var highlightColor = wall.Color * intensity;
            GL.Color3(Math.Min(1.0f, highlightColor.X), 
                     Math.Min(1.0f, highlightColor.Y), 
                     Math.Min(1.0f, highlightColor.Z));

            GL.Begin(PrimitiveType.Points);
            foreach (var point in wall.Points)
            {
                GL.Vertex3(point);
            }
            GL.End();

            GL.PointSize(WallPointSize);
        }

        /// <summary>
        /// 更新可见性状态
        /// </summary>
        private void UpdateVisibilityState()
        {
            wallVisibility[WallSeparationAnalyzer.WallDirection.North] = ShowNorthWall;
            wallVisibility[WallSeparationAnalyzer.WallDirection.South] = ShowSouthWall;
            wallVisibility[WallSeparationAnalyzer.WallDirection.East] = ShowEastWall;
            wallVisibility[WallSeparationAnalyzer.WallDirection.West] = ShowWestWall;
            wallVisibility[WallSeparationAnalyzer.WallDirection.Horizontal] = ShowHorizontalSurfaces;
        }

        /// <summary>
        /// 检查墙面是否可见
        /// </summary>
        private bool IsWallVisible(WallSeparationAnalyzer.WallDirection direction)
        {
            return wallVisibility.TryGetValue(direction, out bool visible) && visible;
        }

        /// <summary>
        /// 渲染墙面标签（简化版本）
        /// </summary>
        private void RenderWallLabels(List<WallSeparationAnalyzer.Wall> walls)
        {
            // 由于WinForms + OpenGL的文本渲染比较复杂，这里暂时省略
            // 可以在后续版本中添加文本渲染功能
        }

        /// <summary>
        /// 渲染边界框
        /// </summary>
        private void RenderBoundingBox(Vector3 min, Vector3 max)
        {
            GL.Begin(PrimitiveType.Lines);

            // 底面
            GL.Vertex3(min.X, min.Y, min.Z);
            GL.Vertex3(max.X, min.Y, min.Z);
            GL.Vertex3(max.X, min.Y, min.Z);
            GL.Vertex3(max.X, max.Y, min.Z);
            GL.Vertex3(max.X, max.Y, min.Z);
            GL.Vertex3(min.X, max.Y, min.Z);
            GL.Vertex3(min.X, max.Y, min.Z);
            GL.Vertex3(min.X, min.Y, min.Z);

            // 顶面
            GL.Vertex3(min.X, min.Y, max.Z);
            GL.Vertex3(max.X, min.Y, max.Z);
            GL.Vertex3(max.X, min.Y, max.Z);
            GL.Vertex3(max.X, max.Y, max.Z);
            GL.Vertex3(max.X, max.Y, max.Z);
            GL.Vertex3(min.X, max.Y, max.Z);
            GL.Vertex3(min.X, max.Y, max.Z);
            GL.Vertex3(min.X, min.Y, max.Z);

            // 垂直边
            GL.Vertex3(min.X, min.Y, min.Z);
            GL.Vertex3(min.X, min.Y, max.Z);
            GL.Vertex3(max.X, min.Y, min.Z);
            GL.Vertex3(max.X, min.Y, max.Z);
            GL.Vertex3(max.X, max.Y, min.Z);
            GL.Vertex3(max.X, max.Y, max.Z);
            GL.Vertex3(min.X, max.Y, min.Z);
            GL.Vertex3(min.X, max.Y, max.Z);

            GL.End();
        }

        /// <summary>
        /// 获取所有可用的墙面方向
        /// </summary>
        public static List<WallSeparationAnalyzer.WallDirection> GetAllWallDirections()
        {
            return new List<WallSeparationAnalyzer.WallDirection>
            {
                WallSeparationAnalyzer.WallDirection.North,
                WallSeparationAnalyzer.WallDirection.South,
                WallSeparationAnalyzer.WallDirection.East,
                WallSeparationAnalyzer.WallDirection.West,
                WallSeparationAnalyzer.WallDirection.Horizontal
            };
        }

        /// <summary>
        /// 切换墙面可见性
        /// </summary>
        public void ToggleWallVisibility(WallSeparationAnalyzer.WallDirection direction)
        {
            switch (direction)
            {
                case WallSeparationAnalyzer.WallDirection.North:
                    ShowNorthWall = !ShowNorthWall;
                    break;
                case WallSeparationAnalyzer.WallDirection.South:
                    ShowSouthWall = !ShowSouthWall;
                    break;
                case WallSeparationAnalyzer.WallDirection.East:
                    ShowEastWall = !ShowEastWall;
                    break;
                case WallSeparationAnalyzer.WallDirection.West:
                    ShowWestWall = !ShowWestWall;
                    break;
                case WallSeparationAnalyzer.WallDirection.Horizontal:
                    ShowHorizontalSurfaces = !ShowHorizontalSurfaces;
                    break;
            }
        }

        /// <summary>
        /// 重置所有墙面可见性
        /// </summary>
        public void ResetWallVisibility()
        {
            ShowNorthWall = true;
            ShowSouthWall = true;
            ShowEastWall = true;
            ShowWestWall = true;
            ShowHorizontalSurfaces = false;
        }
    }
}
