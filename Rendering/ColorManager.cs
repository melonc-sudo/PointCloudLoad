using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using LoadPCDtest.Core;

namespace LoadPCDtest.Rendering
{
    /// <summary>
    /// 颜色显示模式
    /// </summary>
    public enum ColorMode
    {
        HeightBased,    // 基于高度的颜色 (蓝→绿→红)
        OriginalRGB,    // 使用原始RGB颜色
        White           // 白色显示
    }

    /// <summary>
    /// 颜色管理器
    /// </summary>
    public class ColorManager
    {
        public ColorMode CurrentColorMode { get; set; } = ColorMode.HeightBased;

        /// <summary>
        /// 切换颜色模式
        /// </summary>
        public void CycleColorMode(bool hasColors)
        {
            switch (CurrentColorMode)
            {
                case ColorMode.HeightBased:
                    CurrentColorMode = hasColors ? ColorMode.OriginalRGB : ColorMode.White;
                    break;
                case ColorMode.OriginalRGB:
                    CurrentColorMode = ColorMode.White;
                    break;
                case ColorMode.White:
                    CurrentColorMode = ColorMode.HeightBased;
                    break;
            }

            string modeName = GetColorModeName();
            System.Diagnostics.Debug.WriteLine($"颜色模式切换为: {modeName}");
        }

        /// <summary>
        /// 获取当前颜色模式名称
        /// </summary>
        public string GetColorModeName()
        {
            switch (CurrentColorMode)
            {
                case ColorMode.HeightBased:
                    return "高度着色 (蓝→绿→红)";
                case ColorMode.OriginalRGB:
                    return "原始RGB颜色";
                case ColorMode.White:
                    return "白色显示";
                default:
                    return "未知颜色模式";
            }
        }
                /// <summary>
        /// 渲染带颜色的点云
        /// </summary>
        public void RenderColoredPoints(PointCloudData data)
        {
            if (data.Points == null || data.Points.Count == 0) return;

            GL.Begin(PrimitiveType.Points);
            
            // 根据颜色模式选择渲染方式
            switch (CurrentColorMode)
            {
                case ColorMode.OriginalRGB:
                    if (data.HasColors && data.PLYPoints.Count == data.Points.Count)
                    {
                        RenderWithOriginalColors(data);
                    }
                    else
                    {
                        RenderWithHeightColors(data.Points);
                    }
                    break;
                    
                case ColorMode.White:
                    RenderWithWhiteColor(data.Points);
                    break;
                    
                case ColorMode.HeightBased:
                default:
                    RenderWithHeightColors(data.Points);
                    break;
            }

            GL.End();
        }

        /// <summary>
        /// 使用原始RGB颜色渲染
        /// </summary>
        private void RenderWithOriginalColors(PointCloudData data)
        {
            for (int i = 0; i < data.PLYPoints.Count; i++)
            {
                var plyPoint = data.PLYPoints[i];
                var p = data.Points[i];
                
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

        /// <summary>
        /// 使用白色渲染
        /// </summary>
        private void RenderWithWhiteColor(List<Vector3> points)
        {
            GL.Color3(1.0f, 1.0f, 1.0f);
            foreach (var p in points)
            {
                GL.Vertex3(p.X, p.Y, p.Z);
            }
        }
                /// <summary>
        /// 使用基于高度的颜色渲染
        /// </summary>
        private void RenderWithHeightColors(List<Vector3> points)
        {
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in points)
            {
                if (p.Z < minZ) minZ = p.Z;
                if (p.Z > maxZ) maxZ = p.Z;
            }
            
            float zRange = maxZ - minZ;
            if (zRange < 1e-6f) zRange = 1.0f;
            
            int pointCount = 0;
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
                pointCount++;
            }
        }
    }
}
