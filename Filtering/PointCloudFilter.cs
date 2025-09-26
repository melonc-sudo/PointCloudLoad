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
        private static string Format6(double v)
        {
            double t = Math.Truncate(v * 1_000_000.0) / 1_000_000.0;
            return t.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
        }
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
                    writer.WriteLine("property double x");
                    writer.WriteLine("property double y");
                    writer.WriteLine("property double z");
                    writer.WriteLine("end_header");
                    
                    // 点云数据
                    foreach (var point in filteredPoints)
                    {
                        writer.WriteLine(Format6(point.X) + " " + Format6(point.Y) + " " + Format6(point.Z));
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
