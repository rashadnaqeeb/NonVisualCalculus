namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// How the character can walk to a point from where they stand: a complete path, a two-leg detour
    /// through one of the game's self-opening passages (walk beside the gate first, then on), or no way
    /// at all. Spoken before committing a walk so the player knows what the walk will do.
    /// </summary>
    public enum WalkRoute
    {
        Direct,
        Detour,
        None,
    }
}
