using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NonVisualCalculus.Core.Descriptions;
using NonVisualCalculus.Core.World.Zoning;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class DescriptionsTests
    {
        private const string Geometry = @"
# the Whirling's ground floor
zone cafe 1.0 0 -2.5
zone kitchen -0.8 0 6.5
detail mosaic 2.0 0 -1.0
object pinball = Pinball Machine Broken
wall @0 -3,2 3,2
merge 1,0,-2 2,0,-1
";

        private const string Prose = @"
zone.cafe.title = the café
zone.cafe.body = A high room of chequered tiles.
zone.cafe.body@TRIBUNAL:post = A high room of chequered tiles, the floor scarred by gunfire.
zone.kitchen.title = the kitchen
detail.mosaic.title = floor mosaic
detail.mosaic.body = An eight-pointed star of green and gold tiles.
object.pinball = A pinball machine on its side, glass cracked.
";

        [Fact]
        public void GeometryParsesEveryDirective()
        {
            var g = SceneGeometry.Parse(Geometry, out var errors);
            Assert.Empty(errors);
            Assert.Equal(2, g.Zones.Count);
            Assert.Equal("kitchen", g.Zones[1].Key);
            Assert.Equal(new Vector3(-0.8f, 0f, 6.5f), g.Zones[1].Position);
            Assert.Single(g.Details);
            Assert.Equal("Pinball Machine Broken", g.Objects.Single().EntityName);
            Assert.Single(g.Curation.Walls);
            Assert.Equal(0f, g.Curation.Walls[0].Elevation);
            Assert.Equal(2, g.Curation.Walls[0].Points.Count);
            Assert.Single(g.Curation.Merges);
        }

        [Fact]
        public void GeometryReportsBadLinesAndKeepsTheRest()
        {
            var g = SceneGeometry.Parse("zone a 1 2\nbogus 1 2 3\nzone b 1 2 3", out var errors);
            Assert.Equal(2, errors.Count);
            Assert.Contains("line 1", errors[0]);
            Assert.Single(g.Zones);
        }

        [Fact]
        public void ProseResolvesStoryStateVariantsOverTheBaseLine()
        {
            var p = SceneProse.Parse(Prose, out var errors);
            Assert.Empty(errors);
            Assert.Equal("A high room of chequered tiles.", p.Get("zone.cafe.body", _ => "pre"));
            Assert.Equal("A high room of chequered tiles.", p.Get("zone.cafe.body", _ => null));
            Assert.Contains("gunfire", p.Get("zone.cafe.body", loc => loc == "TRIBUNAL" ? "post" : null));
            Assert.Null(p.Get("zone.nowhere.body", _ => null));
            Assert.Contains("zone.cafe.body", p.Keys);
            Assert.DoesNotContain("zone.cafe.body@TRIBUNAL:post", p.Keys);
        }

        private static SceneDescriptions Set(string prose = Prose, string english = Prose)
            => new SceneDescriptions("Test", SceneGeometry.Parse(Geometry, out _),
                SceneProse.Parse(prose, out _), SceneProse.Parse(english, out _), _ => null);

        private static ZoneMap TwoZoneMap()
        {
            // 8 by 8 m at z -6.5..1.5 and 8 by 8 at z 2.5..10.5, joined by a doorway at x 0.
            var r = new WalkRaster(34, 70, 0.25f, -4.25f, -6.75f);
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                {
                    float x = r.CenterX(gx), z = r.CenterZ(gz);
                    bool south = x > -4f && x < 4f && z > -6.5f && z < 1.5f;
                    bool north = x > -4f && x < 4f && z > 2.5f && z < 10.5f;
                    bool door = x > -0.5f && x < 0.5f && z >= 1.5f && z <= 2.5f;
                    if (south || north || door) r.Set(gx, gz, 0f);
                }
            return ZoneMap.Build(r, new ZoneMapOptions(), ZoneCuration.None);
        }

        [Fact]
        public void ZoneTitlesResolveThroughTheLiveMapNotStoredIds()
        {
            var map = TwoZoneMap();
            Assert.Equal(2, map.Zones.Count);
            var set = Set();
            var south = map.ZoneAt(new Vector3(1f, 0f, -2.5f))!;
            var north = map.ZoneAt(new Vector3(-0.8f, 0f, 6.5f))!;
            Assert.Equal("the café", set.ZoneTitle(map, south));
            Assert.Equal("the kitchen", set.ZoneTitle(map, north));
            Assert.Equal("the café", ZoneSpeech.Name(set, map, south));
            Assert.Equal("the café. A high room of chequered tiles.", ZoneSpeech.Describe(set, map, south, south.Centroid));
            Assert.Equal("the kitchen. nothing described", ZoneSpeech.Describe(set, map, north, north.Centroid)); // title but no body
        }

        [Fact]
        public void UnnamedZoneFallsBackToNumberAndClass()
        {
            var map = TwoZoneMap();
            var south = map.ZoneAt(new Vector3(1f, 0f, -2.5f))!;
            Assert.Equal("zone " + south.Id + ", room", ZoneSpeech.Name(null, map, south));
        }

        [Fact]
        public void EnglishIsTheFallbackPerKey()
        {
            var set = Set(prose: "zone.cafe.title = le café\n");
            var map = TwoZoneMap();
            var south = map.ZoneAt(new Vector3(1f, 0f, -2.5f))!;
            Assert.Equal("le café", set.ZoneTitle(map, south));
            Assert.Contains("chequered", set.ZoneBody(map, south, south.Centroid));
        }

        [Fact]
        public void ObjectAndDetailTextByKey()
        {
            var set = Set();
            Assert.Contains("pinball", set.ObjectText("Pinball Machine Broken"));
            Assert.Null(set.ObjectText("Pinball Machine"));
            Assert.Equal("floor mosaic", set.DetailTitle("mosaic"));
            Assert.Contains("eight-pointed", set.DetailBody("mosaic"));
        }

        // ---- the committed description files ----

        private static string? RepoDir()
        {
            DirectoryInfo? dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NonVisualCalculus.slnx")))
                dir = dir.Parent;
            return dir?.FullName;
        }

        /// <summary>The offline validator: every committed scene's geometry parses, every anchor, detail and
        /// object key has English prose (a title for zones and details, a body for objects), prose keys
        /// address something in the geometry, and no key repeats.</summary>
        [Fact]
        public void CommittedDescriptionsAreComplete()
        {
            string? repo = RepoDir();
            Assert.NotNull(repo);
            string dir = Path.Combine(repo!, "descriptions");
            if (!Directory.Exists(dir)) return;
            var problems = new List<string>();
            foreach (string geomPath in Directory.GetFiles(dir, "*.txt"))
            {
                string stem = Path.GetFileNameWithoutExtension(geomPath);
                if (stem.Contains('.')) continue; // a prose file
                var g = SceneGeometry.Parse(File.ReadAllText(geomPath), out var errors);
                foreach (var e in errors) problems.Add(stem + ": " + e);
                string prosePath = Path.Combine(dir, stem + ".en.txt");
                if (!File.Exists(prosePath)) { problems.Add(stem + ": no English prose file"); continue; }
                string proseText = File.ReadAllText(prosePath);
                var prose = SceneProse.Parse(proseText, out var proseErrors);
                foreach (var e in proseErrors) problems.Add(stem + ".en: " + e);
                var used = new HashSet<string>();
                void Need(string key)
                {
                    used.Add(key);
                    if (!prose.Has(key)) problems.Add(stem + ": missing prose " + key);
                }
                var seenKeys = new HashSet<string>();
                foreach (var z in g.Zones)
                {
                    if (!seenKeys.Add("zone." + z.Key)) problems.Add(stem + ": duplicate zone key " + z.Key);
                    Need("zone." + z.Key + ".title");
                    used.Add("zone." + z.Key + ".body");
                }
                foreach (var d in g.Details)
                {
                    if (!seenKeys.Add("detail." + d.Key)) problems.Add(stem + ": duplicate detail key " + d.Key);
                    Need("detail." + d.Key + ".title");
                    Need("detail." + d.Key + ".body");
                }
                foreach (var o in g.Objects)
                {
                    if (!seenKeys.Add("object." + o.Key)) problems.Add(stem + ": duplicate object key " + o.Key);
                    Need("object." + o.Key);
                }
                foreach (string key in prose.Keys)
                    if (!used.Contains(key)) problems.Add(stem + ": orphan prose " + key);
                // A duplicate line in the prose file silently overrides: catch it on the raw text.
                var rawKeys = new HashSet<string>();
                foreach (string line in proseText.Replace("\r\n", "\n").Split('\n'))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || line.TrimStart().StartsWith("#")) continue;
                    string k = line.Substring(0, eq).Trim();
                    if (!rawKeys.Add(k)) problems.Add(stem + ".en: duplicate line " + k);
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

    [Fact]
    public void ObjectTextMatchesAnEntityNameWithATrailingSpace()
    {
        var geometry = SceneGeometry.Parse("object crate = Pier Crate \n", out _);
        var prose = SceneProse.Parse("object.crate = A crate.\n", out _);
        var d = new SceneDescriptions("S", geometry, prose, prose, _ => null);
        Assert.Equal("A crate.", d.ObjectText("Pier Crate "));
        Assert.Equal("A crate.", d.ObjectText("Pier Crate"));
    }

    }
}
