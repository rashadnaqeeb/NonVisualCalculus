using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NonVisualCalculus.Core.World.Zoning;

namespace NonVisualCalculus.Core.Descriptions
{
    /// <summary>
    /// How a scene's authored zone anchors line up with its live zone map. The map is rebuilt from the
    /// navmesh in whatever state the story and the doors leave it, so an anchor placed against one state
    /// can land in another zone in the next: two anchors sharing a zone leave one title never spoken (the
    /// one nearer the zone's centroid wins), a zone with no anchor speaks the numbered fallback, and an
    /// anchor off the ground names nothing. None of that is visible to a listener, so the module logs any
    /// audit that is not clean and the dev server reports it in full.
    /// </summary>
    public sealed class ZoneAudit
    {
        /// <summary>Anchors that resolve to no zone (off the ground, on another level, or dropped).</summary>
        public List<ZoneAnchor> Unresolved { get; } = new List<ZoneAnchor>();

        /// <summary>Zones no anchor resolves to.</summary>
        public List<Zone> Unnamed { get; } = new List<Zone>();

        /// <summary>Zones two or more anchors resolve to, with those anchors.</summary>
        public List<KeyValuePair<Zone, List<ZoneAnchor>>> Shared { get; } = new List<KeyValuePair<Zone, List<ZoneAnchor>>>();

        public bool Clean => Unresolved.Count == 0 && Unnamed.Count == 0 && Shared.Count == 0;

        public static ZoneAudit Run(SceneGeometry geometry, ZoneMap map)
        {
            var audit = new ZoneAudit();
            var byZone = new Dictionary<Zone, List<ZoneAnchor>>();
            foreach (var a in geometry.Zones)
            {
                var z = map.ZoneAt(a.Position);
                if (z == null) { audit.Unresolved.Add(a); continue; }
                if (!byZone.TryGetValue(z, out var list)) { list = new List<ZoneAnchor>(); byZone[z] = list; }
                list.Add(a);
            }
            foreach (var z in map.Zones)
            {
                if (!byZone.TryGetValue(z, out var list)) audit.Unnamed.Add(z);
                else if (list.Count > 1) audit.Shared.Add(new KeyValuePair<Zone, List<ZoneAnchor>>(z, list));
            }
            return audit;
        }

        /// <summary>One line per problem, for the log and the dev server; empty when clean.</summary>
        public string Report()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var a in Unresolved)
                sb.Append("anchor ").Append(a.Key).Append(" at ").Append(Pt(a.Position.X, a.Position.Y, a.Position.Z, inv)).Append(" is in no zone\n");
            foreach (var z in Unnamed)
                sb.Append("zone ").Append(z.Id).Append(' ').Append(z.Class.ToString().ToLowerInvariant()).Append(' ')
                  .Append(z.Area.ToString("0", inv)).Append(" m2 at ").Append(Pt(z.Centroid.X, z.Centroid.Y, z.Centroid.Z, inv))
                  .Append(" has no anchor\n");
            foreach (var kv in Shared)
            {
                sb.Append("zone ").Append(kv.Key.Id).Append(' ').Append(kv.Key.Area.ToString("0", inv)).Append(" m2 at ")
                  .Append(Pt(kv.Key.Centroid.X, kv.Key.Centroid.Y, kv.Key.Centroid.Z, inv)).Append(" holds anchors");
                foreach (var a in kv.Value) sb.Append(' ').Append(a.Key);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string Pt(float x, float y, float z, CultureInfo inv)
            => "(" + x.ToString("0.0", inv) + "," + y.ToString("0.0", inv) + "," + z.ToString("0.0", inv) + ")";
    }
}
