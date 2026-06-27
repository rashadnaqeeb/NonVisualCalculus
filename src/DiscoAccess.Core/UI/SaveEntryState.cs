namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Unity-free snapshot of a focused save/load list entry, extracted by the module adapter and
    /// composed into speech by <see cref="SaveEntryAnnouncer"/>. The name is the entry's shortened save
    /// name (the game's natural-case form, not the raw on-disk file name); the date and time are the
    /// entry's two timestamp labels with their visual separators stripped. A new, empty save slot has a
    /// name but no timestamp, so both are optional.
    /// </summary>
    public sealed class SaveEntryState
    {
        public string Name { get; }
        public string? Date { get; }
        public string? Time { get; }

        public SaveEntryState(string name, string? date, string? time)
        {
            Name = name;
            Date = date;
            Time = time;
        }
    }
}
