using OpenTK;
using System.Collections.Generic;

namespace LoadPCDtest.Core
{
    /// <summary>
    /// 点云数据管理类
    /// </summary>
    public class PointCloudData
    {
        public List<Vector3> OriginalPoints { get; set; } = new List<Vector3>();
        public List<Vector3> Points { get; set; } = new List<Vector3>();
        public List<PLYLoader.PLYPoint> OriginalPLYPoints { get; set; } = new List<PLYLoader.PLYPoint>();
        public List<PLYLoader.PLYPoint> PLYPoints { get; set; } = new List<PLYLoader.PLYPoint>();
        public bool HasColors { get; set; } = false;
        public Vector3 Center { get; set; } = Vector3.Zero;
        public Vector3 BoundingBoxSize { get; set; } = Vector3.One;
        public float ObjectScale { get; set; } = 1.0f;
        public string CurrentFilePath { get; set; } = "";



        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            OriginalPoints.Clear();
            Points.Clear();
            OriginalPLYPoints.Clear();
            PLYPoints.Clear();
            HasColors = false;
            Center = Vector3.Zero;
            BoundingBoxSize = Vector3.One;
            ObjectScale = 1.0f;
            CurrentFilePath = "";
        }
    }
}
