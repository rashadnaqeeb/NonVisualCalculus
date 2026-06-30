using System.Numerics;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// Tracks whether the cursor has moved recently, with a short linger so it reads as "moving" smoothly
    /// across key-repeat gaps instead of flickering. Fed the cursor position each frame;
    /// <see cref="MovingRecently"/> stays true for <see cref="LingerSec"/> after the last real move. Drives
    /// the systems' <see cref="PlayMode.WhenMoving"/> gate. Ported from the WOTR exploration mod.
    /// </summary>
    public sealed class MotionTracker
    {
        public const float LingerSec = 0.25f;

        private Vector3 _last;
        private bool _has;
        private float _linger;

        public bool MovingRecently { get; private set; }

        /// <summary><paramref name="intentMoving"/> = the player is actively holding the movement keys even
        /// when the position can't change (the cursor is against a wall), which counts as moving alongside a
        /// real position change.</summary>
        public void Update(Vector3 pos, float dt, bool intentMoving = false)
        {
            bool moved = intentMoving || (_has && (pos - _last).LengthSquared() > 1e-6f);
            if (moved) _linger = LingerSec;
            else _linger -= dt;
            _last = pos;
            _has = true;
            MovingRecently = _linger > 0f;
        }

        public void Reset()
        {
            _has = false;
            _linger = 0f;
            MovingRecently = false;
        }
    }
}
