using System;
using System.Collections.Generic;
using System.IO;
 
using System.Linq;
using OpenTK;

/// <summary>
/// PLY (Polygon File Format) 加载器 - 简化版
/// </summary>
public class PLYLoader
{
    public class PLYPoint
    {
        public Vector3 Position { get; set; }
        public Vector3 Color { get; set; } = new Vector3(1.0f, 1.0f, 1.0f); // 默认白色
        public float Intensity { get; set; } = 0;
        public bool HasColor { get; set; } = false;
    }
    
    /// <summary>
    /// 加载PLY格式的点云文件（只返回坐标）
    /// </summary>
    public static List<Vector3> LoadPLY(string filePath)
    {
        var points = new List<Vector3>();
        
        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string line;
                bool inHeader = true;
                int vertexCount = 0;
                int currentVertex = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    
                    if (inHeader)
                    {
                        if (line.StartsWith("element vertex"))
                        {
                            var parts = line.Split(' ');
                            if (parts.Length >= 3 && int.TryParse(parts[2], out vertexCount))
                            {
                                System.Diagnostics.Debug.WriteLine($"PLY文件包含 {vertexCount:N0} 个顶点");
                            }
                        }
                        else if (line == "end_header")
                        {
                            inHeader = false;
                        }
                        continue;
                    }

                    // 解析顶点数据
                    if (currentVertex < vertexCount)
                    {
                        var coords = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (coords.Length >= 3)
                        {
                            if (float.TryParse(coords[0], out float x) &&
                                float.TryParse(coords[1], out float y) &&
                                float.TryParse(coords[2], out float z))
                            {
                                points.Add(new Vector3(x, y, z));
                            }
                        }
                        currentVertex++;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"PLY加载完成: {points.Count:N0} 个点");
            return points;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载PLY文件失败: {ex.Message}");
            throw;
        }
    }
    
    
    
    /// <summary>
    /// 加载PLY文件（包含RGB颜色信息）
    /// </summary>
    public static List<PLYPoint> LoadPLYWithColors(string filePath)
    {
        var points = new List<PLYPoint>();
        
        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string line;
                bool inHeader = true;
                int vertexCount = 0;
                int currentVertex = 0;
                bool hasRGB = false;

                // 解析头部信息
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    
                    if (inHeader)
                    {
                        if (line.StartsWith("element vertex"))
                        {
                            var parts = line.Split(' ');
                            if (parts.Length >= 3 && int.TryParse(parts[2], out vertexCount))
                            {
                                System.Diagnostics.Debug.WriteLine($"PLY文件包含 {vertexCount:N0} 个顶点");
                            }
                        }
                        else if (line.Contains("red") || line.Contains("green") || line.Contains("blue"))
                        {
                            hasRGB = true;
                        }
                        else if (line == "end_header")
                        {
                            inHeader = false;
                            System.Diagnostics.Debug.WriteLine($"PLY文件包含RGB颜色: {hasRGB}");
                        }
                        continue;
                    }

                    // 解析顶点数据
                    if (currentVertex < vertexCount)
                    {
                        var coords = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (coords.Length >= 3)
                        {
                            if (float.TryParse(coords[0], out float x) &&
                                float.TryParse(coords[1], out float y) &&
                                float.TryParse(coords[2], out float z))
                            {
                                var point = new PLYPoint();
                                point.Position = new Vector3(x, y, z);
                                
                                // 尝试解析RGB颜色
                                if (hasRGB && coords.Length >= 6)
                                {
                                    if (byte.TryParse(coords[3], out byte r) &&
                                        byte.TryParse(coords[4], out byte g) &&
                                        byte.TryParse(coords[5], out byte b))
                                    {
                                        point.Color = new Vector3(r / 255.0f, g / 255.0f, b / 255.0f);
                                        point.HasColor = true;
                                    }
                                }
                                
                                points.Add(point);
                            }
                        }
                        currentVertex++;
                    }
                }
            }
            
            int coloredCount = points.Count(p => p.HasColor);
            System.Diagnostics.Debug.WriteLine($"PLY加载完成: {points.Count:N0} 个点，{coloredCount:N0} 个有颜色");
            return points;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载PLY文件失败: {ex.Message}");
            throw;
        }
    }

    

    /// <summary>
    /// 检测文件是否为PLY格式
    /// </summary>
    public static bool IsPLYFile(string filePath)
    {
        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string firstLine = reader.ReadLine();
                return firstLine != null && firstLine.Trim().ToLower() == "ply";
            }
        }
        catch
        {
            return false;
        }
    }
}