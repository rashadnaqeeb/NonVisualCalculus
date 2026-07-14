using Snv = System.Numerics.Vector3;
using UVec = UnityEngine.Vector3;

namespace NonVisualCalculus.Module.World
{
    /// <summary>The one place Unity and System.Numerics vectors convert, at the Module/Core boundary, so no
    /// Unity type crosses into the engine-free Core world layer.
    ///
    /// The X and Z ground axes are negated across the boundary (Y, elevation, is untouched), rotating the
    /// Core world frame 180 degrees about Y from Unity's. Disco's isometric camera is fixed and yawed 180
    /// degrees from a default Unity camera, so Unity world +Z points toward the viewer (screen-down) and +X
    /// to screen-left; the accessibility convention is camera-up = north, screen-right = east. The negation
    /// makes Core's north (+Z) resolve to Unity -Z (screen-up) and Core's east (+X) to Unity -X
    /// (screen-right), so spoken bearings, cursor glide, and audio placement all match what a sighted player
    /// sees. Both directions negate identically, so they are inverses and positions round-trip exactly; the
    /// map being linear (no translation), direction vectors transform the same way as positions.</summary>
    internal static class WorldConvert
    {
        public static Snv ToSnv(UVec v) => new Snv(-v.x, v.y, -v.z);
        public static UVec ToUnity(Snv v) => new UVec(-v.X, v.Y, -v.Z);
    }
}
