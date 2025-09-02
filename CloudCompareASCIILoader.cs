using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Globalization;
using OpenTK;

/// <summary>
/// CloudCompare ASCII格式点云加载器
/// 支持CloudCompare导出的ASCII cloud格式 (.txt)
/// </summary>
public class CloudCompareASCIILoader
{
    /// <summary>
    /// 加载CloudCompare导出的ASCII格式点云文件
    /// 格式: X Y Z Intensity [其他字段...]
    /// </summary>
    public static List<Vector3> LoadCloudCompareASCII(string filePath)
    {
        var points = new List<Vector3>();
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件不存在: {filePath}");

        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string line;
                int lineNumber = 0;
                int validPoints = 0;
                int skippedPoints = 0;
                
                System.Diagnostics.Debug.WriteLine($"开始加载CloudCompare ASCII文件: {Path.GetFileName(filePath)}");
                
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    line = line.Trim();
                    
                    // 跳过空行和注释行
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;
                    
                    // 分割数据（空格或制表符）
                    var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // 至少需要3个字段 (X, Y, Z)
                    if (parts.Length >= 3)
                    {
                        try
                        {
                            // 解析XYZ坐标 (前3个字段)
                            float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                            float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                            float z = float.Parse(parts[2], CultureInfo.InvariantCulture);
                            
                            // 可选：解析强度值 (第4个字段)
                            float intensity = 0;
                            if (parts.Length >= 4)
                            {
                                if (!float.TryParse(parts[3], out intensity))
                                {
                                    intensity = 0;
                                }
                            }
                            
                            // 验证点的有效性
                            if (IsValidPoint(x, y, z))
                            {
                                points.Add(new Vector3(x, y, z));
                                validPoints++;
                                
                                // 输出前几个点用于验证
                                if (validPoints <= 10)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"点 {validPoints}: X={x:F6}, Y={y:F6}, Z={z:F6}, 强度={intensity:F1}");
                                }
                            }
                            else
                            {
                                skippedPoints++;
                                if (skippedPoints <= 5) // 只显示前5个无效点
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"跳过无效点 (行{lineNumber}): X={x:F6}, Y={y:F6}, Z={z:F6}");
                                }
                            }
                        }
                        catch (FormatException ex)
                        {
                            skippedPoints++;
                            if (skippedPoints <= 5)
                            {
                                System.Diagnostics.Debug.WriteLine($"解析失败 (行{lineNumber}): {line} - {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        skippedPoints++;
                        if (skippedPoints <= 5)
                        {
                            System.Diagnostics.Debug.WriteLine($"字段不足 (行{lineNumber}): {line}");
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"加载完成:");
                System.Diagnostics.Debug.WriteLine($"  总行数: {lineNumber:N0}");
                System.Diagnostics.Debug.WriteLine($"  有效点: {validPoints:N0}");
                System.Diagnostics.Debug.WriteLine($"  跳过点: {skippedPoints:N0}");
                
                // 显示点云统计信息
                if (points.Count > 0)
                {
                    ShowPointCloudStatistics(points);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"加载CloudCompare ASCII文件失败: {ex.Message}", ex);
        }
        
        return points;
    }
    
    /// <summary>
    /// 显示点云统计信息
    /// </summary>
    private static void ShowPointCloudStatistics(List<Vector3> points)
    {
        if (points.Count == 0) return;
        
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        
        foreach (var point in points)
        {
            if (point.X < minX) minX = point.X;
            if (point.X > maxX) maxX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.Y > maxY) maxY = point.Y;
            if (point.Z < minZ) minZ = point.Z;
            if (point.Z > maxZ) maxZ = point.Z;
        }
        
        System.Diagnostics.Debug.WriteLine($"点云边界框:");
        System.Diagnostics.Debug.WriteLine($"  X: [{minX:F3}, {maxX:F3}] 跨度: {maxX - minX:F3}");
        System.Diagnostics.Debug.WriteLine($"  Y: [{minY:F3}, {maxY:F3}] 跨度: {maxY - minY:F3}");
        System.Diagnostics.Debug.WriteLine($"  Z: [{minZ:F3}, {maxZ:F3}] 跨度: {maxZ - minZ:F3}");
        
        float centerX = (minX + maxX) / 2;
        float centerY = (minY + maxY) / 2;
        float centerZ = (minZ + maxZ) / 2;
        System.Diagnostics.Debug.WriteLine($"  中心点: ({centerX:F3}, {centerY:F3}, {centerZ:F3})");
    }
    
    /// <summary>
    /// 检查点是否有效
    /// </summary>
    private static bool IsValidPoint(float x, float y, float z)
    {
        // 检查NaN和无穷大
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
            float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
            return false;
            
        // 检查合理的坐标范围
        const float maxCoord = 1e6f;
        if (Math.Abs(x) > maxCoord || Math.Abs(y) > maxCoord || Math.Abs(z) > maxCoord)
            return false;
            
        return true;
    }
    
    /// <summary>
    /// 检测文件是否为CloudCompare ASCII格式
    /// </summary>
    public static bool IsCloudCompareASCII(string filePath)
    {
        try
        {
            using (var reader = new StreamReader(filePath))
            {
                // 读取前几行进行检测
                for (int i = 0; i < 10; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    
                    line = line.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    
                    // 检查是否符合数字格式 (至少3个浮点数)
                    var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        // 尝试解析前3个字段为浮点数
                        if (float.TryParse(parts[0], out _) &&
                            float.TryParse(parts[1], out _) &&
                            float.TryParse(parts[2], out _))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            return false;
        }
        
        return false;
    }
}
