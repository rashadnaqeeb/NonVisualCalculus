using System;
using System.Numerics;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// The overlay's point of attention: a world position the sensing systems describe, moved by a freeform
    /// glide. Until something sets it, it reads the player's position (so a cold read and a recenter both
    /// land on the player). Movement is navmesh-clamped through the <see cref="IWorldEnvironment"/> so the
    /// cursor can't leave walkable ground. Disco has only this one freeform mode, so there is no separate
    /// movement-mode abstraction.
    /// </summary>
    public sealed class Cursor
    {
        private readonly IWorldEnvironment _env;
        private Vector3 _pos;
        private bool _has;

        public Cursor(IWorldEnvironment env)
        {
            _env = env;
        }

        /// <summary>The cursor's world point; falls back to the player's position until set.</summary>
        public Vector3 Position
        {
            get => _has ? _pos : _env.PlayerPosition;
            set { _pos = value; _has = true; }
        }

        /// <summary>The player's live position (the readout origin).</summary>
        public Vector3 PlayerPosition => _env.PlayerPosition;

        /// <summary>Snap the cursor back onto the player.</summary>
        public void Recenter()
        {
            _pos = _env.PlayerPosition;
            _has = true;
        }

        /// <summary>Glide one frame toward direction (<paramref name="dx"/>, <paramref name="dz"/>) on the
        /// XZ plane at <paramref name="speed"/> metres/second, navmesh-clamped. The direction need not be
        /// normalized (a held diagonal is fine); a zero direction is a no-op.</summary>
        public void Glide(float dx, float dz, float dt, float speed)
        {
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6f) return;
            var cur = Position;
            float step = speed * dt / len;
            var intended = new Vector3(cur.X + dx * step, cur.Y, cur.Z + dz * step);
            Position = _env.TraceMove(cur, intended);
        }
    }
}
