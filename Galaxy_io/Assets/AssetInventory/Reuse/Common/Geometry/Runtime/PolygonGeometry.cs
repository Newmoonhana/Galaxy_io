using System;
using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;
using LibTess = LibTessDotNet.Double;

namespace ImpossibleRobert.Common.Geometry
{
    public sealed class TriangulatedSurface
    {
        public readonly Vector2[] Vertices;
        public readonly int[] Indices;
        public TriangulatedSurface(Vector2[] vertices, int[] indices) { Vertices = vertices; Indices = indices; }
    }

    /// <summary>Bounded polygon operations shared by planar surfaces and extruded geometry.</summary>
    public static class PolygonGeometry
    {
        public const int MaximumVertices = 65536;

        public static Vector2[][] Union(IReadOnlyList<Vector2[]> contours)
        {
            return FromPaths(Clipper.Union(ToPaths(contours), new PathsD(), FillRule.NonZero, 6));
        }

        public static Vector2[][] Difference(IReadOnlyList<Vector2[]> subject, IReadOnlyList<Vector2[]> clip)
        {
            return FromPaths(Clipper.Difference(ToPaths(subject), ToPaths(clip), FillRule.NonZero, 6));
        }

        public static Vector2[][] Intersect(IReadOnlyList<Vector2[]> subject, IReadOnlyList<Vector2[]> clip)
        {
            return FromPaths(Clipper.Intersect(ToPaths(subject), ToPaths(clip), FillRule.NonZero, 6));
        }

        /// <summary>Round dilation or erosion of a complete nonzero-winding polygon set, preserving holes.</summary>
        public static Vector2[][] Offset(IReadOnlyList<Vector2[]> contours, float distance, float tolerance = .0005f)
        {
            if (!float.IsFinite(distance) || !float.IsFinite(tolerance) || tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(distance));
            return FromPaths(Clipper.InflatePaths(ToPaths(contours), distance, JoinType.Round, EndType.Polygon, 2, 6, tolerance));
        }

        public static TriangulatedSurface Triangulate(IReadOnlyList<Vector2[]> contours)
        {
            LibTess.Tess tess = new LibTess.Tess();
            int count = 0;
            foreach (Vector2[] contour in contours)
            {
                count = checked(count + contour.Length);
                if (count > MaximumVertices) throw new InvalidOperationException("Surface triangulation exceeded its vertex budget.");
                if (contour.Length < 3) continue;
                LibTess.ContourVertex[] vertices = new LibTess.ContourVertex[contour.Length];
                for (int index = 0; index < contour.Length; index++)
                    vertices[index].Position = new LibTess.Vec3 { X = contour[index].x, Y = contour[index].y, Z = 0 };
                tess.AddContour(vertices, LibTess.ContourOrientation.Original);
            }
            tess.Tessellate(LibTess.WindingRule.NonZero, LibTess.ElementType.Polygons, 3);
            if (tess.VertexCount > MaximumVertices) throw new InvalidOperationException("Surface triangulation exceeded its vertex budget.");
            Vector2[] result = new Vector2[tess.VertexCount];
            for (int index = 0; index < result.Length; index++)
                result[index] = new Vector2((float)tess.Vertices[index].Position.X, (float)tess.Vertices[index].Position.Y);
            int[] triangles = new int[tess.ElementCount * 3];
            if (triangles.Length > 0) Array.Copy(tess.Elements, triangles, triangles.Length);
            return new TriangulatedSurface(result, triangles);
        }

        public static double SignedArea(IReadOnlyList<Vector2> polygon)
        {
            double area = 0;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 a = polygon[index];
                Vector2 b = polygon[(index + 1) % polygon.Count];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }
            return area * .5;
        }

        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            bool inside = false;
            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
            {
                Vector2 a = polygon[index];
                Vector2 b = polygon[previous];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (double)(b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static Vector2[][] RoundCorners(IReadOnlyList<Vector2[]> contours, float radius, float tolerance)
        {
            if (radius <= 0) return FromPaths(ToPaths(contours));
            List<Vector2[]> result = new List<Vector2[]>();
            foreach (Vector2[] contour in contours)
            {
                List<Vector2> points = new List<Vector2>();
                for (int index = 0; index < contour.Length; index++)
                {
                    Vector2 vertex = contour[index];
                    Vector2 incoming = contour[(index + contour.Length - 1) % contour.Length] - vertex;
                    Vector2 outgoing = contour[(index + 1) % contour.Length] - vertex;
                    float length = Mathf.Min(radius, .49f * Mathf.Min(incoming.magnitude, outgoing.magnitude));
                    Vector2 start = vertex + incoming.normalized * length;
                    Vector2 end = vertex + outgoing.normalized * length;
                    CurveSegment.AppendDistinct(points, start);
                    CurveSegment.Quadratic(start, vertex, end).Flatten(points, tolerance, MaximumVertices);
                }
                result.Add(points.ToArray());
            }
            return result.ToArray();
        }

        static PathsD ToPaths(IReadOnlyList<Vector2[]> contours)
        {
            PathsD result = new PathsD();
            int count = 0;
            foreach (Vector2[] contour in contours)
            {
                count = checked(count + contour.Length);
                if (count > MaximumVertices) throw new InvalidOperationException("Polygon input exceeded its vertex budget.");
                PathD path = new PathD(contour.Length);
                foreach (Vector2 point in contour)
                {
                    if (!float.IsFinite(point.x) || !float.IsFinite(point.y)) throw new ArgumentException("Polygon coordinates must be finite.");
                    path.Add(new PointD(point.x, point.y));
                }
                if (path.Count >= 3) result.Add(path);
            }
            return result;
        }

        static Vector2[][] FromPaths(PathsD paths)
        {
            Vector2[][] result = new Vector2[paths.Count][];
            int count = 0;
            for (int index = 0; index < paths.Count; index++)
            {
                count = checked(count + paths[index].Count);
                if (count > MaximumVertices) throw new InvalidOperationException("Polygon output exceeded its vertex budget.");
                Vector2[] points = new Vector2[paths[index].Count];
                for (int point = 0; point < points.Length; point++)
                    points[point] = new Vector2((float)paths[index][point].x, (float)paths[index][point].y);
                result[index] = points;
            }
            return result;
        }
    }
}
