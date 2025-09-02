using OpenTK.Graphics.OpenGL;
using System;
using LoadPCDtest.Core;

namespace LoadPCDtest.Rendering
{
    /// <summary>
    /// 轨迹球渲染器
    /// </summary>
    public class TrackballRenderer
    {
        private float trackballRadius = 5.0f;
        private int trackballSegments = 48;

        /// <summary>
        /// 渲染轨迹球
        /// </summary>
        public void RenderTrackball(PointCloudData data, CameraController camera, int viewportWidth, int viewportHeight)
        {
            // 计算合适的轨迹球半径
            float radius = CalculateTrackballRadius(camera.Distance, viewportWidth, viewportHeight);
            
            // 启用混合以实现半透明效果
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            // 保存当前矩阵状态
            GL.PushMatrix();
            
            // 取消缩放，只保留位置和旋转变换
            GL.Scale(1.0f / (data.ObjectScale * camera.GlobalScale), 
                     1.0f / (data.ObjectScale * camera.GlobalScale), 
                     1.0f / (data.ObjectScale * camera.GlobalScale));
            
            // 绘制三个主要的圆圈（XY, XZ, YZ平面）
            GL.LineWidth(1.0f);
            
            // XY平面圆圈 (红色)
            GL.Color4(1.0f, 0.2f, 0.2f, 0.7f);
            DrawCircle(radius, trackballSegments, 0, 0, 1); // Z轴为法向量
            
            // XZ平面圆圈 (绿色)
            GL.Color4(0.2f, 1.0f, 0.2f, 0.7f);
            DrawCircle(radius, trackballSegments, 0, 1, 0); // Y轴为法向量
            
            // YZ平面圆圈 (蓝色)
            GL.Color4(0.2f, 0.2f, 1.0f, 0.7f);
            DrawCircle(radius, trackballSegments, 1, 0, 0); // X轴为法向量
            
            // 绘制中心点
            GL.PointSize(4.0f);
            GL.Color4(1.0f, 1.0f, 1.0f, 0.8f);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(0, 0, 0);
            GL.End();
            
            GL.PopMatrix();
            GL.Disable(EnableCap.Blend);
        }

        /// <summary>
        /// 绘制圆圈
        /// </summary>
        private void DrawCircle(float radius, int segments, float normalX, float normalY, float normalZ)
        {
            GL.Begin(PrimitiveType.LineLoop);
            
            for (int i = 0; i < segments; i++)
            {
                float angle = 2.0f * (float)Math.PI * i / segments;
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                
                if (normalZ != 0) // XY平面
                {
                    GL.Vertex3(radius * cos, radius * sin, 0);
                }
                else if (normalY != 0) // XZ平面
                {
                    GL.Vertex3(radius * cos, 0, radius * sin);
                }
                else if (normalX != 0) // YZ平面
                {
                    GL.Vertex3(0, radius * cos, radius * sin);
                }
            }
            
            GL.End();
        }

        /// <summary>
        /// 计算合适的轨迹球半径
        /// </summary>
        private float CalculateTrackballRadius(float distance, int viewportWidth, int viewportHeight)
        {
            // 基于窗体大小和相机距离计算合适的轨迹球半径
            float viewportSize = Math.Min(viewportWidth, viewportHeight);
            
            // 轨迹球应该占据视口的80-90%
            float viewportRatio = 0.85f;
            
            // 根据透视投影计算3D空间中的半径
            float fovRadians = (float)(Math.PI / 4); // 45度转弧度
            float radius = (float)(Math.Tan(fovRadians / 2) * distance * viewportRatio);
            
            // 设置合理的范围
            radius = Math.Max(2.0f, Math.Min(50.0f, radius));
            
            return radius;
        }
    }
}
