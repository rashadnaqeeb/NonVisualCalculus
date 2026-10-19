using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using NonVisualCalculus.Core.World.Zoning;

namespace NonVisualCalculus.Core.Descriptions
{
    /// <summary>A zone's authored anchor: a point on its open floor that resolves to the zone at run time,
    /// and the prose key its title and body live under.</summary>
    public sealed class ZoneAnchor
    {
        public string Key { get; }
        public Vector3 Position { get; }
        public ZoneAnchor(string key, Vector3 position) { Key = key; Position = position; }
    }

    /// <summary>A painted detail: a point with no entity behind it, surfaced as a scan item.</summary>
    public sealed class DetailPoint
    {
        public string Key { get; }
        public Vector3 Position { get; }
        public DetailPoint(string key, Vector3 position) { Key = key; Position = position; }
    }

    /// <summary>An interactable entity with authored appearance text, matched by its exact GameObject name.</summary>
    public sealed class ObjectEntry
    {
        public string Key { get; }
        public string EntityName { get; }
        public ObjectEntry(string key, string entityName) { Key = key; EntityName = entityName; }
    }

    /// <summary>
    /// The geometry half of a scene's description data (descriptions/&lt;scene&gt;.txt): zone anchors,
    /// detail points, object entries, and the zoning curation. Coordinates are the mod's world frame.
    /// No prose lives here; the keys address the scene's prose files. One directive per line:
    /// <code>
    /// zone &lt;key&gt; &lt;x&gt; &lt;y&gt; &lt;z&gt;
    /// detail &lt;key&gt; &lt;x&gt; &lt;y&gt; &lt;z&gt;
    /// object &lt;key&gt; = &lt;entity GameObject name&gt;
    /// wall [@&lt;elevation&gt;] &lt;x&gt;,&lt;z&gt; &lt;x&gt;,&lt;z&gt; ...
    /// merge &lt;x&gt;,&lt;y&gt;,&lt;z&gt; &lt;x&gt;,&lt;y&gt;,&lt;z&gt; ...
    /// drop &lt;x&gt; &lt;y&gt; &lt;z&gt;
    /// </code>
    /// Blank lines and lines starting with # are ignored. Numbers use the invariant culture.
    /// </summary>
    public sealed class SceneGeometry
    {
        public static readonly SceneGeometry Empty = new SceneGeometry(
            new ZoneAnchor[0], new DetailPoint[0], new ObjectEntry[0], ZoneCuration.None);

        public IReadOnlyList<ZoneAnchor> Zones { get; }
        public IReadOnlyList<DetailPoint> Details { get; }
        public IReadOnlyList<ObjectEntry> Objects { get; }
        public ZoneCuration Curation { get; }

        public SceneGeometry(IReadOnlyList<ZoneAnchor> zones, IReadOnlyList<DetailPoint> details,
                             IReadOnlyList<ObjectEntry> objects, ZoneCuration curation)
        {
            Zones = zones;
            Details = details;
            Objects = objects;
            Curation = curation;
        }

        public static SceneGeometry Parse(string text, out List<string> errors)
        {
            errors = new List<string>();
            var zones = new List<ZoneAnchor>();
            var details = new List<DetailPoint>();
            var objects = new List<ObjectEntry>();
            var walls = new List<CurationWall>();
            var merges = new List<IReadOnlyList<Vector3>>();
            var drops = new List<Vector3>();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] tok = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    switch (tok[0])
                    {
                        case "zone":
                            zones.Add(new ZoneAnchor(tok[1], Point3(tok, 2)));
                            break;
                        case "detail":
                            details.Add(new DetailPoint(tok[1], Point3(tok, 2)));
                            break;
                        case "object":
                        {
                            int eq = line.IndexOf('=');
                            if (eq < 0) throw new FormatException("object needs '= <entity name>'");
                            string key = line.Substring("object".Length, eq - "object".Length).Trim();
                            string name = line.Substring(eq + 1).Trim();
                            if (key.Length == 0 || name.Length == 0) throw new FormatException("object needs a key and an entity name");
                            objects.Add(new ObjectEntry(key, name));
                            break;
                        }
                        case "wall":
                        {
                            float? elevation = null;
                            int start = 1;
                            if (tok.Length > 1 && tok[1].StartsWith("@"))
                            {
                                elevation = Num(tok[1].Substring(1));
                                start = 2;
                            }
                            var pts = new List<Vector2>();
                            for (int i = start; i < tok.Length; i++)
                            {
                                string[] xz = tok[i].Split(',');
                                if (xz.Length != 2) throw new FormatException("wall points are x,z pairs");
                                pts.Add(new Vector2(Num(xz[0]), Num(xz[1])));
                            }
                            if (pts.Count < 2) throw new FormatException("a wall needs at least two points");
                            walls.Add(new CurationWall(pts, elevation));
                            break;
                        }
                        case "drop":
                            drops.Add(Point3(tok, 1));
                            break;
                        case "merge":
                        {
                            var pts = new List<Vector3>();
                            for (int i = 1; i < tok.Length; i++)
                            {
                                string[] xyz = tok[i].Split(',');
                                if (xyz.Length != 3) throw new FormatException("merge anchors are x,y,z triples");
                                pts.Add(new Vector3(Num(xyz[0]), Num(xyz[1]), Num(xyz[2])));
                            }
                            if (pts.Count < 2) throw new FormatException("a merge needs at least two anchors");
                            merges.Add(pts);
                            break;
                        }
                        default:
                            throw new FormatException("unknown directive '" + tok[0] + "'");
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException)
                {
                    errors.Add("line " + (n + 1) + ": " + e.Message);
                }
            }
            var seeds = new List<Vector3>();
            foreach (var z in zones) seeds.Add(z.Position);
            return new SceneGeometry(zones, details, objects, new ZoneCuration(walls, merges, drops, seeds));
        }

        private static float Num(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static Vector3 Point3(string[] tok, int at)
        {
            if (tok.Length < at + 3) throw new FormatException("expected x y z");
            return new Vector3(Num(tok[at]), Num(tok[at + 1]), Num(tok[at + 2]));
        }
    }
}
