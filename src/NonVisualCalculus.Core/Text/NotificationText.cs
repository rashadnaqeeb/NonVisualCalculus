namespace NonVisualCalculus.Core.Text
{
    /// <summary>
    /// Composes the spoken form of a game pop-up notification from its two live text parts: the header
    /// (the title, e.g. the localized "MONEY" or "ITEM GAINED") and the description (the detail, e.g. the
    /// formatted "+5 réal" or an item name). The game styles both ALL-CAPS for display, which a screen
    /// reader voices acceptably as words, so the text is read faithfully rather than recased (recasing
    /// would mangle proper nouns the description often carries). Pure and unit-tested; the module reads the
    /// live <c>Notification</c> and hands these two strings here.
    /// </summary>
    public static class NotificationText
    {
        /// <summary>Title then detail, each cleaned, joined with a comma. Either part may be empty (a
        /// header-only notification carries no description); when one is empty the other stands alone, and
        /// a description that merely repeats the header is not read twice.</summary>
        public static string Compose(string? header, string? description)
        {
            string h = TextFilter.Clean(header);
            string d = TextFilter.Clean(description);
            if (string.IsNullOrEmpty(h)) return d;
            if (string.IsNullOrEmpty(d)) return h;
            if (string.Equals(h, d, System.StringComparison.OrdinalIgnoreCase)) return h;
            return h + ", " + d;
        }
    }
}
