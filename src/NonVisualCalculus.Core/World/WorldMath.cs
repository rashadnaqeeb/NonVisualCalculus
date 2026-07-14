namespace NonVisualCalculus.Core.World
{
    /// <summary>Small numeric helpers shared across the world layer. netstandard2.0 has no
    /// <c>Math.Clamp</c>, so it lives here once rather than being re-rolled per file.</summary>
    internal static class WorldMath
    {
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
