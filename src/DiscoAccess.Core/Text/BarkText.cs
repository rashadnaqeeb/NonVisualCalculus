namespace DiscoAccess.Core.Text
{
    /// <summary>
    /// Composes the spoken form of a world bark (the TV, background NPC chatter, a proximity remark) from its
    /// two live parts: the speaker's display name and the bark line. A bark carries a speaker only sometimes
    /// (ambient sources like a television have none), so the name leads with a colon when present and the
    /// line stands alone when not. Both parts are cleaned so dedup and comparison see markup-free text. Pure
    /// and unit-tested; the module reads the live <c>Subtitle</c> and hands these two strings here.
    /// </summary>
    public static class BarkText
    {
        /// <summary>Speaker then line, joined "Name: line"; the line alone when no speaker is named. Empty
        /// when the bark has no line to read (nothing to say).</summary>
        public static string Compose(string? speaker, string? line)
        {
            string l = TextFilter.Clean(line);
            if (string.IsNullOrEmpty(l)) return "";
            string s = TextFilter.Clean(speaker);
            if (string.IsNullOrEmpty(s)) return l;
            return s + ": " + l;
        }
    }
}
