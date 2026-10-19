using System.Globalization;
using System.IO;
using System.Text;
using NonVisualCalculus.Core.World.Zoning;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// Dev-server hooks over the live <see cref="ZoneTracker"/> (call through <c>/eval</c>): the zone
    /// table, the zone under the character or the cursor, a text dump for the offline floor-plan renderer
    /// (tools/zones_render.py), and a retune-and-rebuild. Coordinates in and out are the mod's world
    /// frame, the frame every readout and bookmark uses. Public so the REPL can reach it; nothing here
    /// speaks or runs in play.
    /// </summary>
    public static class DevZones
    {
        private static ZoneTracker Tracker => ZoneTracker.Active;

        /// <summary>One line per zone: id, class, area, centroid, exits by destination id.</summary>
        public static string Stats()
        {
            var t = Tracker;
            if (t?.Map == null) return "no zone map" + (t != null ? " (scene " + t.Scene + ")" : "");
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.Append(t.Scene).Append(": ").Append(t.Map.Zones.Count).Append(" zones, raster ")
              .Append(t.Map.Raster.Width).Append('x').Append(t.Map.Raster.Height).Append(" at ")
              .Append(t.Map.Raster.Cell.ToString("0.##", inv)).Append(" m, built in ").Append(t.LastBuildMs).Append(" ms\n");
            foreach (var z in t.Map.Zones)
            {
                sb.Append(z.Id).Append(' ').Append(z.Class).Append(' ').Append(z.Area.ToString("0", inv)).Append(" m2 at (")
                  .Append(z.Centroid.X.ToString("0.0", inv)).Append(',').Append(z.Centroid.Y.ToString("0.0", inv)).Append(',')
                  .Append(z.Centroid.Z.ToString("0.0", inv)).Append(") exits:");
                foreach (var e in z.Exits) sb.Append(' ').Append(e.To.Id);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>The authored anchors against the live map: anchors in no zone, zones with no anchor,
        /// zones holding several. "clean" when every zone has exactly one; the same check the world reader
        /// logs in play, here on demand and whether or not the map has settled.</summary>
        public static string Validate()
        {
            var t = Tracker;
            if (t?.Map == null) return "no zone map" + (t != null ? " (scene " + t.Scene + ")" : "");
            var d = WorldReader.Active?.Descriptions.Current;
            if (d == null || d.Scene != t.Scene) return t.Scene + ": no descriptions";
            string report = Core.Descriptions.ZoneAudit.Run(d.Geometry, t.Map).Report();
            string head = $"{t.Scene}: {t.Map.Zones.Count} zones, {d.Geometry.Zones.Count} anchors, {t.Map.AnchorSplits} split by anchors, {t.Map.AnchorJoins} unnamed joined, {t.Map.AnchorSpecks} specks dropped{(t.Settled ? "" : ", map not settled yet")}{(t.Building ? ", rebuild in flight" : "")}";
            return head + (report.Length == 0 ? ": clean" : "\n" + report.TrimEnd());
        }

        /// <summary>The zone at a point in the mod's world frame.</summary>
        public static string At(float x, float y, float z)
        {
            var zone = Tracker?.ZoneAt(new Snv(x, y, z));
            return zone == null ? "none" : $"zone {zone.Id} {zone.Class} {zone.Area:0} m2";
        }

        /// <summary>The zone under the character and under the cursor.</summary>
        public static string Here()
        {
            var w = WorldReader.Active;
            if (w == null) return "no world reader";
            Snv player = w.PlayerPosition, cursor = w.CursorPosition;
            return $"player {player}: {At(player.X, player.Y, player.Z)}; cursor {cursor}: {At(cursor.X, cursor.Y, cursor.Z)}";
        }

        /// <summary>Write the map's text dump for the renderer; returns the path.</summary>
        public static string Dump(string path = null)
        {
            var t = Tracker;
            if (t?.Map == null) return "no zone map";
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.GetTempPath(), "nvc_zones_" + t.Scene + ".txt");
            File.WriteAllText(path, ZoneDump.Write(t.Map, t.Scene));
            return path;
        }

        /// <summary>Drop the map so the next frame rebuilds it with the current options.</summary>
        public static string Rebuild()
        {
            var t = Tracker;
            if (t == null) return "no tracker";
            t.Rebuild();
            return "rebuilding";
        }

        /// <summary>The live options object, for a dev retune (set fields, then Rebuild).</summary>
        public static ZoneMapOptions Options => Tracker?.Options;
    }
}
