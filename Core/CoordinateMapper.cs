using OpenTK;
using System;
using System.Collections.Generic;

namespace LoadPCDtest.Core
{
    /// <summary>
    /// 坐标映射模式
    /// </summary>
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

    /// <summary>
    /// 坐标映射处理类
    /// </summary>
    public static class CoordinateMapper
    {
        /// <summary>
        /// 应用坐标映射
        /// </summary>
        public static void ApplyMapping(PointCloudData data, CoordinateMapping mapping)
        {
            if (data.OriginalPoints == null || data.OriginalPoints.Count == 0) return;
            
            data.Points = new List<Vector3>(data.OriginalPoints.Count);
            
            // 同时处理PLY点（如果有的话）
            if (data.HasColors && data.OriginalPLYPoints.Count == data.OriginalPoints.Count)
            {
                data.PLYPoints = new List<PLYLoader.PLYPoint>(data.OriginalPLYPoints.Count);
            }
            else
            {
                data.PLYPoints.Clear();
            }
            
            for (int i = 0; i < data.OriginalPoints.Count; i++)
            {
                var p = data.OriginalPoints[i];
                Vector3 transformedPoint = ApplyMappingToPoint(p, mapping);
                
                data.Points.Add(transformedPoint);
                
                // 同时处理PLY点的坐标映射
                if (data.HasColors && i < data.OriginalPLYPoints.Count)
                {
                    var plyPoint = new PLYLoader.PLYPoint
                    {
                        Position = transformedPoint,
                        Color = data.OriginalPLYPoints[i].Color,
                        HasColor = data.OriginalPLYPoints[i].HasColor,
                        Intensity = data.OriginalPLYPoints[i].Intensity
                    };
                    data.PLYPoints.Add(plyPoint);
                }
            }
            
            string mappingFormula = GetMappingFormula(mapping);
            System.Diagnostics.Debug.WriteLine($"应用坐标映射: {mappingFormula}");
        }

        /// <summary>
        /// 对单个点应用坐标映射
        /// </summary>
        public static Vector3 ApplyMappingToPoint(Vector3 point, CoordinateMapping mapping)
        {
            switch (mapping)
            {
                case CoordinateMapping.Original:
                    return new Vector3(point.X, point.Y, point.Z);
                case CoordinateMapping.FlipZ:
                    return new Vector3(point.X, point.Y, -point.Z);
                case CoordinateMapping.YToZ:
                    return new Vector3(point.X, point.Z, -point.Y);
                case CoordinateMapping.ZToY:
                    return new Vector3(point.X, -point.Z, point.Y);
                case CoordinateMapping.FlipXZ:
                    return new Vector3(-point.X, point.Y, -point.Z);
                case CoordinateMapping.FlipYZ:
                    return new Vector3(point.X, -point.Y, -point.Z);
                case CoordinateMapping.RotateXY:
                    return new Vector3(-point.Y, point.X, point.Z);
                case CoordinateMapping.RotateXYFlipZ:
                    return new Vector3(-point.Y, point.X, -point.Z);
                default:
                    return new Vector3(point.X, point.Y, point.Z);
            }
        }

        /// <summary>
        /// 获取映射模式名称
        /// </summary>
        public static string GetMappingName(CoordinateMapping mapping)
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

        /// <summary>
        /// 获取映射公式
        /// </summary>
        public static string GetMappingFormula(CoordinateMapping mapping)
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
    }
}

