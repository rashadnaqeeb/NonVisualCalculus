using System;
using System.Collections.Generic;
using System.Numerics;

namespace NonVisualCalculus.Core.World
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
        /// <summary>An axis-aligned rectangle on the XZ plane about <paramref name="center"/>, with the given
        /// half-widths - the natural footprint from a collider/renderer world AABB. A wide crate reads as its
        /// whole surface, a thin door as its length, so the cursor is "on" the thing when over any part of it.</summary>
        public static ScanBounds Box(Vector3 center, float halfX, float halfZ) => new BoxBounds(center, halfX, halfZ);
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
            float t = WorldMath.Clamp01(((from.X - a.X) * abx + (from.Z - a.Z) * abz) / len2);
            return new Vector3(a.X + abx * t, Lerp(a.Y, b.Y, t), a.Z + abz * t);
        }

        // The closest point of `from` over a run of segments in `pts`: stride 1 walks a connected polyline
        // (consecutive endpoints), stride 2 walks disjoint segments (endpoint pairs). 3D distance so a run up
        // on a ledge doesn't win over one at the reference's level. Returns `fallback` if there is no segment.
        private static Vector3 NearestOverSegments(Vector3 from, Vector3[] pts, int stride, Vector3 fallback)
        {
            Vector3 best = fallback;
            float bestD = float.MaxValue;
            for (int i = 0; i + 1 < pts.Length; i += stride)
            {
                Vector3 p = ClosestOnSegment(from, pts[i], pts[i + 1]);
                float dx = from.X - p.X, dy = from.Y - p.Y, dz = from.Z - p.Z;
                float d = dx * dx + dy * dy + dz * dz;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
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

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private sealed class PointBounds : ScanBounds
        {
            private readonly Vector3 _p;
            public PointBounds(Vector3 p) { _p = p; }
            public override Vector3 Center => _p;
            public override Vector3 NearestPoint(Vector3 from) => _p;
        }

        // An axis-aligned XZ rectangle: the nearest point clamps the reference into the rectangle (inside
        // returns the reference itself, distance 0), Y carried from the centre. A Unity collider/renderer
        // world AABB maps straight onto this.
        private sealed class BoxBounds : ScanBounds
        {
            private readonly Vector3 _c;
            private readonly float _hx, _hz;
            public BoxBounds(Vector3 c, float hx, float hz) { _c = c; _hx = Math.Max(0f, hx); _hz = Math.Max(0f, hz); }
            public override Vector3 Center => _c;
            public override Vector3 NearestPoint(Vector3 from)
            {
                float x = WorldMath.Clamp(from.X, _c.X - _hx, _c.X + _hx);
                float z = WorldMath.Clamp(from.Z, _c.Z - _hz, _c.Z + _hz);
                return new Vector3(x, _c.Y, z);
            }
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
                => _pts == null ? _center : NearestOverSegments(from, _pts, 2, _center);
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
                => _pts.Length == 1 ? _pts[0] : NearestOverSegments(from, _pts, 1, _pts[0]);
        }
    }
}
