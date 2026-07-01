using System;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.World.Overlays;
using DiscoAccess.Core.World.Overlays.Systems;
using Sunshine.Views;
using UnityEngine;
using PlayMode = DiscoAccess.Core.World.Overlays.PlayMode; // disambiguate from UnityEngine.PlayMode
using Snv = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// Owns the one world overlay and the world keyboard while the player is in the isometric scene, the
    /// world-layer counterpart to <see cref="Nav.ScreenManager"/> for menus. Being in the free-roam world IS
    /// owning the keyboard: whenever the view reads CLEAR with the player in control and no menu screen
    /// taking it, this takes the same one lever the menu navigator uses (mutes <c>InControl</c> wholesale)
    /// and re-provides the world keys below it, restoring the lever on leaving. It engages the overlay on
    /// entering the world and disengages on leaving (so audio systems build/release their voices), glides the
    /// cursor from the held movement vector, and runs the <see cref="WalkInteract"/> verb.
    ///
    /// The <c>Dev*</c> hooks remain only as dev-server introspection; the live keys are wired through the
    /// module's input registry (the registration and the held-glide read live in <see cref="UiModule"/>,
    /// which owns the one <c>InputManager</c>).
    /// </summary>
    public sealed class WorldReader : IDisposable
    {
        /// <summary>The cursor glide rate, metres per second.</summary>
        private const float GlideSpeed = 4f;
        // How near the cursor must be to an actionable thing's body for Enter to target it rather than walk
        // to bare ground. Tied to the cursor's name-on-stop reach so the two can never drift apart: everything
        // Enter can snap to was named when the glide stopped, never a surprise interaction on unannounced
        // ground. The reach is generous for the same reason - the freeform cursor is navmesh-clamped and a
        // body can sit off the mesh (a door in a wall, a prop on a ledge), so the cursor only gets near it.
        private const float SnapRadius = ObjectCueSystem.ReachRadius;

        /// <summary>The live reader, for dev-server introspection/driving.</summary>
        public static WorldReader Active;

        private readonly IModHost _host;
        private readonly IAudioEngine _audio;
        private readonly Overlay _overlay;
        private readonly ObjectCueSystem _objects;
        private readonly SpatialSystem _spatial;
        private readonly WallToneSystem _wallTones;
        private readonly WorldModel _model = new WorldModel();
        private readonly WalkInteract _walk;
        private bool _engaged;
        private bool _ownsKeyboard;
        private bool _wasOwning;
        private bool _wasGliding;
        private bool _inWorld; // the frame's view read, resolved in ResolveOwnership and reused in Tick
        private bool _viewReadyOnce;
        private bool _warnedViewThrow;
        private IWallTones _devTones;

        public WorldReader(IModHost host)
        {
            _host = host;
            _audio = host.Audio;
            var env = new WorldEnvironment();
            _overlay = new Overlay(env, host.Speech);
            // The cursor's object sense: the enter/exit blips while gliding and the name of the thing under
            // the cursor on stop. Registered before the spatial system so its name leads the joined readout
            // ("crate; northeast, 2 meters"). Reads the same live registry the sonar and scanner will.
            _objects = new ObjectCueSystem(_model, _audio);
            _objects.BindMode(() => PlayMode.Continuous);
            _overlay.With(_objects);
            _spatial = new SpatialSystem();
            // Until the settings menu wires the world systems, the cursor readout is simply on.
            _spatial.BindMode(() => PlayMode.Continuous);
            _overlay.With(_spatial);
            // Wall tones: continuous when the player chose it, else only while the cursor is gliding (and the
            // brief linger after). The same env backs the cursor clamp and the wall-distance cast.
            _wallTones = new WallToneSystem(env, _audio);
            _wallTones.BindMode(() => host.Settings.WallTonesContinuous.Value ? PlayMode.Continuous : PlayMode.WhenMoving);
            _wallTones.BindVolume(() => host.Settings.WallToneVolume.Fraction);
            _overlay.With(_wallTones);
            _walk = new WalkInteract(host);
            Active = this;
        }

        /// <summary>Whether the world owns the keyboard this frame (the input layer gates the World category
        /// on it). Set by <see cref="ResolveOwnership"/> before input is polled.</summary>
        public bool OwnsKeyboard => _ownsKeyboard;

        /// <summary>Resolve keyboard ownership for this frame and take/restore the InControl lever. Call
        /// before polling input, after the screen manager has resolved its own ownership: a menu screen, the
        /// mod menu, or a popup is authoritative, so the world yields to it (<paramref name="screensOwn"/>),
        /// and otherwise owns the keyboard while in free-roam with control.</summary>
        public void ResolveOwnership(bool screensOwn)
        {
            // Read the view once here and reuse it in Tick this frame (the value is frame-stable, and the
            // bridge read enters a try/catch on the per-frame pump). ResolveOwnership always runs before Tick.
            _inWorld = InWorld();
            bool own = !screensOwn && _inWorld && _overlay.HasControl;

            // A committed walk that outlives our control (a script grabbed the character, the area unloaded)
            // is abandoned silently - the player did not ask to stop, so no spoken cancel.
            if (!own && _wasOwning) _walk.Abandon();

            // Mute InControl wholesale and re-provide our keys below it: targeted mutes do not take, so the
            // model is all-or-nothing (same lever the menu navigator uses). Reasserted each owning frame (the
            // game re-enables InControl on focus/device changes); handed back exactly once when we stop, but
            // never while a menu owns it, so we don't fight the lever the screen manager just took.
            if (own) InControl.InputManager.Enabled = false;
            else if (_wasOwning && !screensOwn) InControl.InputManager.Enabled = true;

            _wasOwning = own;
            _ownsKeyboard = own;
        }

        /// <summary>Engage/disengage on world entry/exit, refresh the registry, and - while we own the
        /// keyboard - glide the cursor by the held vector (<paramref name="dirX"/> east, <paramref name="dirZ"/>
        /// north) and advance the interact verb. Call after input is polled.</summary>
        public void Tick(float dirX, float dirZ)
        {
            bool inWorld = _inWorld; // resolved this frame by ResolveOwnership, which always runs first
            if (inWorld && !_engaged) { _overlay.OnEnter(); _engaged = true; }
            else if (!inWorld && _engaged) { _overlay.OnExit(); _engaged = false; _walk.Abandon(); }
            if (!inWorld) { _wasGliding = false; return; }

            float dt = Time.unscaledDeltaTime;
            _model.Tick(dt); // the sonar/scanner data layer, kept current whether or not we drive
            // The audio systems mute when we aren't the live keyboard owner (a conversation, a cutscene, or a
            // menu floating over the in-world view); they keep their voices and resume on return.
            _overlay.InputActive = _ownsKeyboard;

            if (!_ownsKeyboard)
            {
                // In the world but not driving (a conversation or cutscene, or a menu over the world). Keep the
                // systems and motion tracking current without moving the cursor; the audio mutes via InputActive.
                _overlay.Tick(dt, 0f, 0f, 0f);
                _wasGliding = false;
                return;
            }

            _overlay.Tick(dt, dirX, dirZ, GlideSpeed);
            // Read the cursor's new spot when a glide stroke ends (keys released) - the natural "where am I
            // now" - rather than every frame, which would be a wall of speech.
            bool gliding = dirX != 0f || dirZ != 0f;
            if (_wasGliding && !gliding) _overlay.AnnounceCurrent();
            _wasGliding = gliding;

            _walk.Tick();
        }

        /// <summary>Snap the cursor back onto the character and read the new spot (the recenter key).</summary>
        public void Recenter() => _overlay.Recenter();

        /// <summary>Cancel a committed walk and stop the character (the Stop key).</summary>
        public void Cancel() => _walk.Cancel();

        /// <summary>Walk-then-interact at the cursor: interact with the nearest actionable thing under the
        /// cursor, or walk to the bare ground there when nothing is close (the Enter verb).</summary>
        public void Interact()
        {
            if (!_engaged) return;
            Snv cursor = _overlay.Cursor.Position;
            Snv player = _overlay.Cursor.PlayerPosition;
            EntityProxy target = NearestActionableTo(cursor);
            // One reachability-oracle call, on the single chosen target. Reachable: walk to its stand-point
            // and interact. Unreachable: the cursor sits on walkable ground regardless, so walk there (getting
            // closer can make the thing reachable for a follow-up Enter) and name it so the player learns it
            // is there, rather than leaving a dead zone where Enter does nothing. No target near the cursor:
            // a plain walk to the spot.
            if (target != null && target.IsActionable(player))
                _walk.BeginInteract(target, player);
            else if (target != null)
                _walk.BeginWalk(cursor, Strings.WorldUnreachable(target.Name));
            else
                _walk.BeginWalk(cursor, Strings.WorldWalking);
        }

        // The nearest actionable entity whose body sits within the snap radius of the cursor, or null for a
        // bare-ground cursor. Selection measures to the body (cheap) and filters on the IsAccessible gate;
        // the reachability oracle and stand-point are only consulted for the one chosen target (in
        // WalkInteract), never the whole set. Orbs are skipped - orb interaction is deferred (camera-follow).
        private EntityProxy NearestActionableTo(Snv cursor)
        {
            EntityProxy best = null;
            float bestDist = SnapRadius;
            foreach (var item in _model.Items)
            {
                if (!item.IsAccessible || !(item is EntityProxy ep)) continue;
                float d = Snv.Distance(item.Position, cursor);
                if (d <= bestDist) { bestDist = d; best = ep; }
            }
            return best;
        }

        // The plain in-game world is the CLEAR view. Confirmed live: during free-roam ViewsPagesBridge.Current
        // reads CLEAR steadily, and DevScan sees the full entity set; a menu, dialogue, or cutscene is its own
        // ViewType. (The LOBBY value ScreenAdapter maps to the world-screen NAME is a different page state,
        // not the free-roam view - do not switch this gate to LOBBY: it reads false while actually in-world.)
        // HasControl gates the finer cutscene/dialogue case on top. The bridge throws during early boot (no
        // view system yet) - expected and frequent - so that is swallowed; any other throw is logged once so a
        // real failure (a post-update proxy change) surfaces without spamming the per-frame pump.
        private bool InWorld()
        {
            try
            {
                ViewType view = ViewsPagesBridge.Current;
                _viewReadyOnce = true;
                return view == ViewType.CLEAR;
            }
            catch (Exception e)
            {
                // Boot transients (before the view system ever comes up) are expected and silent; a throw
                // after it has worked once is a real regression, logged a single time.
                if (_viewReadyOnce && !_warnedViewThrow)
                {
                    _warnedViewThrow = true;
                    _host.LogWarning("WorldReader: view read failed; world layer idle until it recovers: " + e.Message);
                }
                return false;
            }
        }

        public void Dispose()
        {
            if (_engaged) { _overlay.OnExit(); _engaged = false; }
            _devTones?.Dispose();
            _devTones = null;
            if (Active == this) Active = null;
        }

        // ---- dev hooks (drive/inspect the cursor over the dev /eval server until live keys land) ----

        public string DevState()
            => $"player={_overlay.Cursor.PlayerPosition}, cursor={_overlay.Cursor.Position}, inWorld={InWorld()}";

        public string DevView()
        {
            try { return "view=" + ViewsPagesBridge.Current; }
            catch (Exception e) { return "view threw: " + e.GetType().Name + " " + e.Message; }
        }

        public void DevAnnounce() => _overlay.AnnounceCurrent();

        /// <summary>Glide the cursor one ~quarter-second step in (dx east, dz north) at 4 m/s, then read it.</summary>
        public void DevGlide(float dx, float dz)
        {
            _overlay.Tick(0.25f, dx, dz, 4f);
            _overlay.AnnounceCurrent();
        }

        public void DevRecenter() => _overlay.Recenter();

        // Audio-backbone validation: a panned one-shot, and the four wall-tone voices driven directly.
        public string DevAudioState() => "available=" + _audio.Available;
        public void DevBeep(float pan) => _audio.PlayOneShot(440f, 0.3f, 0.8f, pan);
        public void DevWall(float n, float s, float e, float w)
        {
            if (_devTones == null) _devTones = _audio.CreateWallTones();
            _devTones.Update(new[] { n, s, e, w });
        }
        public void DevWallStop() { _devTones?.Dispose(); _devTones = null; }

        // World-model validation: total items, per-category counts, and how many pass the IsAccessible gate
        // (the doc's ~400 entities collapsing to ~90 actionable things).
        public string DevScan()
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            int total = 0, accessible = 0, accessibleByCat;
            var accCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var it in _model.Items)
            {
                total++;
                counts.TryGetValue(it.Category, out int c); counts[it.Category] = c + 1;
                if (it.IsAccessible)
                {
                    accessible++;
                    accCounts.TryGetValue(it.Category, out accessibleByCat); accCounts[it.Category] = accessibleByCat + 1;
                }
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("total=").Append(total).Append(" accessible=").Append(accessible).Append('\n');
            foreach (var cat in DiscoAccess.Core.World.WorldTaxonomy.All)
            {
                counts.TryGetValue(cat, out int c);
                accCounts.TryGetValue(cat, out int a);
                sb.Append("  ").Append(cat).Append(": ").Append(c).Append(" (").Append(a).Append(" accessible)\n");
            }
            return sb.ToString();
        }
    }
}
