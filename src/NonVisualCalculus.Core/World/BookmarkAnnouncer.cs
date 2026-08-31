using NonVisualCalculus.Core.Text;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// Composes a bookmark's spoken row line: the name first (the distinguishing part), the walking
    /// distance from where the character stands, "preset" on a built-in entry (it explains the missing
    /// delete column), and "can't reach" when no path connects them - heard before activating, so the
    /// player never commits to a walk the mod already knows will refuse.
    /// </summary>
    public static class BookmarkAnnouncer
    {
        public static string Compose(string name, int meters, bool reachable, bool preset = false)
            => SpokenLine.Join(name, Strings.Strings.WorldDistance(meters),
                               preset ? Strings.Strings.BookmarkPresetMark : null,
                               reachable ? null : Strings.Strings.WorldUnreachable(null));
    }
}
