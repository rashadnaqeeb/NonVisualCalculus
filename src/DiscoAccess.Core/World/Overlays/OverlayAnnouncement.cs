namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// One system's contribution to a spoken readout, tagged with the <see cref="AnnouncementContext"/> it
    /// answers. The overlay gathers these from every system for a given request, keeps the ones matching the
    /// requested context, and joins their text into one line.
    /// </summary>
    public sealed class OverlayAnnouncement
    {
        public AnnouncementContext Context { get; }
        public string Text { get; }

        public OverlayAnnouncement(AnnouncementContext context, string text)
        {
            Context = context;
            Text = text;
        }
    }
}
