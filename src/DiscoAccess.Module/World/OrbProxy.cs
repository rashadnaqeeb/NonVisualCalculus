using DiscoAccess.Core.Strings;
using DiscoAccess.Core.World;
using Vector3 = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The <see cref="IWorldItem"/> over a live <see cref="SenseOrb"/> (a clue/thought orb in the scene). The
    /// spoken name is a short locator word ("orb") so the cursor can announce an orb's position without
    /// dumping its full clue prose; reading that prose (<c>SenseOrb.GetText</c>) is the orb reader's job once
    /// orb interaction lands. A streamed-out orb reads <see cref="OrbType.NONE"/>, so it is not yet visible.
    /// </summary>
    internal sealed class OrbProxy : IWorldItem
    {
        private readonly SenseOrb _orb;

        public OrbProxy(SenseOrb orb) { _orb = orb; }

        public string Name => Strings.WorldThingOrb;
        public Vector3 Position => WorldConvert.ToSnv(_orb.transform.position);
        public ScanBounds Bounds => ScanBounds.Point(Position);
        public string Category => WorldTaxonomy.Orb;

        // Streamed-in orbs (orbType set) are knowable; streamed-out ghosts read NONE with no text.
        public bool IsAccessible => _orb.orbType != OrbType.NONE;
        public bool IsVisible => _orb.orbType != OrbType.NONE;

        // An orb has no interaction stand-point of its own (its clickable lives on the SenseOrb UI, which
        // only activates once the camera is on the orb), so the cursor navigates to the orb's body and orb
        // interaction stays deferred until camera-follow lands. The body is the navigation target; nothing
        // is actionable yet.
        public Vector3 InteractionPoint(Vector3 from) => Position;
        public bool IsActionable(Vector3 from) => false;

        // Orbs are activated through their UI element, not a direct world interaction; deferred.
        public bool Interact() => false;
    }
}
