using OpenTK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using LoadPCDtest.Core;

namespace LoadPCDtest.IO
{
    /// <summary>
    /// 点云文件加载器
    /// </summary>
    public static class PointCloudLoader
    {
        /// <summary>
        /// 加载点云文件
        /// </summary>
        public static bool LoadPointCloud(string path, PointCloudData data)
        {
            try
            {
                string extension = Path.GetExtension(path).ToLower();
                
                if (extension == ".ply" || PLYLoader.IsPLYFile(path))
                {
                    return LoadPLYFile(path, data);
                }
                else if (extension == ".txt" || CloudCompareASCIILoader.IsCloudCompareASCII(path))
                {
                    return LoadTXTFile(path, data);
                }
                else
                {
                    throw new NotSupportedException($"不支持的文件格式: {extension}。请使用PLY或TXT格式。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 加载PLY文件
        /// </summary>
        private static bool LoadPLYFile(string path, PointCloudData data)
        {
            data.CurrentFilePath = path;
            
            // 默认使用可视化范围选择模式（原来的第一种方式）
            try
            {
                // 可视化范围选择模式 - 先加载原始数据
                data.OriginalPLYPoints = PLYLoader.LoadPLYWithColors(path);
                
                // 从PLY点提取坐标
                data.OriginalPoints = new List<Vector3>();
                foreach (var plyPoint in data.OriginalPLYPoints)
                {
                    data.OriginalPoints.Add(plyPoint.Position);
                }
                
                // 检查是否有颜色信息
                data.HasColors = CheckForColors(data.OriginalPLYPoints);
            }
            catch
            {
                // 如果优化加载失败，回退到标准加载
                data.OriginalPoints = PLYLoader.LoadPLY(path);
                data.OriginalPLYPoints.Clear();
                data.HasColors = false;
                System.Diagnostics.Debug.WriteLine("使用PLY加载器成功加载文件（回退模式）");
            }

            return data.OriginalPoints.Count > 0;
        }

        /// <summary>
        /// 加载TXT文件
        /// </summary>
        private static bool LoadTXTFile(string path, PointCloudData data)
        {
            data.CurrentFilePath = path;
            data.OriginalPoints = CloudCompareASCIILoader.LoadCloudCompareASCII(path);
            data.HasColors = false;
            System.Diagnostics.Debug.WriteLine("使用CloudCompare ASCII加载器成功加载文件");
            
            return data.OriginalPoints.Count > 0;
        }



        /// <summary>
        /// 检查PLY点是否包含颜色信息
        /// </summary>
        private static bool CheckForColors(List<PLYLoader.PLYPoint> plyPoints)
        {
            foreach (var plyPoint in plyPoints)
            {
                if (plyPoint.HasColor)
                {
                    return true;
                }
            }
            return false;
        }


    }
}
