using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using LoadPCDtest.Core;

namespace LoadPCDtest.Rendering
{
    /// <summary>
    /// 3D网格渲染器
    /// </summary>
    public class MeshRenderer
    {
        /// <summary>
        /// 渲染3D网格
        /// </summary>
        public void RenderMesh(SurfaceReconstruction.Mesh mesh)
        {
            if (mesh == null || mesh.Triangles.Count == 0) return;

            // 启用光照
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            
            // 设置光源
            float[] lightPos = { 0.0f, 0.0f, 10.0f, 1.0f };
            float[] lightAmbient = { 0.3f, 0.3f, 0.3f, 1.0f };
            float[] lightDiffuse = { 0.8f, 0.8f, 0.8f, 1.0f };
            
            GL.Light(LightName.Light0, LightParameter.Position, lightPos);
            GL.Light(LightName.Light0, LightParameter.Ambient, lightAmbient);
            GL.Light(LightName.Light0, LightParameter.Diffuse, lightDiffuse);
            
            // 设置材质
            float[] materialAmbient = { 0.2f, 0.6f, 0.8f, 1.0f };
            float[] materialDiffuse = { 0.3f, 0.7f, 0.9f, 1.0f };
            float[] materialSpecular = { 1.0f, 1.0f, 1.0f, 1.0f };
            
            GL.Material(MaterialFace.Front, MaterialParameter.Ambient, materialAmbient);
            GL.Material(MaterialFace.Front, MaterialParameter.Diffuse, materialDiffuse);
            GL.Material(MaterialFace.Front, MaterialParameter.Specular, materialSpecular);
            GL.Material(MaterialFace.Front, MaterialParameter.Shininess, 50.0f);
            
            // 绘制三角形
            GL.Begin(PrimitiveType.Triangles);
            
            foreach (var triangle in mesh.Triangles)
            {
                // 设置法向量
                GL.Normal3(triangle.Normal.X, triangle.Normal.Y, triangle.Normal.Z);
                
                // 绘制三个顶点
                GL.Vertex3(triangle.V1.X, triangle.V1.Y, triangle.V1.Z);
                GL.Vertex3(triangle.V2.X, triangle.V2.Y, triangle.V2.Z);
                GL.Vertex3(triangle.V3.X, triangle.V3.Y, triangle.V3.Z);
            }
            
            GL.End();
            
            // 可选：绘制线框
            if (mesh.Triangles.Count < 1000) // 避免太多线框影响性能
            {
                GL.Disable(EnableCap.Lighting);
                GL.Color3(0.2f, 0.2f, 0.2f); // 深灰色线框
                GL.LineWidth(1.0f);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                
                GL.Begin(PrimitiveType.Triangles);
                foreach (var triangle in mesh.Triangles)
                {
                    GL.Vertex3(triangle.V1.X, triangle.V1.Y, triangle.V1.Z);
                    GL.Vertex3(triangle.V2.X, triangle.V2.Y, triangle.V2.Z);
                    GL.Vertex3(triangle.V3.X, triangle.V3.Y, triangle.V3.Z);
                }
                GL.End();
                
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            }
            
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Light0);
        }
    }
}
