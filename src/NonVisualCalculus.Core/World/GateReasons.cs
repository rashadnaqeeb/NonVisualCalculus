namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// The spoken reason behind each of the game's rule-gated passages: a navmesh blocker the game keeps
    /// enabled while a Lua boolean is false, where no trigger box flips that boolean - the player has to
    /// meet the rule instead (hold the flashlight). The boolean's name is the game's own identity for the
    /// rule, so it is the key; a boolean without an entry here has no known reason and the refusal stays
    /// the plain "can't reach".
    /// </summary>
    public static class GateReasons
    {
        /// <summary>The Lua boolean behind the Doomed Commercial Area's dark rooms, true while the
        /// flashlight is equipped.</summary>
        public const string WieldingFlashlight = "auto.wielding_flashlight";

        public static string? For(string luaBoolean) => luaBoolean switch
        {
            WieldingFlashlight => Strings.Strings.GateNeedsFlashlight,
            _ => null,
        };
    }
}
