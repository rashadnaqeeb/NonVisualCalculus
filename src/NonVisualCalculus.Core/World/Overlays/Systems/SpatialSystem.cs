using System.Collections.Generic;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World.Overlays.Systems
{
    /// <summary>
    /// The cursor's point readout: on request it describes the exact spot under the cursor (bearing,
    /// distance, height from the player) via <see cref="SpatialReadout"/>. A pure readout with no
    /// movement-timed playback, so it offers only Off/Continuous, not "when moving".
    /// </summary>
    public sealed class SpatialSystem : OverlaySystem
    {
        public override string Name => WorldSystemSpatial;
        public override string Key => "spatial";

        private static readonly PlayMode[] OffContinuous = { PlayMode.Off, PlayMode.Continuous };
        public override IReadOnlyList<PlayMode> SupportedModes => OffContinuous;

        public override IEnumerable<OverlayAnnouncement> Announce(OverlayContext ctx)
        {
            if (!Enabled || ctx.Want != AnnouncementContext.Point) yield break;
            yield return new OverlayAnnouncement(AnnouncementContext.Point, SpatialReadout.Describe(ctx.Reference, ctx.Cursor));
        }
    }
}
