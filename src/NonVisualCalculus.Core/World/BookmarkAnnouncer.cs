using NonVisualCalculus.Core.Text;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// Composes a bookmark's spoken row line: the name first (the distinguishing part), the walking
    /// distance from where the character stands, "preset" on a built-in entry (it explains the missing
    /// delete column), then the route when it is not a plain walk - "detour" when the spot is reached
    /// only by walking beside one of the game's self-opening passages first, "can't reach" when no path
    /// connects them - heard before activating, so the player never commits to a walk the mod already
    /// knows will refuse, and knows when one will head away first.
    /// </summary>
    public static class BookmarkAnnouncer
    {
        public static string Compose(string name, int meters, WalkRoute route, bool preset = false)
            => SpokenLine.Join(name, Strings.Strings.WorldDistance(meters),
                               preset ? Strings.Strings.BookmarkPresetMark : null,
                               RouteWord(route));

        private static string? RouteWord(WalkRoute route) => route switch
        {
            WalkRoute.Detour => Strings.Strings.WorldDetour,
            WalkRoute.None => Strings.Strings.WorldUnreachable(null),
            _ => null,
        };
    }
}
