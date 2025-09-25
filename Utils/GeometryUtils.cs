using System;
using System.Collections.Generic;
using System.Linq;
using LoadPCDtest.Analysis;
using OpenTK;

namespace LoadPCDtest.Utils
{
	/// <summary>
	/// 几何帮助：计算凸包、外扩多边形、从墙面生成近似四边形等
	/// </summary>
	public static class GeometryUtils
	{
		/// <summary>
		/// 计算二维凸包（单调链），返回按顺序的多边形点（逆时针）
		/// </summary>
		public static List<Vector2> ComputeConvexHull2D(List<Vector2> points)
		{
			if (points == null || points.Count < 3) return points?.ToList() ?? new List<Vector2>();
			var pts = points.Distinct(new Vec2Eq()).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
			if (pts.Count < 3) return pts;

			List<Vector2> lower = new List<Vector2>();
			foreach (var p in pts)
			{
				while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0) lower.RemoveAt(lower.Count - 1);
				lower.Add(p);
			}

			List<Vector2> upper = new List<Vector2>();
			for (int i = pts.Count - 1; i >= 0; i--)
			{
				var p = pts[i];
				while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0) upper.RemoveAt(upper.Count - 1);
				upper.Add(p);
			}

			lower.RemoveAt(lower.Count - 1);
			upper.RemoveAt(upper.Count - 1);
			lower.AddRange(upper);
			return lower; // 逆时针
		}

		/// <summary>
		/// 多边形外扩（基于顶点法线 + miter 连接），distance为外扩距离（米）
		/// </summary>
		public static List<Vector2> OffsetPolygonOutward(List<Vector2> polygon, float distance)
		{
			if (polygon == null || polygon.Count < 3 || distance <= 0) return polygon?.ToList() ?? new List<Vector2>();
			int n = polygon.Count; var result = new List<Vector2>(n);
			for (int i = 0; i < n; i++)
			{
				Vector2 prev = polygon[(i - 1 + n) % n];
				Vector2 curr = polygon[i];
				Vector2 next = polygon[(i + 1) % n];
				Vector2 dir1 = Normalize(curr - prev);
				Vector2 dir2 = Normalize(next - curr);
				Vector2 n1 = new Vector2(-dir1.Y, dir1.X);
				Vector2 n2 = new Vector2(-dir2.Y, dir2.X);
				Vector2 miter = n1 + n2;
				float mlen = miter.Length;
				if (mlen < 1e-6f)
				{
					result.Add(curr + n1 * distance);
				}
				else
				{
					miter /= mlen;
					float cosHalf = Vector2.Dot(miter, n1);
					float scale = distance / Math.Max(0.2f, cosHalf);
					scale = Math.Min(scale, distance * 3.0f);
					result.Add(curr + miter * scale);
				}
			}
			return result;
		}

		/// <summary>
		/// 从墙面集合近似出四边形轮廓：取XY的凸包，并用极值方向上的四个支撑点粗略还原四边
		/// </summary>
		public static List<Vector2> ApproximateQuadrilateralFromWalls(List<WallSeparationAnalyzer.Wall> walls)
		{
			var pts = new List<Vector2>();
			if (walls == null) return pts;
			foreach (var w in walls)
			{
				if (w.Direction == WallSeparationAnalyzer.WallDirection.Horizontal) continue;
				foreach (var p in w.Points) pts.Add(new Vector2(p.X, p.Y));
			}
			if (pts.Count < 3) return pts;
			var hull = ComputeConvexHull2D(pts);
			if (hull.Count <= 4) return hull;

			// 简化为4点：按极值（xmin/xmax/ymin/ymax）选代表点
			var xmin = hull.OrderBy(p => p.X).First();
			var xmax = hull.OrderByDescending(p => p.X).First();
			var ymin = hull.OrderBy(p => p.Y).First();
			var ymax = hull.OrderByDescending(p => p.Y).First();
			var quad = new List<Vector2> { xmin, ymin, xmax, ymax };
			// 再按极角排序，形成闭合四边形
			var centroid = new Vector2(quad.Average(p => p.X), quad.Average(p => p.Y));
			quad = quad.OrderBy(p => Math.Atan2(p.Y - centroid.Y, p.X - centroid.X)).ToList();
			return quad;
		}

		private static float Cross(Vector2 a, Vector2 b, Vector2 c)
		{
			var ab = b - a; var ac = c - a; return ab.X * ac.Y - ab.Y * ac.X;
		}
		private static Vector2 Normalize(Vector2 v)
		{
			float len = v.Length; return len < 1e-6f ? Vector2.Zero : v / len;
		}

		private class Vec2Eq : IEqualityComparer<Vector2>
		{
			public bool Equals(Vector2 a, Vector2 b) { return Math.Abs(a.X - b.X) < 1e-6f && Math.Abs(a.Y - b.Y) < 1e-6f; }
			public int GetHashCode(Vector2 v) { unchecked { return (v.X.GetHashCode() * 397) ^ v.Y.GetHashCode(); } }
		}
	}
}



