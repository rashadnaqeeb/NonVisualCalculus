using DiscoAccess.Core.Strings;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused save/load list entry from its <see cref="SaveEntryState"/>.
    /// Order follows the house style: the "new save" marker first when this is the create-new slot (the
    /// one fact that changes what activating it does), then the save name (the distinguishing word a
    /// navigator scans for), then its date and time. The name, date, and time are the game's own text
    /// spoken verbatim; only the new-save marker is mod-authored.
    /// </summary>
    public static class SaveEntryAnnouncer
    {
        public static string Compose(SaveEntryState s)
            => Text.SpokenLine.Join(s.IsNew ? Strings.Strings.StatusNewSave : null, s.Name, s.Date, s.Time);
    }
}
