using System.Numerics;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// What a system is handed when asked to announce: the cursor point, the player reference (origin for
    /// bearing/distance), the <see cref="AnnouncementContext"/> requested (so a system can skip contexts it
    /// doesn't serve), and the owning overlay (so one system can read a sibling — one system per type makes
    /// that lookup deterministic via <see cref="Overlay.Get{T}"/>).
    /// </summary>
    public sealed class OverlayContext
    {
        public Overlay Overlay { get; }
        public Vector3 Cursor { get; }
        public Vector3 Reference { get; }
        public AnnouncementContext Want { get; }

        public OverlayContext(Overlay overlay, Vector3 cursor, Vector3 reference, AnnouncementContext want)
        {
            Overlay = overlay;
            Cursor = cursor;
            Reference = reference;
            Want = want;
        }

        public T? System<T>() where T : OverlaySystem => Overlay.Get<T>();
    }
}
