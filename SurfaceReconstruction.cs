using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;

/// <summary>
/// 表面重建工具 - 简化版
/// </summary>
public static class SurfaceReconstruction
{
    public class Triangle
    {
        public Vector3 V1 { get; set; }
        public Vector3 V2 { get; set; }
        public Vector3 V3 { get; set; }
        public Vector3 Normal { get; set; }
        
        public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
            
            // 计算法向量
            var edge1 = v2 - v1;
            var edge2 = v3 - v1;
            Normal = Vector3.Cross(edge1, edge2).Normalized();
        }
    }
    
    public class Mesh
    {
        public List<Triangle> Triangles { get; set; } = new List<Triangle>();
        
        public void AddTriangle(Triangle triangle)
        {
            Triangles.Add(triangle);
        }
    }
    
    /// <summary>
    /// 简单的2.5D网格重建
    /// </summary>
    public static Mesh CreateSimpleGridMesh(List<Vector3> points, float gridSize = 0.1f)
    {
        var mesh = new Mesh();
        
        if (points == null || points.Count < 3)
            return mesh;
            
        try
        {
            // 创建网格
            var grid = CreateGrid(points, gridSize);
            
            // 生成三角形
            GenerateTriangles(grid, mesh);
            
            System.Diagnostics.Debug.WriteLine($"表面重建完成: {mesh.Triangles.Count} 个三角形");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"表面重建失败: {ex.Message}");
        }
        
        return mesh;
    }
    
    /// <summary>
    /// 创建规则网格
    /// </summary>
    private static Dictionary<(int, int), Vector3> CreateGrid(List<Vector3> points, float gridSize)
    {
        var grid = new Dictionary<(int, int), Vector3>();
        
        // 计算边界
        float minX = points.Min(p => p.X);
        float maxX = points.Max(p => p.X);
        float minY = points.Min(p => p.Y);
        float maxY = points.Max(p => p.Y);
        
        // 将点映射到网格
        foreach (var point in points)
        {
            int gridX = (int)Math.Round((point.X - minX) / gridSize);
            int gridY = (int)Math.Round((point.Y - minY) / gridSize);
            
            var key = (gridX, gridY);
            
            // 如果网格位置已有点，选择Z值较高的
            if (!grid.ContainsKey(key) || point.Z > grid[key].Z)
            {
                grid[key] = point;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"网格创建: {grid.Count} 个网格点");
        return grid;
    }
    
    /// <summary>
    /// 从网格生成三角形
    /// </summary>
    private static void GenerateTriangles(Dictionary<(int, int), Vector3> grid, Mesh mesh)
    {
        var keys = grid.Keys.ToList();
        
        foreach (var key in keys)
        {
            int x = key.Item1;
            int y = key.Item2;
            
            // 生成两个三角形组成一个四边形
            if (grid.ContainsKey((x, y)) && 
                grid.ContainsKey((x + 1, y)) && 
                grid.ContainsKey((x, y + 1)) && 
                grid.ContainsKey((x + 1, y + 1)))
            {
                var p1 = grid[(x, y)];
                var p2 = grid[(x + 1, y)];
                var p3 = grid[(x, y + 1)];
                var p4 = grid[(x + 1, y + 1)];
                
                // 三角形1: p1-p2-p3
                mesh.AddTriangle(new Triangle(p1, p2, p3));
                
                // 三角形2: p2-p4-p3
                mesh.AddTriangle(new Triangle(p2, p4, p3));
            }
        }
    }
}