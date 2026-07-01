using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using Snv = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// Names the map district for the two location readouts, modelled on wotr-access's room tracker: the
    /// auto-announce speaks the sub-district the instant the point of attention crosses into a different one
    /// ("Harbour"), and the 'r' key reads the full location, the map name plus the sub-district ("Martinaise,
    /// Harbour"). The reference point is the overlay cursor, which reads the player's position until the cursor
    /// is glided away, so the readout follows the player while walking and the cursor while reviewing.
    ///
    /// The exterior has no runtime notion of sub-districts - the game names whole scenes only - so we author
    /// the partition as labelled anchor points and take the nearest as the district: a Voronoi tiling, so
    /// every walkable point belongs to exactly one district with no overlaps or gaps.
    ///
    /// An elevated micro-district that stacks over a ground district on the same XZ can't be a Voronoi
    /// anchor - a ground point directly below is nearest to it, so it would read up on the platform while
    /// standing beneath it. Such a district is authored instead as a bounded box (an XZ rectangle plus a Y
    /// band), checked before the Voronoi and measured off the navmesh, so it reads only when the reference
    /// is actually up on the platform and nowhere else. The Whirling balcony and the Capeside apartment
    /// balcony and roof are all boxed this way, so every anchor left in the Voronoi is ground-level and the
    /// nearest-anchor test is a flat XZ distance.
    ///
    /// The anchor coordinates are a rough first pass, tuned live by hot-reload during a full playthrough; the
    /// district names are settled and live in <see cref="Strings"/>.
    /// </summary>
    internal sealed class DistrictReader
    {
        // Only Martinaise-ext has authored sub-districts; other scenes read just their map name.
        private const string Scene = "Martinaise-ext";

        // A ground-level district anchor, taken as the district of the nearest anchor by flat XZ distance.
        private readonly struct Anchor
        {
            public readonly string Name;
            public readonly float X, Z;
            public Anchor(string name, float x, float z) { Name = name; X = x; Z = z; }
        }

        // A precisely-bounded elevated micro-district: an axis-aligned XZ rectangle plus a Y band. Checked
        // before the Voronoi so a raised platform reads only when the reference is inside it - never from the
        // ground on the same XZ beneath it, never anywhere else in the city.
        private readonly struct Region
        {
            public readonly string Name;
            public readonly float MinX, MaxX, MinZ, MaxZ, MinY, MaxY;
            public Region(string name, float minX, float maxX, float minZ, float maxZ, float minY, float maxY)
            { Name = name; MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ; MinY = minY; MaxY = maxY; }
            public bool Contains(Snv p) =>
                p.X >= MinX && p.X <= MaxX && p.Z >= MinZ && p.Z <= MaxZ && p.Y >= MinY && p.Y <= MaxY;
        }

        // Bounded elevated micro-districts, checked before the anchor Voronoi. Coordinates are measured live
        // off the navmesh (a flood-fill of the connected platform), padded slightly with a Y band that clears
        // the ground below.
        private static readonly Region[] Regions =
        {
            // Whirling balcony: navmesh footprint X[-27.3,-19.7] Z[-87.0,-79.6] at Y 7.43.
            new Region(Strings.DistrictWhirlingBalcony, -28f, -19f, -88f, -79f, 5.5f, 9.5f),
            // Capeside apartment balcony: navmesh footprint X[-26.8,-17.7] Z[-132.0,-128.6] at Y 10.4. The Y
            // floor clears the apartment roof (Y ~8) one level down on adjacent XZ.
            new Region(Strings.DistrictApartmentBalcony, -27f, -17f, -133f, -128f, 9f, 12.5f),
            // Capeside apartment roof: navmesh footprint X[-8.4,4.9] Z[-144.3,-134.0] at Y 7.5. The Y band sits
            // below the apartment balcony (Y 10.4) and above the pier below (Y 1).
            new Region(Strings.DistrictApartmentRoof, -9f, 5f, -145f, -133f, 6f, 9f),
        };

        // Authored ground anchors for Martinaise-ext (world XZ). Several districts carry more than one anchor
        // so their concave shapes classify correctly. Coordinates are rough first-pass and get tuned live
        // during the playthrough.
        private static readonly Anchor[] Anchors =
        {
            new Anchor(Strings.DistrictPlaza, -8, -75), new Anchor(Strings.DistrictPlaza, -16, -72),
            new Anchor(Strings.DistrictYard, -20, -118), new Anchor(Strings.DistrictYard, -19, -128),
            new Anchor(Strings.DistrictTrafficJam, -38, -82), new Anchor(Strings.DistrictTrafficJam, -40, -70),
            new Anchor(Strings.DistrictHarbourGate, -52, -100),
            new Anchor(Strings.DistrictHarbour, -58, -128), new Anchor(Strings.DistrictHarbour, -67, -135),
            new Anchor(Strings.DistrictPier, 0, -130), new Anchor(Strings.DistrictPier, 6, -140),
            new Anchor(Strings.DistrictWaterlock, 10, -40), new Anchor(Strings.DistrictWaterlock, 0, -52),
            new Anchor(Strings.DistrictFishingVillage, 50, -40), new Anchor(Strings.DistrictFishingVillage, 65, -62),
            new Anchor(Strings.DistrictFishingVillage, 55, -80), new Anchor(Strings.DistrictFishingVillage, 72, -66),
            new Anchor(Strings.DistrictIce, 56, -100),
            new Anchor(Strings.DistrictFishMarket, 83, -127),
            new Anchor(Strings.DistrictLandsEnd, 80, -165), new Anchor(Strings.DistrictLandsEnd, 85, -185),
            new Anchor(Strings.DistrictBoardwalk, 115, -70), new Anchor(Strings.DistrictBoardwalk, 120, -95),
            new Anchor(Strings.DistrictBoardwalk, 110, -56), new Anchor(Strings.DistrictBoardwalk, 128, -88),
            new Anchor(Strings.DistrictSeaFortress, 40, -235), new Anchor(Strings.DistrictSeaFortress, 42, -255),
        };

        private readonly IModHost _host;
        private string _announced; // the sub-district last auto-announced (null off-map / not yet resolved)

        public DistrictReader(IModHost host) { _host = host; }

        /// <summary>Auto-announce: read the sub-district at <paramref name="reference"/> (the overlay cursor,
        /// i.e. cursor-else-player) and speak it the instant it differs from the last. A no-op outside the
        /// authored scene, which resets the tracker so re-entry re-announces.</summary>
        public void Tick(Snv reference, string sceneName)
        {
            if (sceneName != Scene) { _announced = null; return; }

            string district = Nearest(reference);
            if (district == _announced) return;

            _announced = district;
            // Ambient orientation, so it queues behind whatever is speaking rather than interrupting.
            _host.Speech.Speak(district, interrupt: false);
        }

        /// <summary>The 'r' readout: speak the full location, the map name and the sub-district when there is
        /// one. An explicit request, so it interrupts.</summary>
        public void ReadLocation(Snv reference, string sceneName)
        {
            string map = MapName(sceneName);
            string subregion = sceneName == Scene ? Nearest(reference) : null;
            _host.Speech.Speak(Strings.WorldLocation(map, subregion), interrupt: true);
        }

        // The map's spoken name: the game's own localized area name with hyphens read as spaces ("Whirling in
        // Rags"), plus the floor word for a numbered interior level so stacked scenes are distinguishable.
        private static string MapName(string sceneName)
        {
            string localized = I2.Loc.LocalizationManager.GetTranslation("Area Names/" + sceneName);
            string map = (string.IsNullOrEmpty(localized) ? sceneName : localized).Replace('-', ' ');
            string floor = FloorLabel(sceneName);
            return floor == null ? map : map + " " + floor;
        }

        // The floor word from a scene id's level suffix: "-f<n>" reads "floor N", "-s<n>" reads "basement"
        // (a shared basement level). Null when the scene carries no level suffix (the exterior, a flat interior).
        private static string FloorLabel(string sceneName)
        {
            foreach (string tok in sceneName.Split('-'))
            {
                if (tok.Length < 2) continue;
                string digits = tok.Substring(1);
                if (!IsDigits(digits)) continue;
                if (tok[0] == 'f') return Strings.WorldFloor + " " + digits;
                if (tok[0] == 's') return Strings.WorldBasement;
            }
            return null;
        }

        private static bool IsDigits(string s)
        {
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        // The district at a point: a bounded elevated micro-district when the point is inside one, else the
        // nearest ground anchor by flat XZ squared distance.
        private static string Nearest(Snv p)
        {
            foreach (var r in Regions)
                if (r.Contains(p)) return r.Name;

            string best = null;
            float bd = float.MaxValue;
            foreach (var a in Anchors)
            {
                float dx = p.X - a.X, dz = p.Z - a.Z;
                float d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = a.Name; }
            }
            return best;
        }
    }
}
