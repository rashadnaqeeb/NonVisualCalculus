namespace NonVisualCalculus.Core.World.Overlays
{
    /// <summary>
    /// Which kind of readout an announce request asks for, so a system can skip requests it does not serve
    /// and several systems can fold their contributions into one spoken line for the same request. Only the
    /// cursor point readout exists today; further contexts join as readout systems do.
    /// </summary>
    public enum AnnouncementContext
    {
        /// <summary>The exact point under the cursor (direction, distance, height from the player).</summary>
        Point,
    }
}
