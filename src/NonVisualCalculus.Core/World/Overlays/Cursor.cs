using System;
using System.Numerics;

namespace NonVisualCalculus.Core.World.Overlays
{
    /// <summary>What stopped a glide, when something did. Wall clamping is not a block (the wall tones
    /// already sound walls); these are the senses' own limits, cued by the overlay's impassable bump.</summary>
    public enum GlideBlock
    {
        /// <summary>Nothing - the stroke moved (or there was no stroke).</summary>
        None,

        /// <summary>The step would leave the camera's visible frame.</summary>
        ViewEdge,

        /// <summary>The step would go deeper into unrevealed fog-of-war ground than the visible fringe.</summary>
        Fog,
    }

    /// <summary>One glide stroke's result: whether the cursor moved, what refused it when it didn't, and
    /// the refused point (for panning the impassable cue toward the direction that was tried).</summary>
    public readonly struct GlideOutcome
    {
        public readonly bool Moved;
        public readonly GlideBlock Block;
        public readonly Vector3 BlockedToward;

        public GlideOutcome(bool moved, GlideBlock block, Vector3 blockedToward)
        {
            Moved = moved;
            Block = block;
            BlockedToward = blockedToward;
        }
    }

    /// <summary>
    /// The overlay's point of attention: a world position the sensing systems describe, moved by a freeform
    /// glide. Until something sets it, it reads the player's position (so a cold read and a recenter both
    /// land on the player). Movement is clamped through the <see cref="IWorldEnvironment"/> three ways: onto
    /// the navmesh (can't leave walkable ground), inside the camera's visible frame (the senses end at the
    /// edge of what's rendered), and no deeper than <see cref="FogFringe"/> into unrevealed fog-of-war
    /// ground. Disco has only this one freeform mode, so there is no separate movement-mode abstraction.
    /// </summary>
    public sealed class Cursor
    {
        /// <summary>How far (metres from the clear ground where it entered) the cursor may roam into
        /// unrevealed fog-of-war ground before the Fog block refuses it. The game's unseen shrouds fade
        /// in over roughly this distance past their volume's edge, so a sighted player sees this much dim
        /// ground into an unentered zone (and the volumes' authored edges overhang revealed doorways by a
        /// few tenths of a metre, which a zero fringe turns into a sealed threshold). Calibrated at the
        /// Whirling room-one doorway: rendered ground reaches full black about two metres past the volume
        /// edge. Measured from the entry point (not travel spent) so a stroke back toward clear ground is
        /// always passable.</summary>
        public const float FogFringe = 2f;

        private readonly IWorldEnvironment _env;
        private Func<bool> _unrestricted = () => false;
        private Vector3 _pos;
        private bool _has;
        private Vector3 _fogEntry; // the last clear ground the cursor stood on (the fringe's centre)

        public Cursor(IWorldEnvironment env)
        {
            _env = env;
        }

        /// <summary>Bind the live unrestricted toggle (a testing aid, off by default): while it reads true,
        /// glides pass the view-edge and fog bounds instead of being refused (navmesh walls still clamp),
        /// and the overlay swaps the impassable bump for the fog enter/exit cues at those crossings. A
        /// provider, not a captured value, so the setting reads live across module reloads.</summary>
        public void BindUnrestricted(Func<bool> provider) => _unrestricted = provider;

        /// <summary>Whether the bound unrestricted toggle reads true right now.</summary>
        public bool Unrestricted => _unrestricted();

        /// <summary>The cursor's world point; falls back to the player's position until set.</summary>
        public Vector3 Position
        {
            get => _has ? _pos : _env.PlayerPosition;
            set { _pos = value; _has = true; }
        }

        /// <summary>Whether the cursor holds its own spot (false = unpinned, riding the player). The
        /// frame-drag clamp applies only to a pinned cursor: an unpinned one is wherever the player is,
        /// and clamping it against a mid-transition camera would pin it somewhere stale.</summary>
        public bool IsPinned => _has;

        /// <summary>The player's live position (the readout origin).</summary>
        public Vector3 PlayerPosition => _env.PlayerPosition;

        /// <summary>Snap the cursor back onto the player.</summary>
        public void Recenter()
        {
            _pos = _env.PlayerPosition;
            _has = true;
        }

        /// <summary>Unpin the cursor so it rides the player's live position again, as on a cold read. Used
        /// on every world exit (a conversation, a menu) and when the character is repositioned out from
        /// under the cursor (a scene load, a save load); the next glide re-pins it.</summary>
        public void Reset()
        {
            _has = false;
        }

        /// <summary>Glide one frame toward direction (<paramref name="dx"/>, <paramref name="dz"/>) on the
        /// XZ plane at <paramref name="speed"/> metres/second, navmesh-clamped and bounded by the visible
        /// frame and the fog of war. The direction need not be normalized (a held diagonal is fine); a zero
        /// direction is a no-op. A diagonal whose full step is refused slides along the boundary on the
        /// passable axis (the same feel as sliding along a wall), silently; only a stroke that cannot move
        /// at all reports its block, so the overlay bumps exactly when the cursor is pinned.</summary>
        public GlideOutcome Glide(float dx, float dz, float dt, float speed)
        {
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6f) return new GlideOutcome(false, GlideBlock.None, default);
            var cur = Position;
            float step = speed * dt / len;

            // Re-base the fringe's centre from the live world every stroke: clear ground under the cursor
            // is the new entry point (so a zone revealed while the cursor sat in its fringe frees the next
            // stroke), and a cursor planted deep in fog by something other than a glide (glides can't pass
            // the fringe) roams from where it stands rather than freezing against a far-off stale entry.
            if (!_env.IsFogged(cur) || PlanarDistance(_fogEntry, cur) > FogFringe + 0.1f)
                _fogEntry = cur;

            // Unrestricted: the view-edge and fog bounds don't refuse (the navmesh trace above still
            // clamps at walls). Reported as unblocked so the overlay never bumps; its fog cues sound the
            // crossings instead.
            var traced = Trace(cur, dx * step, dz * step, out GlideBlock block);
            if (block == GlideBlock.None || _unrestricted())
            {
                Position = traced;
                return new GlideOutcome(Moved(cur, traced), GlideBlock.None, default);
            }

            // The full step is refused: slide on whichever single axis still passes (held diagonal against
            // the frame edge or a fog border), trying the larger component first so the dominant intent wins.
            var refused = traced;
            bool xFirst = Math.Abs(dx) >= Math.Abs(dz);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool alongX = xFirst == (attempt == 0);
                float adx = alongX ? dx * step : 0f, adz = alongX ? 0f : dz * step;
                if (alongX ? dx == 0f : dz == 0f) continue;
                var slid = Trace(cur, adx, adz, out GlideBlock axisBlock);
                if (axisBlock != GlideBlock.None || !Moved(cur, slid)) continue;
                Position = slid;
                return new GlideOutcome(true, GlideBlock.None, default);
            }

            return new GlideOutcome(false, block, refused);
        }

        // One candidate step: navmesh-trace it, then classify the landing against the senses' bounds. The
        // navmesh clamp runs first so a wall stop (in view, revealed) stays a silent non-block as before.
        // A fogged landing is refused only past the fringe around the entry point.
        private Vector3 Trace(Vector3 from, float dx, float dz, out GlideBlock block)
        {
            var intended = new Vector3(from.X + dx, from.Y, from.Z + dz);
            var traced = _env.TraceMove(from, intended);
            if (!_env.InView(traced)) block = GlideBlock.ViewEdge;
            else if (_env.IsFogged(traced) && PlanarDistance(_fogEntry, traced) > FogFringe)
                block = GlideBlock.Fog;
            else block = GlideBlock.None;
            return traced;
        }

        private static bool Moved(Vector3 a, Vector3 b) => Vector3.DistanceSquared(a, b) > 1e-8f;

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
