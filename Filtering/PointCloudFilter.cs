using OpenTK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LoadPCDtest.Core;

namespace LoadPCDtest.Filtering
{
    /// <summary>
    /// 点云过滤器
    /// </summary>
    public static class PointCloudFilter
    {
        /// <summary>
        /// 使用多边形过滤点云数据
        /// </summary>
        public static List<Vector3> FilterPointsInPolygon(List<Vector3> points, List<Vector2> polygon)
        {
            var filteredPoints = new List<Vector3>();
            
            foreach (var point in points)
            {
                Vector2 point2D = new Vector2(point.X, point.Y);
                if (IsPointInPolygon(point2D, polygon))
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
        public static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
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
        /// 应用过滤后的点云数据并调整显示参数
        /// </summary>
        public static void ApplyFilteredPoints(PointCloudData data, CameraController camera, List<Vector3> filteredPoints)
        {
            System.Diagnostics.Debug.WriteLine($"应用过滤后的点云数据: {filteredPoints.Count} 个点");
            
            // 更新点云数据
            data.Points = filteredPoints;
            data.OriginalPoints = new List<Vector3>(filteredPoints);
            data.HasColors = false;
            data.PLYPoints.Clear();
            data.OriginalPLYPoints.Clear();
            
            data.Center = new Vector3(0, 0, 0);
            data.ObjectScale = 1.0f;
            
            // 重置相机参数
            camera.ResetForFilteredData();
            

        }

        /// <summary>
        /// 确保过滤后的点云可见且大小合适
        /// </summary>
        private static void EnsurePointCloudVisible(PointCloudData data, float distance)
        {
            if (data.Points == null || data.Points.Count == 0)
                return;
                
            // 计算过滤后点云的包围盒
            var minX = data.Points.Min(p => p.X);
            var maxX = data.Points.Max(p => p.X);
            var minY = data.Points.Min(p => p.Y);
            var maxY = data.Points.Max(p => p.Y);
            var minZ = data.Points.Min(p => p.Z);
            var maxZ = data.Points.Max(p => p.Z);
            
            var filteredSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            var maxDimension = Math.Max(Math.Max(filteredSize.X, filteredSize.Y), filteredSize.Z);
            
            // 保存当前的objectScale用于比较
            float originalObjectScale = data.ObjectScale;
            
            // 重新计算合适的objectScale，但要保持合理的拖动灵敏度
            float targetScreenSize = 5.0f;
            float newObjectScale = targetScreenSize / Math.Max(maxDimension, 0.1f);
            
            // 如果新的缩放过大（会导致拖动过于敏感），则限制它
            float maxReasonableScale = originalObjectScale * 10.0f; // 最多放大10倍
            if (newObjectScale > maxReasonableScale && originalObjectScale > 0)
            {
                data.ObjectScale = maxReasonableScale;
                System.Diagnostics.Debug.WriteLine($"限制objectScale从 {newObjectScale:F6} 到 {data.ObjectScale:F6} 以保持合理的拖动灵敏度");
            }
            else
            {
                data.ObjectScale = newObjectScale;
            }
            
            System.Diagnostics.Debug.WriteLine($"调整显示参数:");
            System.Diagnostics.Debug.WriteLine($"  过滤后包围盒: {filteredSize.X:F2} x {filteredSize.Y:F2} x {filteredSize.Z:F2}");
            System.Diagnostics.Debug.WriteLine($"  最大维度: {maxDimension:F2}");
            System.Diagnostics.Debug.WriteLine($"  原始缩放: {originalObjectScale:F6}");
            System.Diagnostics.Debug.WriteLine($"  最终缩放: {data.ObjectScale:F6}");
        }

        /// <summary>
        /// 保存过滤后的点云数据到文件
        /// </summary>
        public static void SaveFilteredPointCloud(List<Vector3> filteredPoints, string originalFilePath)
        {
            try
            {
                string outputPath = Path.Combine(Path.GetDirectoryName(originalFilePath) ?? ".", 
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
                
                System.Diagnostics.Debug.WriteLine($"过滤后数据已保存到: {outputPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存过滤后数据失败: {ex.Message}");
            }
        }
    }
}
