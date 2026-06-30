using System.Numerics;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// Keeps the game camera focused near the exploration cursor. Orbs are camera-streamed: the game culls
    /// them against the camera frustum, so an orb only wakes (gains its text and type) and renders once the
    /// camera is on it. Following the cursor is therefore what reveals the orbs around wherever the player is
    /// looking, and what lets an orb the cursor sits on become interactable (its clickable activates only when
    /// the orb is rendered). The game has no automatic character-follow in free roam, so nothing fights this.
    ///
    /// It re-focuses through the <see cref="IWorldEnvironment"/> seam whenever the cursor drifts past a step
    /// (and once on becoming active), not every frame, since each camera move recomputes the frustum and
    /// churns the orb streamer. It releases when inactive (a conversation, a cutscene, or a menu over the
    /// world), leaving the game's own camera - dialogue framing, scripted shots - unopposed, and re-focuses
    /// fresh on the next activation.
    /// </summary>
    public sealed class CameraFollow
    {
        // Re-focus only after the cursor has drifted this far from the last focus point. The orb sense radius
        // is far wider, so streaming stays seamless while we drive roughly one camera move per couple of metres
        // rather than one per frame.
        private const float FocusStepMetres = 2f;

        private readonly IWorldEnvironment _env;
        private Vector3 _lastFocus;
        private bool _following;

        public CameraFollow(IWorldEnvironment env)
        {
            _env = env;
        }

        /// <summary>One frame. When <paramref name="active"/>, keep the camera on <paramref name="target"/>,
        /// re-focusing when it has drifted past the step (and once on becoming active). When inactive, release
        /// the camera back to the game and re-focus fresh on the next activation.</summary>
        public void Tick(Vector3 target, bool active)
        {
            if (!active)
            {
                Release();
                return;
            }
            if (_following && Vector3.Distance(_lastFocus, target) < FocusStepMetres) return;
            _env.FocusCamera(target);
            _lastFocus = target;
            _following = true;
        }

        /// <summary>Hand the camera back to the game if we were following (control lost, a menu over the world,
        /// or the world left, including module teardown via the overlay's exit). Idempotent.</summary>
        public void Release()
        {
            if (!_following) return;
            _env.ReleaseCamera();
            _following = false;
        }
    }
}
