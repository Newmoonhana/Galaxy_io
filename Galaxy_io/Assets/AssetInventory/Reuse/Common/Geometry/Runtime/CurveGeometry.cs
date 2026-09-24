using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImpossibleRobert.Common.Geometry
{
    public enum CurveKind : byte { Line, Quadratic, Cubic }

    /// <summary>An immutable canonical Bezier segment in a caller-defined coordinate space.</summary>
    public readonly struct CurveSegment
    {
        public CurveKind Kind { get; }
        public Vector2 Start { get; }
        public Vector2 Control1 { get; }
        public Vector2 Control2 { get; }
        public Vector2 End { get; }
        public Bounds Bounds
        {
            get
            {
                Vector2 minimum = Vector2.Min(Start, End);
                Vector2 maximum = Vector2.Max(Start, End);
                IncludeExtrema(Start.x, Control1.x, Control2.x, End.x, ref minimum, ref maximum);
                IncludeExtrema(Start.y, Control1.y, Control2.y, End.y, ref minimum, ref maximum);
                return new Bounds((minimum + maximum) * .5f, maximum - minimum);
            }
        }

        void IncludeExtrema(float p0, float p1, float p2, float p3, ref Vector2 minimum, ref Vector2 maximum)
        {
            if (Kind == CurveKind.Line) return;
            if (Kind == CurveKind.Quadratic)
            {
                float denominator = p0 - 2f * p1 + p3;
                if (Mathf.Abs(denominator) > 1e-12f) IncludeAt((p0 - p1) / denominator, ref minimum, ref maximum);
                return;
            }
            float a = -p0 + 3f * p1 - 3f * p2 + p3;
            float b = 2f * (p0 - 2f * p1 + p2);
            float c = p1 - p0;
            if (Mathf.Abs(a) < 1e-12f)
            {
                if (Mathf.Abs(b) > 1e-12f) IncludeAt(-c / b, ref minimum, ref maximum);
                return;
            }
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0) return;
            float root = Mathf.Sqrt(discriminant);
            IncludeAt((-b + root) / (2f * a), ref minimum, ref maximum);
            IncludeAt((-b - root) / (2f * a), ref minimum, ref maximum);
        }

        void IncludeAt(float t, ref Vector2 minimum, ref Vector2 maximum)
        {
            if (t <= 0 || t >= 1) return;
            Vector2 point = Evaluate(t);
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }

        public CurveSegment(CurveKind kind, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
        {
            if (kind < CurveKind.Line || kind > CurveKind.Cubic || !Finite(start) || !Finite(control1) || !Finite(control2) || !Finite(end))
                throw new ArgumentException("Curve kind and coordinates must be valid and finite.");
            Kind = kind;
            Start = start;
            Control1 = control1;
            Control2 = control2;
            End = end;
        }

        static bool Finite(Vector2 point) => float.IsFinite(point.x) && float.IsFinite(point.y);

        public static CurveSegment Line(Vector2 start, Vector2 end) => new CurveSegment(CurveKind.Line, start, start, end, end);
        public static CurveSegment Quadratic(Vector2 start, Vector2 control, Vector2 end) => new CurveSegment(CurveKind.Quadratic, start, control, control, end);
        public static CurveSegment Cubic(Vector2 start, Vector2 first, Vector2 second, Vector2 end) => new CurveSegment(CurveKind.Cubic, start, first, second, end);

        public Vector2 Evaluate(float t)
        {
            float u = 1f - t;
            return Kind switch
            {
                CurveKind.Line => Vector2.LerpUnclamped(Start, End, t),
                CurveKind.Quadratic => u * u * Start + 2f * u * t * Control1 + t * t * End,
                _ => u * u * u * Start + 3f * u * u * t * Control1 + 3f * u * t * t * Control2 + t * t * t * End
            };
        }

        public Vector2 Derivative(float t)
        {
            float u = 1f - t;
            return Kind switch
            {
                CurveKind.Line => End - Start,
                CurveKind.Quadratic => 2f * (u * (Control1 - Start) + t * (End - Control1)),
                _ => 3f * (u * u * (Control1 - Start) + 2f * u * t * (Control2 - Control1) + t * t * (End - Control2))
            };
        }

        public CurveSegment Transform(Matrix4x4 matrix)
        {
            return new CurveSegment(Kind, matrix.MultiplyPoint3x4(Start), matrix.MultiplyPoint3x4(Control1), matrix.MultiplyPoint3x4(Control2), matrix.MultiplyPoint3x4(End));
        }

        public CurveSegment Reverse() => Kind == CurveKind.Quadratic
            ? Quadratic(End, Control1, Start)
            : new CurveSegment(Kind, End, Control2, Control1, Start);

        public void Flatten(List<Vector2> output, float tolerance, int maximumSegments = 65536)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (!float.IsFinite(tolerance) || tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            Subdivide(in this, output, tolerance * tolerance, 0, maximumSegments);
        }

        static void Subdivide(in CurveSegment curve, List<Vector2> output, float toleranceSquared, int depth, int maximum)
        {
            if (output.Count >= maximum) throw new InvalidOperationException("Curve flattening exceeded its segment budget.");
            float error = curve.Kind == CurveKind.Line ? 0 : DistanceToSegmentSquared(curve.Control1, curve.Start, curve.End);
            if (curve.Kind == CurveKind.Cubic)
                error = Mathf.Max(error, DistanceToSegmentSquared(curve.Control2, curve.Start, curve.End));
            if (error <= toleranceSquared)
            {
                AppendDistinct(output, curve.End);
                return;
            }
            if (depth >= 24) throw new InvalidOperationException("Curve tolerance exceeds the supported subdivision precision.");
            Vector2 a = (curve.Start + curve.Control1) * .5f;
            if (curve.Kind == CurveKind.Quadratic)
            {
                Vector2 b = (curve.Control1 + curve.End) * .5f;
                Vector2 mid = (a + b) * .5f;
                CurveSegment left = Quadratic(curve.Start, a, mid);
                CurveSegment right = Quadratic(mid, b, curve.End);
                Subdivide(in left, output, toleranceSquared, depth + 1, maximum);
                Subdivide(in right, output, toleranceSquared, depth + 1, maximum);
            }
            else
            {
                Vector2 b = (curve.Control1 + curve.Control2) * .5f;
                Vector2 c = (curve.Control2 + curve.End) * .5f;
                Vector2 d = (a + b) * .5f;
                Vector2 e = (b + c) * .5f;
                Vector2 mid = (d + e) * .5f;
                CurveSegment left = Cubic(curve.Start, a, d, mid);
                CurveSegment right = Cubic(mid, e, c, curve.End);
                Subdivide(in left, output, toleranceSquared, depth + 1, maximum);
                Subdivide(in right, output, toleranceSquared, depth + 1, maximum);
            }
        }

        internal static void AppendDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 1e-12f)
                points.Add(point);
        }

        public static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 edge = end - start;
            float parameter = edge.sqrMagnitude > 1e-20f ? Mathf.Clamp01(Vector2.Dot(point - start, edge) / edge.sqrMagnitude) : 0;
            return (point - (start + parameter * edge)).sqrMagnitude;
        }
    }

    public sealed class CurveContour
    {
        readonly CurveSegment[] _segments;
        public int Count => _segments.Length;
        public bool Closed { get; }
        public CurveSegment this[int index] => _segments[index];

        public CurveContour(CurveSegment[] segments, bool closed = true)
        {
            _segments = segments != null ? (CurveSegment[])segments.Clone() : Array.Empty<CurveSegment>();
            Closed = closed;
        }

        public Vector2[] Flatten(float tolerance, int maximumSegments = 65536)
        {
            List<Vector2> points = new List<Vector2>();
            if (Count == 0) return Array.Empty<Vector2>();
            points.Add(_segments[0].Start);
            for (int index = 0; index < Count; index++)
                _segments[index].Flatten(points, tolerance, maximumSegments);
            if (Closed && points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= 1e-12f)
                points.RemoveAt(points.Count - 1);
            return points.ToArray();
        }

        public CurveContour Transform(Matrix4x4 matrix)
        {
            CurveSegment[] segments = new CurveSegment[Count];
            for (int index = 0; index < Count; index++) segments[index] = _segments[index].Transform(matrix);
            return new CurveContour(segments, Closed);
        }
    }

    /// <summary>Detached, immutable font geometry before flattening or field generation.</summary>
    public sealed class CurveGlyph
    {
        readonly CurveContour[] _contours;
        internal readonly Vector2[] SourcePoints;
        public int ContourCount => _contours.Length;
        public Bounds Bounds { get; }
        public float Advance { get; }
        public CurveContour GetContour(int index) => _contours[index];
        public long PayloadBytes { get; }

        public CurveGlyph(CurveContour[] contours, Bounds bounds, float advance)
            : this(contours, bounds, advance, Array.Empty<Vector2>()) { }

        internal CurveGlyph(CurveContour[] contours, Bounds bounds, float advance, Vector2[] sourcePoints)
        {
            _contours = contours != null ? (CurveContour[])contours.Clone() : Array.Empty<CurveContour>();
            SourcePoints = sourcePoints;
            Bounds = bounds;
            Advance = advance;
            long bytes = 64L + SourcePoints.LongLength * 8L;
            foreach (CurveContour contour in _contours) bytes += 32L + contour.Count * 36L;
            PayloadBytes = bytes;
        }
    }
}
