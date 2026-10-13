using System.Globalization;
using System.Text;

namespace NonVisualCalculus.Core.World.Zoning
{
    /// <summary>
    /// The text form of a zone map for the offline floor-plan renderer (tools/zones_render.py): one
    /// header line "width height cell originX originZ sceneName", the per-cell zone labels as CSV (-1 off
    /// the ground), the per-cell elevations as CSV, then one "Z id|class|area|cx|cy|cz" line per zone and
    /// one "E fromId|toId|x|y|z" line per exit. Invariant culture throughout, so the file reads the same
    /// on any machine.
    /// </summary>
    public static class ZoneDump
    {
        public static string Write(ZoneMap map, string sceneName)
        {
            var inv = CultureInfo.InvariantCulture;
            var r = map.Raster;
            var sb = new StringBuilder();
            sb.Append(r.Width).Append(' ').Append(r.Height).Append(' ')
              .Append(r.Cell.ToString("0.###", inv)).Append(' ')
              .Append(r.OriginX.ToString("0.###", inv)).Append(' ')
              .Append(r.OriginZ.ToString("0.###", inv)).Append(' ')
              .Append(sceneName).Append('\n');
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                    sb.Append(map.LabelAt(gx, gz)).Append(',');
            sb.Append('\n');
            for (int i = 0; i < r.Count; i++)
                sb.Append(r.Elevation[i].ToString("0.0", inv)).Append(',');
            sb.Append('\n');
            foreach (var z in map.Zones)
            {
                sb.Append("Z ").Append(z.Id).Append('|').Append(z.Class.ToString().ToLowerInvariant()).Append('|')
                  .Append(z.Area.ToString("0", inv)).Append('|')
                  .Append(z.Centroid.X.ToString("0.0", inv)).Append('|')
                  .Append(z.Centroid.Y.ToString("0.0", inv)).Append('|')
                  .Append(z.Centroid.Z.ToString("0.0", inv)).Append('\n');
                foreach (var e in z.Exits)
                    sb.Append("E ").Append(z.Id).Append('|').Append(e.To.Id).Append('|')
                      .Append(e.Position.X.ToString("0.0", inv)).Append('|')
                      .Append(e.Position.Y.ToString("0.0", inv)).Append('|')
                      .Append(e.Position.Z.ToString("0.0", inv)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
