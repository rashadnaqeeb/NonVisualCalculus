namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// Tracks whether the player is actively driving the cursor - the movement keys held - with a short
    /// linger so the signal reads as "moving" smoothly across brief releases instead of flickering. Held
    /// keys count even when the cursor cannot advance (pinned against a wall or the frame edge); a cursor
    /// position change with no keys held (a recenter, a reposition reset, the frame-drag as the character
    /// walks) never counts, so the <see cref="PlayMode.WhenMoving"/> systems sound only for deliberate
    /// movement. Ported from the WOTR exploration mod.
    /// </summary>
    public sealed class MotionTracker
    {
        public const float LingerSec = 0.25f;

        private float _linger;

        public bool MovingRecently { get; private set; }

        /// <summary><paramref name="moving"/> = the player is holding the movement keys this frame.</summary>
        public void Update(float dt, bool moving)
        {
            if (moving) _linger = LingerSec;
            else _linger -= dt;
            MovingRecently = _linger > 0f;
        }

        public void Reset()
        {
            _linger = 0f;
            MovingRecently = false;
        }
    }
}
