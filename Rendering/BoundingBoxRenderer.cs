using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using OpenTK;

namespace LoadPCDtest.Rendering
{
    /// <summary>
    /// 边界框渲染器
    /// </summary>
    public class BoundingBoxRenderer
    {
        /// <summary>
        /// 渲染边界框
        /// </summary>
        public void RenderBoundingBox(List<Vector3> points)
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
    }
}
