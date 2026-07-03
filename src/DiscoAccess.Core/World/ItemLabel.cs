using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The one composition of a thing's spoken label from its resolved name and its live state: a door
    /// standing open reads "door, open" (closed is the default a blind player assumes, so it stays
    /// silent). Shared by the scanner's landing line and the cursor's stop readout, so the two senses
    /// can never disagree about a thing's state.
    /// </summary>
    public static class ItemLabel
    {
        /// <summary>The spoken label: <paramref name="name"/> (the caller's resolved display name,
        /// fallbacks already applied) with the thing's state folded in.</summary>
        public static string For(IWorldItem item, string name)
            => item.IsOpen ? Text.SpokenLine.Join(name, StatusOpen) : name;
    }
}
