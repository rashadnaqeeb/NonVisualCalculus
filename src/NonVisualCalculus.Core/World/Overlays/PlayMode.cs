namespace NonVisualCalculus.Core.World.Overlays
{
    /// <summary>
    /// How a sensing system decides to play: never (<see cref="Off"/>), only while the cursor is moving
    /// (<see cref="WhenMoving"/>), or always (<see cref="Continuous"/>). One shared vocabulary for every
    /// system. The settings menu may expose only Off/Continuous as a plain on-off and still drive this; the
    /// three-way capability stays in the model so "sonar only while gliding" can be surfaced later without
    /// rework.
    /// </summary>
    public enum PlayMode
    {
        Off,
        WhenMoving,
        Continuous,
    }
}
