using System;
using System.Collections.Generic;
using System.Numerics;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// A thing's spatial extent on the XZ plane. <see cref="Center"/> is its middle (where the cursor
    /// snaps); <see cref="NearestPoint"/> is the closest part of it to a reference point, which is what the
    /// spoken distance and bearing measure to, so standing due south of a wide doorway reads "south", not
    /// "west" to its centre. Shapes: a point (default), a circle (a footprint), disjoint segments (a
    /// doorway's portal edges), and a connected polyline. More can be added without touching call sites.
    /// Ported from the WOTR exploration mod onto plain <see cref="Vector3"/> points.
    /// </summary>
    public abstract class ScanBounds
    {
        /// <summary>The thing's middle (cursor target).</summary>
        public abstract Vector3 Center { get; }

        /// <summary>The closest point of the thing to <paramref name="from"/> on the XZ plane; Y carried
        /// from the bounds so above/below still reads. Inside the bounds returns the reference point itself
        /// (distance 0, "here").</summary>
        public abstract Vector3 NearestPoint(Vector3 from);

        public static ScanBounds Point(Vector3 p) => new PointBounds(p);
        public static ScanBounds Circle(Vector3 c, float radius) => new CircleBounds(c, radius);
        /// <summary>Disjoint segments as flat endpoint pairs [a0,b0,a1,b1,...] — e.g. a doorway's portal
        /// edges (the full opening extent). <paramref name="center"/> is the cursor target.</summary>
        public static ScanBounds Segments(Vector3 center, IList<Vector3> edgePairs) => new SegmentsBounds(center, edgePairs);
        /// <summary>A connected chain of one or more points; the closest point lies on its consecutive
        /// segments.</summary>
        public static ScanBounds Polyline(Vector3 center, IList<Vector3> points) => new PolylineBounds(center, points);

        // Closest point on segment a->b to `from`, on the XZ plane (Y lerped along the segment).
        protected static Vector3 ClosestOnSegment(Vector3 from, Vector3 a, Vector3 b)
        {
            float abx = b.X - a.X, abz = b.Z - a.Z;
            float len2 = abx * abx + abz * abz;
            if (len2 < 1e-6f) return a;
            float t = Clamp01(((from.X - a.X) * abx + (from.Z - a.Z) * abz) / len2);
            return new Vector3(a.X + abx * t, Lerp(a.Y, b.Y, t), a.Z + abz * t);
        }

        /// <summary>Closest point on a circle of radius <paramref name="r"/> about
        /// <paramref name="center"/> to <paramref name="from"/> (XZ); inside (or r &lt;= 0) returns the
        /// reference point (distance 0). Non-allocating, so the per-frame lenses can share this geometry with
        /// the spoken path.</summary>
        public static Vector3 NearestOnCircleXZ(Vector3 center, float r, Vector3 from)
        {
            r = Math.Max(0f, r);
            float dx = from.X - center.X, dz = from.Z - center.Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= r || d < 1e-4f) return new Vector3(from.X, center.Y, from.Z);
            float t = r / d;
            return new Vector3(center.X + dx * t, center.Y, center.Z + dz * t);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private sealed class PointBounds : ScanBounds
        {
            private readonly Vector3 _p;
            public PointBounds(Vector3 p) { _p = p; }
            public override Vector3 Center => _p;
            public override Vector3 NearestPoint(Vector3 from) => _p;
        }

        private sealed class CircleBounds : ScanBounds
        {
            private readonly Vector3 _c;
            private readonly float _r;
            public CircleBounds(Vector3 c, float r) { _c = c; _r = Math.Max(0f, r); }
            public override Vector3 Center => _c;
            public override Vector3 NearestPoint(Vector3 from) => NearestOnCircleXZ(_c, _r, from);
        }

        // The closest point over a set of independent segments (each portal edge), so the full opening
        // extent is covered, not just a chord between two midpoints.
        private sealed class SegmentsBounds : ScanBounds
        {
            private readonly Vector3 _center;
            private readonly Vector3[]? _pts; // flat pairs: [a0,b0,a1,b1,...]; null when degenerate
            public SegmentsBounds(Vector3 center, IList<Vector3> pts)
            {
                _center = center;
                if (pts != null && pts.Count >= 2)
                {
                    _pts = new Vector3[pts.Count];
                    for (int i = 0; i < pts.Count; i++) _pts[i] = pts[i];
                }
            }
            public override Vector3 Center => _center;
            public override Vector3 NearestPoint(Vector3 from)
            {
                if (_pts == null) return _center;
                Vector3 best = _center;
                float bestD = float.MaxValue;
                for (int i = 0; i + 1 < _pts.Length; i += 2)
                {
                    var p = ClosestOnSegment(from, _pts[i], _pts[i + 1]);
                    float dx = from.X - p.X, dy = from.Y - p.Y, dz = from.Z - p.Z;
                    float d = dx * dx + dy * dy + dz * dz; // 3D: an opening edge up on a ledge shouldn't win
                    if (d < bestD) { bestD = d; best = p; }
                }
                return best;
            }
        }

        private sealed class PolylineBounds : ScanBounds
        {
            private readonly Vector3 _center;
            private readonly Vector3[] _pts;
            public PolylineBounds(Vector3 center, IList<Vector3> points)
            {
                _center = center;
                _pts = points != null && points.Count > 0 ? new Vector3[points.Count] : new[] { center };
                for (int i = 0; points != null && i < points.Count; i++) _pts[i] = points[i];
            }
            public override Vector3 Center => _center;
            public override Vector3 NearestPoint(Vector3 from)
            {
                if (_pts.Length == 1) return _pts[0];
                Vector3 best = _pts[0];
                float bestD = float.MaxValue;
                for (int i = 0; i + 1 < _pts.Length; i++)
                {
                    var p = ClosestOnSegment(from, _pts[i], _pts[i + 1]);
                    float dx = from.X - p.X, dy = from.Y - p.Y, dz = from.Z - p.Z;
                    float d = dx * dx + dy * dy + dz * dz; // 3D so a vertically-distant chain doesn't win
                    if (d < bestD) { bestD = d; best = p; }
                }
                return best;
            }
        }
    }
}
