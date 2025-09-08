using OpenTK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LoadPCDtest.Analysis
{
	/// <summary>
	/// 基于多边形边的矩形内缩扫描，鲁棒确定墙面位置
	/// </summary>
	public static class WallRefinement
	{
		public static float LastSelectedRatio { get; private set; } = -1f;
		public static bool HasReportedOnce { get; set; } = false;

		public class WallFace
		{
			public Vector2 Start;           // 2D 起点（XY）
			public Vector2 End;             // 2D 终点（XY）
			public Vector2 NormalInward;    // 指向多边形内部的单位法线
			public float EdgeLength;        // 边长
			public float BestOffset;        // 选中的内缩距离（米）
			public float InitialWidth;      // 初始扫描宽度（米）
			public float Step;              // 步长（米）
			public int BaselineCount;       // 初始宽度内的点数
			public int BestCount;           // BestOffset 对应的点数
		}

		/// <summary>
		/// 对用户划定的多边形的每条边执行内缩扫描，返回每条边的墙面位置（内缩距离）
		/// </summary>
		public static List<WallFace> DetectFacesByEdgeSweep(
			List<Vector3> points,
			List<Vector2> polygon,
			float initialWidthMeters = 2.0f,
			float stepMeters = 0.05f,
			int minBaselinePoints = 50,
			bool chooseAfterDrop = true,
			float outwardBiasMeters = 0.0f,
			float ratioThreshold = -1f)
		{
			var results = new List<WallFace>();
			if (points == null || points.Count == 0) return results;
			if (polygon == null || polygon.Count < 2) return results;

			// 计算多边形方向（>0 逆时针，<0 顺时针）
			float signedArea = 0f;
			for (int i = 0; i < polygon.Count; i++)
			{
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				signedArea += (a.X * b.Y - b.X * a.Y);
			}
			bool isCCW = signedArea > 0f;

			// 预计算XY点
			var pts2 = new List<Vector2>(points.Count);
			for (int i = 0; i < points.Count; i++) pts2.Add(new Vector2(points[i].X, points[i].Y));

			for (int i = 0; i < polygon.Count; i++)
			{
				var p0 = polygon[i];
				var p1 = polygon[(i + 1) % polygon.Count];
				var edge = p1 - p0;
				float len = edge.Length;
				if (len < 1e-6f) continue;

				var u = edge / len; // 切向单位向量（沿边）
				// 逆时针多边形，内部在左侧；顺时针内部在右侧
				Vector2 inward = isCCW ? new Vector2(-u.Y, u.X) : new Vector2(u.Y, -u.X);

				// 基线：宽度 = initialWidthMeters，统计 [0, len] × [0, initialWidth]
				int baseline = 0;
				for (int k = 0; k < pts2.Count; k++)
				{
					var d = pts2[k] - p0;
					float s = Vector2.Dot(d, u);
					if (s < 0 || s > len) continue;
					float t = Vector2.Dot(d, inward);
					if (t >= 0 && t <= initialWidthMeters) baseline++;
				}

				if (baseline < minBaselinePoints)
				{
					// 此边缺乏足够点支撑，跳过
					continue;
				}

				// 扫描：逐步缩小宽度，得到占比曲线
				int steps = Math.Max(1, (int)Math.Ceiling(initialWidthMeters / Math.Max(1e-3f, stepMeters)));
				var widths = new float[steps + 1];
				var counts = new int[steps + 1];
				var ratios = new float[steps + 1];

				for (int si = 0; si <= steps; si++)
				{
					float w = Math.Max(0f, initialWidthMeters - si * stepMeters);
					widths[si] = w;
					if (si == 0)
					{
						counts[si] = baseline;
						ratios[si] = 1.0f;
						continue;
					}

					int c = 0;
					for (int k = 0; k < pts2.Count; k++)
					{
						var d = pts2[k] - p0;
						float s = Vector2.Dot(d, u);
						if (s < 0 || s > len) continue;
						float t = Vector2.Dot(d, inward);
						if (t >= 0 && t <= w) c++;
					}
					counts[si] = c;
					ratios[si] = baseline > 0 ? (float)c / baseline : 0f;
				}

				// 选择策略：优先使用占比阈值，其次使用“最大下降拐点”
				int bestIndex = -1;
				if (ratioThreshold >= 0f && ratioThreshold <= 1f)
				{
					for (int si = 1; si <= steps; si++)
					{
						if (ratios[si] <= ratioThreshold)
						{
							bestIndex = si;
							break;
						}
					}
				}

				if (bestIndex < 0)
				{
					float bestDrop = float.NegativeInfinity;
					bestIndex = 0;
					for (int si = 1; si <= steps; si++)
					{
						float drop = ratios[si] - ratios[si - 1]; // 负值为下降
						if (drop < bestDrop)
						{
							bestDrop = drop;
							bestIndex = chooseAfterDrop ? si : Math.Max(0, si - 1);
						}
					}
				}

				float bestOffset = widths[bestIndex];
				float selectedRatio = ratios[bestIndex];
				if (LastSelectedRatio < 0f) LastSelectedRatio = selectedRatio;
				if (outwardBiasMeters > 0)
				{
					bestOffset = Math.Max(0f, bestOffset - outwardBiasMeters);
				}
				int bestCount = counts[bestIndex];

				results.Add(new WallFace
				{
					Start = p0 + inward * bestOffset,
					End = p1 + inward * bestOffset,
					NormalInward = inward,
					EdgeLength = len,
					BestOffset = bestOffset,
					InitialWidth = initialWidthMeters,
					Step = stepMeters,
					BaselineCount = baseline,
					BestCount = bestCount
				});
			}

			return results;
		}

		/// <summary>
		/// 生成检测墙面点（沿墙线采样，按多个Z层展开）
		/// </summary>
		public static List<Vector3> GenerateWallPoints(
			List<WallFace> faces,
			float zMin,
			float zMax,
			float alongSpacing = 0.5f,
			float zSpacing = 0.5f)
		{
			var output = new List<Vector3>();
			if (faces == null || faces.Count == 0) return output;
			if (zMax < zMin) { var tmp = zMin; zMin = zMax; zMax = tmp; }

			foreach (var f in faces)
			{
				var dir = f.End - f.Start;
				float len = dir.Length;
				if (len < 1e-6f) continue;
				var u = dir / len;

				int samplesAlong = Math.Max(2, (int)Math.Ceiling(len / Math.Max(1e-3f, alongSpacing)) + 1);
				int samplesZ = Math.Max(2, (int)Math.Ceiling((zMax - zMin) / Math.Max(1e-3f, zSpacing)) + 1);

				for (int i = 0; i < samplesAlong; i++)
				{
					float t = (float)i / (samplesAlong - 1);
					var p2 = f.Start + t * dir;
					for (int j = 0; j < samplesZ; j++)
					{
						float tz = (float)j / (samplesZ - 1);
						float z = zMin + (zMax - zMin) * tz;
						output.Add(new Vector3(p2.X, p2.Y, z));
					}
				}
			}

			return output;
		}

		/// <summary>
		/// 将检测到的墙面导出为PLY点，用于可视化（沿墙线采样，按多个Z层展开）
		/// </summary>
		public static void ExportFacesAsPly(
			List<WallFace> faces,
			string outputPath,
			float zMin,
			float zMax,
			float alongSpacing = 0.5f,
			float zSpacing = 0.5f)
		{
			if (faces == null || faces.Count == 0) return;
			if (zMax < zMin) { var tmp = zMin; zMin = zMax; zMax = tmp; }

			// 预采样统计
			int total = 0;
			foreach (var f in faces)
			{
				int samplesAlong = Math.Max(2, (int)Math.Ceiling(f.EdgeLength / Math.Max(1e-3f, alongSpacing)) + 1);
				int samplesZ = Math.Max(2, (int)Math.Ceiling((zMax - zMin) / Math.Max(1e-3f, zSpacing)) + 1);
				total += samplesAlong * samplesZ;
			}

			using (var sw = new StreamWriter(outputPath, false))
			{
				sw.WriteLine("ply");
				sw.WriteLine("format ascii 1.0");
				sw.WriteLine($"element vertex {total}");
				sw.WriteLine("property float x");
				sw.WriteLine("property float y");
				sw.WriteLine("property float z");
				sw.WriteLine("end_header");

				foreach (var f in faces)
				{
					var dir = f.End - f.Start;
					float len = dir.Length;
					if (len < 1e-6f) continue;
					var u = dir / len;

					int samplesAlong = Math.Max(2, (int)Math.Ceiling(len / Math.Max(1e-3f, alongSpacing)) + 1);
					int samplesZ = Math.Max(2, (int)Math.Ceiling((zMax - zMin) / Math.Max(1e-3f, zSpacing)) + 1);

					for (int i = 0; i < samplesAlong; i++)
					{
						float t = (float)i / (samplesAlong - 1);
						var p2 = f.Start + t * dir;
						for (int j = 0; j < samplesZ; j++)
						{
							float tz = (float)j / (samplesZ - 1);
							float z = zMin + (zMax - zMin) * tz;
							sw.WriteLine($"{p2.X:F6} {p2.Y:F6} {z:F6}");
						}
					}
				}
			}
		}
	}
}


