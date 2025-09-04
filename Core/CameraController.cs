using OpenTK;
using System;

namespace LoadPCDtest.Core
{
    /// <summary>
    /// 相机控制器
    /// </summary>
    public class CameraController
    {
        // 相机参数
        
        public float Distance { get; set; } = 10f;
        public Vector2 Pan { get; set; } = Vector2.Zero;
        
        // 点云旋转
        public float PointCloudYaw { get; set; } = 0f;
        public float PointCloudPitch { get; set; } = 0f;
        
        // 缩放控制
        public float GlobalScale { get; set; } = 1.0f;
        
        // 交互状态
        public bool IsRotating { get; set; } = false;
        public bool IsPanning { get; set; } = false;

        /// <summary>
        /// 重置相机到默认视角
        /// </summary>
        public void ResetToDefault()
        {
            
            PointCloudYaw = 0f;
            PointCloudPitch = 0f;
            Pan = Vector2.Zero;
            GlobalScale = 1.0f;
        }

        /// <summary>
        /// 重置相机为过滤后数据优化的视角
        /// </summary>
        public void ResetForFilteredData()
        {
            
            PointCloudYaw = 0f;
            PointCloudPitch = 0f;
            Pan = Vector2.Zero;
            GlobalScale = 1.0f;
        }

        /// <summary>
        /// 处理鼠标旋转
        /// </summary>
        public void HandleRotation(float deltaX, float deltaY)
        {
            PointCloudYaw += deltaX * 0.3f;
            PointCloudPitch += deltaY * 0.3f;
            PointCloudPitch = Math.Max(-89f, Math.Min(89f, PointCloudPitch));
        }

        /// <summary>
        /// 处理鼠标平移
        /// </summary>
        public void HandlePanning(float deltaX, float deltaY, float distance, float objectScale)
        {
            // 基础速度减慢，并与距离相关
            float baseSpeed = 0.003f;  // 减小基础速度
            float panSpeed = baseSpeed * Math.Max(distance * 0.1f, 1.0f);
            
            // 计算稳定的缩放因子，避免过滤后剧烈变化
            float effectiveScale = Math.Max(objectScale * GlobalScale, 0.001f);
            float normalizedScale = Math.Min(effectiveScale, 1.0f);
            
            // 使用更温和的平移计算
            float panFactor = panSpeed / (normalizedScale * 20f + 0.5f);  // 增加分母，减慢速度
            
            // 限制单次移动的最大幅度
            float maxDelta = 0.5f;
            deltaX = Math.Max(-maxDelta, Math.Min(maxDelta, deltaX * panFactor));
            deltaY = Math.Max(-maxDelta, Math.Min(maxDelta, deltaY * panFactor));
            
            Pan = new Vector2(
                Pan.X + deltaX,
                Pan.Y - deltaY
            );
        }

        /// <summary>
        /// 处理滚轮缩放
        /// </summary>
        public void HandleZoom(int delta)
        {
            float zoomFactor = (delta > 0) ? 1.1f : 0.9f;
            GlobalScale *= zoomFactor;
            GlobalScale = Math.Max(0.001f, Math.Min(1000f, GlobalScale));
        }
    }
}
