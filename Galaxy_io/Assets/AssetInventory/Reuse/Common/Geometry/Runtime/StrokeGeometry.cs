using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImpossibleRobert.Common.Geometry
{
    public enum PathJoin : byte { Round, Miter, Bevel }
    public enum PathCap : byte { Butt, Round, Square }

    /// <summary>Geometric offsets and contour dashes. Miter limits are ratios of tip distance to side offset.</summary>
    public static class StrokeGeometry
    {
        public static Vector2[][] Stroke(IReadOnlyList<Vector2[]> contours, float width, float alignment, PathJoin join, float miterLimit, float tolerance = .0005f)
        {
            Validate(width, miterLimit, tolerance);
            if (width == 0) return Array.Empty<Vector2[]>();
            float outside = width * Mathf.Clamp01(alignment);
            float inside = width * (1f - Mathf.Clamp01(alignment));
            List<Vector2[]> outer = new List<Vector2[]>();
            List<Vector2[]> inner = new List<Vector2[]>();
            foreach (Vector2[] contour in contours)
            {
                if (contour.Length < 3) continue;
                if (outside > 0) AppendStrokeBand(outer, contour, outside, 1, join, miterLimit, PathCap.Butt, tolerance, true);
                if (inside > 0) AppendStrokeBand(inner, contour, inside, 0, join, miterLimit, PathCap.Butt, tolerance, true);
            }
            // Separate the two sides before clipping. A short concave edge's infinite offset
            // intersection can otherwise cut holes through a neighbouring part of the stroke.
            Vector2[][] outward = PolygonGeometry.Difference(PolygonGeometry.Union(outer), contours);
            Vector2[][] inward = PolygonGeometry.Intersect(PolygonGeometry.Union(inner), contours);
            List<Vector2[]> combined = new List<Vector2[]>(outward.Length + inward.Length);
            combined.AddRange(outward);
            combined.AddRange(inward);
            return PolygonGeometry.Union(combined);
        }

        public static Vector2[] OffsetClosed(IReadOnlyList<Vector2> contour, float offset, PathJoin join, float miterLimit, float tolerance)
        {
            Validate(Mathf.Abs(offset), miterLimit, tolerance);
            if (contour == null) throw new ArgumentNullException(nameof(contour));
            if (contour.Count > PolygonGeometry.MaximumVertices) throw new InvalidOperationException("Stroke input exceeded its vertex budget.");
            List<Vector2> output = new List<Vector2>();
            for (int index = 0; index < contour.Count; index++)
            {
                Vector2 vertex = contour[index];
                if (Mathf.Abs(offset) <= 1e-10f) { output.Add(vertex); continue; }
                Vector2 previous = (vertex - contour[(index + contour.Count - 1) % contour.Count]).normalized;
                Vector2 next = (contour[(index + 1) % contour.Count] - vertex).normalized;
                AppendJoin(output, vertex, previous, next, offset, join, miterLimit, tolerance);
                if (output.Count > PolygonGeometry.MaximumVertices) throw new InvalidOperationException("Stroke exceeded its vertex budget.");
            }
            return output.ToArray();
        }

        static void AppendJoin(List<Vector2> output, Vector2 point, Vector2 previous, Vector2 next, float offset, PathJoin join, float limit, float tolerance)
        {
            Vector2 firstNormal = new Vector2(previous.y, -previous.x);
            Vector2 lastNormal = new Vector2(next.y, -next.x);
            Vector2 first = point + firstNormal * offset;
            Vector2 last = point + lastNormal * offset;
            float turn = Cross(previous, next);
            if (Mathf.Abs(turn) < 1e-6f)
            {
                CurveSegment.AppendDistinct(output, last);
                return;
            }
            float distance = Cross(last - first, next) / turn;
            Vector2 intersection = first + previous * distance;
            bool convex = turn * offset > 0;
            float ratio = (intersection - point).magnitude / Mathf.Abs(offset);
            if (!convex || join == PathJoin.Miter && ratio <= limit + .00001f)
            {
                CurveSegment.AppendDistinct(output, intersection);
                return;
            }
            CurveSegment.AppendDistinct(output, first);
            if (join == PathJoin.Round)
            {
                float angle = Mathf.Atan2(turn, Vector2.Dot(previous, next));
                float radius = Mathf.Abs(offset);
                float step = 2f * Mathf.Acos(Mathf.Clamp(1f - tolerance / radius, -1f, 1f));
                int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(angle) / Mathf.Max(.001f, step)), 1, 4096);
                Vector2 start = first - point;
                for (int index = 1; index < segments; index++)
                {
                    float theta = angle * index / segments;
                    float sine = Mathf.Sin(theta), cosine = Mathf.Cos(theta);
                    CurveSegment.AppendDistinct(output, point + new Vector2(cosine * start.x - sine * start.y, sine * start.x + cosine * start.y));
                }
            }
            CurveSegment.AppendDistinct(output, last);
            if (output.Count > PolygonGeometry.MaximumVertices) throw new InvalidOperationException("Stroke exceeded its vertex budget.");
        }

        public static Vector2[][] DashedStroke(IReadOnlyList<Vector2[]> contours, float width, float alignment, PathJoin join, float miterLimit,
            IReadOnlyList<float> pattern, float phase, PathCap cap, bool continueAcrossContours, float tolerance = .0005f)
        {
            Validate(width, miterLimit, tolerance);
            if (!float.IsFinite(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (width == 0 || pattern == null || pattern.Count == 0) return Stroke(contours, width, alignment, join, miterLimit, tolerance);
            return DashedStroke(new MeasuredContourSet(contours), width, alignment, join, miterLimit, pattern, phase, cap, continueAcrossContours, tolerance);
        }

        public static Vector2[][] DashedStroke(MeasuredContourSet contours, float width, float alignment, PathJoin join, float miterLimit,
            IReadOnlyList<float> pattern, float phase, PathCap cap, bool continueAcrossContours, float tolerance = .0005f)
        {
            if (contours == null) throw new ArgumentNullException(nameof(contours));
            Validate(width, miterLimit, tolerance);
            if (!float.IsFinite(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (width == 0 || pattern == null || pattern.Count == 0) return Stroke(contours.Points, width, alignment, join, miterLimit, tolerance);
            double cycle = 0;
            foreach (float value in pattern)
            {
                if (!float.IsFinite(value) || value <= 0) throw new ArgumentException("Contour dash lengths and gaps must be positive and finite.");
                cycle += value;
            }
            // Odd dash lists repeat twice so the on/off parity is periodic.
            int patternCount = pattern.Count % 2 == 0 ? pattern.Count : pattern.Count * 2;
            if (patternCount != pattern.Count) cycle *= 2;
            List<Vector2[]> polygons = new List<Vector2[]>();
            double accumulated = 0;
            for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
            {
                Vector2[] contour = contours.Points[contourIndex];
                if (contour.Length < 3) continue;
                PathMetric path = contours.Metrics[contourIndex];
                double position = 0;
                double offset = ((phase + accumulated) % cycle + cycle) % cycle;
                int dash = 0;
                while (offset >= pattern[dash % pattern.Count]) { offset -= pattern[dash % pattern.Count]; dash++; }
                bool startsOn = (dash & 1) == 0;
                bool endsOn = false;
                List<Vector2[]> segments = new List<Vector2[]>();
                while (position < path.Length - 1e-8)
                {
                    double length = Math.Min(pattern[dash % pattern.Count] - offset, path.Length - position);
                    if ((dash & 1) == 0 && length > 1e-8)
                    {
                        Vector2[] segment = path.Slice(position, position + length);
                        segments.Add(segment);
                    }
                    endsOn = (dash & 1) == 0;
                    position += length;
                    offset = 0;
                    dash = (dash + 1) % patternCount;
                    if (segments.Count > 8192) throw new InvalidOperationException("Contour dash count exceeded its budget.");
                }
                if (startsOn && endsOn && segments.Count == 1)
                {
                    AppendStrokeBand(polygons, contour, width, alignment, join, miterLimit, cap, tolerance, true);
                    segments.Clear();
                }
                else if (startsOn && endsOn && segments.Count > 1)
                {
                    // A closed contour has no cap at its arbitrary measurement seam.
                    List<Vector2> joined = new List<Vector2>(segments[segments.Count - 1]);
                    foreach (Vector2 point in segments[0]) CurveSegment.AppendDistinct(joined, point);
                    segments[0] = joined.ToArray();
                    segments.RemoveAt(segments.Count - 1);
                }
                foreach (Vector2[] segment in segments)
                    AppendStrokeBand(polygons, segment, width, alignment, join, miterLimit, cap, tolerance, false);
                if (polygons.Count > 8192) throw new InvalidOperationException("Contour dash geometry exceeded its budget.");
                if (continueAcrossContours) accumulated += path.Length;
            }
            return PolygonGeometry.Union(polygons);
        }

        static void AppendStrokeBand(List<Vector2[]> polygons, IReadOnlyList<Vector2> points, float width, float alignment,
            PathJoin join, float limit, PathCap cap, float tolerance, bool closed)
        {
            if (points.Count < 2) return;
            float radius = width * .5f;
            float outside = width * Mathf.Clamp01(alignment), inside = width - outside;
            Vector2 startDirection = (points[1] - points[0]).normalized;
            Vector2 endDirection = (points[points.Count - 1] - points[points.Count - 2]).normalized;
            // Union finite edge strips and outer join sectors. Intersecting infinite inner
            // offset lines creates arbitrarily long spikes at short, concave curve segments.
            int edgeCount = closed ? points.Count : points.Count - 1;
            for (int index = 0; index < edgeCount; index++)
            {
                Vector2 a = points[index], b = points[(index + 1) % points.Count];
                Vector2 direction = (b - a).normalized;
                if (direction.sqrMagnitude < .5f) continue;
                Vector2 normal = new Vector2(direction.y, -direction.x);
                if (!closed && cap == PathCap.Square && index == 0) a -= direction * radius;
                if (!closed && cap == PathCap.Square && index == points.Count - 2) b += direction * radius;
                polygons.Add(new[] { a + normal * outside, b + normal * outside, b - normal * inside, a - normal * inside });
            }
            for (int index = closed ? 0 : 1; index < (closed ? points.Count : points.Count - 1); index++)
            {
                Vector2 previous = (points[index] - points[(index + points.Count - 1) % points.Count]).normalized;
                Vector2 next = (points[(index + 1) % points.Count] - points[index]).normalized;
                float turn = Cross(previous, next);
                if (Mathf.Abs(turn) < 1e-6f) continue;
                float offset = turn > 0 ? outside : -inside;
                if (Mathf.Abs(offset) < 1e-8f) continue;
                List<Vector2> sector = new List<Vector2> { points[index], points[index] + new Vector2(previous.y, -previous.x) * offset };
                AppendJoin(sector, points[index], previous, next, offset, join, limit, tolerance);
                CurveSegment.AppendDistinct(sector, points[index] + new Vector2(next.y, -next.x) * offset);
                if (PolygonGeometry.SignedArea(sector) < 0) sector.Reverse();
                polygons.Add(sector.ToArray());
            }
            if (closed || cap != PathCap.Round) return;
            Vector2 start = points[0] + new Vector2(startDirection.y, -startDirection.x) * (outside - inside) * .5f;
            Vector2 end = points[points.Count - 1] + new Vector2(endDirection.y, -endDirection.x) * (outside - inside) * .5f;
            List<Vector2> firstCap = new List<Vector2> { start };
            AppendCap(firstCap, start, -startDirection, radius, cap, tolerance);
            List<Vector2> lastCap = new List<Vector2> { end };
            AppendCap(lastCap, end, endDirection, radius, cap, tolerance);
            polygons.Add(firstCap.ToArray()); polygons.Add(lastCap.ToArray());
        }

        static void AppendCap(List<Vector2> output, Vector2 point, Vector2 direction, float radius, PathCap cap, float tolerance)
        {
            Vector2 normal = new Vector2(direction.y, -direction.x);
            int count = cap == PathCap.Round ? Mathf.Clamp(Mathf.CeilToInt(Mathf.PI / Mathf.Max(.01f, 2f * Mathf.Acos(Mathf.Clamp(1f - tolerance / radius, -1f, 1f)))), 2, 512) : 1;
            for (int index = 0; index <= count; index++)
            {
                float angle = Mathf.PI * index / count;
                CurveSegment.AppendDistinct(output, point + radius * (normal * Mathf.Cos(angle) + direction * Mathf.Sin(angle)));
            }
        }

        static float Cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;
        static void Validate(float width, float limit, float tolerance)
        {
            if (!float.IsFinite(width) || width < 0 || !float.IsFinite(limit) || limit < 1 || !float.IsFinite(tolerance) || tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Stroke width, miter limit, and tolerance must be finite and within their supported ranges.");
        }
    }

    /// <summary>Detached closed contours and arc lengths, reusable across stroke widths, joins, and dash phases.</summary>
    public sealed class MeasuredContourSet
    {
        internal readonly Vector2[][] Points;
        internal readonly PathMetric[] Metrics;
        public int Count => Points.Length;
        public long PayloadBytes { get; }
        public ReadOnlySpan<Vector2> GetContour(int index) => Points[index];

        public MeasuredContourSet(IReadOnlyList<Vector2[]> contours)
        {
            if (contours == null) throw new ArgumentNullException(nameof(contours));
            Points = new Vector2[contours.Count][];
            Metrics = new PathMetric[contours.Count];
            long bytes = 64 + contours.Count * 16L;
            long pointCount = 0;
            for (int index = 0; index < contours.Count; index++)
            {
                Vector2[] contour = contours[index] ?? throw new ArgumentException("A contour must not be null.", nameof(contours));
                pointCount += contour.Length;
                if (pointCount > PolygonGeometry.MaximumVertices) throw new InvalidOperationException("Measured contours exceeded their vertex budget.");
                Points[index] = (Vector2[])contour.Clone();
                Metrics[index] = contour.Length >= 2 ? new PathMetric(Points[index], true, false) : null;
                bytes += 24 + contour.LongLength * 8 + (Metrics[index]?.MetricBytes ?? 0);
            }
            PayloadBytes = bytes;
        }
    }

    public sealed class PathMetric
    {
        readonly Vector2[] _points;
        readonly double[] _distance;
        public double Length => _distance[_distance.Length - 1];
        internal long MetricBytes => 40 + _distance.LongLength * 8;

        public PathMetric(IReadOnlyList<Vector2> points, bool closed)
            : this(Copy(points), closed, false) { }

        internal PathMetric(Vector2[] points, bool closed, bool copy)
        {
            if (points == null || points.Length < 2 || points.Length > PolygonGeometry.MaximumVertices)
                throw new ArgumentException("A measured path needs at least two points within the vertex budget.");
            foreach (Vector2 point in points)
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y)) throw new ArgumentException("Measured path coordinates must be finite.");
            _points = copy ? (Vector2[])points.Clone() : points;
            _distance = new double[closed ? points.Length + 1 : points.Length];
            for (int index = 1; index < _distance.Length; index++)
                _distance[index] = _distance[index - 1] + Vector2.Distance(_points[index - 1], _points[index % points.Length]);
        }

        static Vector2[] Copy(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2 || points.Count > PolygonGeometry.MaximumVertices)
                throw new ArgumentException("A measured path needs at least two points within the vertex budget.");
            Vector2[] result = new Vector2[points.Count];
            for (int index = 0; index < points.Count; index++) result[index] = points[index];
            return result;
        }

        public Vector2 Sample(double distance, out Vector2 tangent)
        {
            double value = Math.Max(0, Math.Min(Length, distance));
            int edge = Array.BinarySearch(_distance, value);
            edge = edge >= 0 ? Math.Min(edge, _distance.Length - 2) : Math.Max(0, ~edge - 1);
            Vector2 a = _points[edge];
            Vector2 b = _points[(edge + 1) % _points.Length];
            tangent = (b - a).normalized;
            double span = _distance[edge + 1] - _distance[edge];
            return Vector2.LerpUnclamped(a, b, span > 0 ? (float)((value - _distance[edge]) / span) : 0);
        }

        public Vector2[] Slice(double start, double end)
        {
            List<Vector2> points = new List<Vector2> { Sample(start, out _) };
            int first = Array.BinarySearch(_distance, start);
            first = first >= 0 ? first + 1 : ~first;
            for (int index = Math.Max(1, first); index < _distance.Length - 1 && _distance[index] < end; index++)
                if (_distance[index] > start) CurveSegment.AppendDistinct(points, _points[index % _points.Length]);
            CurveSegment.AppendDistinct(points, Sample(end, out _));
            return points.ToArray();
        }
    }
}
