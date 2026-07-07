using System;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.World;
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

        /// <summary>How far the character may move in one step and still count as walking. A single-step jump
        /// past this is a reposition (a door, quicktravel, a loaded save), not a stride: at any walk/run speed
        /// the character covers a fraction of a metre per frame, while a teleport is metres to hundreds.</summary>
        private const float RepositionJump = 3f;

        /// <summary>The live reader, for dev-server introspection/driving.</summary>
        public static WorldReader Active;

        private readonly IModHost _host;
        private readonly IAudioEngine _audio;
        private readonly SpatialSources _sources;
        private readonly WorldEnvironment _env;
        private readonly Overlay _overlay;
        private readonly ObjectCueSystem _objects;
        private readonly SpatialSystem _spatial;
        private readonly WallToneSystem _wallTones;
        private readonly SonarSystem _sonar;
        private readonly WorldModel _model;
        private readonly WalkInteract _walk;
        private readonly Scanner _scanner;
        private bool _engaged;
        private Snv _lastPlayer; // character position last in-world frame, to catch a reposition (load/teleport)
        private string _lastScene; // scene last in-world frame - a change is a reposition regardless of distance
        private bool _hasLastPlayer;
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
            // The tracked positional one-shots (the blips and pings, the sonar when it lands): fired here,
            // re-placed each frame in Tick so they follow a moving listener.
            _sources = new SpatialSources(_audio, host.LogWarning);
            _env = new WorldEnvironment();
            // The data layer every world sense reads. The logger is threaded to each entity proxy so a
            // silent reachability over-reject (an accessible interactable dropped by the standing-ground
            // geometry) can surface itself to the log at the point it happens - see EntityProxy.WarnIfNearMiss.
            _model = new WorldModel(host.LogWarning);
            _overlay = new Overlay(_env, host.Speech, _sources);
            // The cursor's object sense: the enter/exit blips while gliding and the name of the thing under
            // the cursor on stop. Registered before the spatial system so its name leads the joined readout
            // ("crate; northeast, 2 meters"). Reads the same live registry the sonar and scanner will.
            _objects = new ObjectCueSystem(_model, _sources);
            _objects.BindMode(() => PlayMode.Continuous);
            _overlay.With(_objects);
            _spatial = new SpatialSystem();
            // Until the settings menu wires the world systems, the cursor readout is simply on.
            _spatial.BindMode(() => PlayMode.Continuous);
            _overlay.With(_spatial);
            // Wall tones: continuous when the player chose it, else only while the cursor is gliding (and the
            // brief linger after). The same env backs the cursor clamp and the wall-distance cast.
            _wallTones = new WallToneSystem(_env, _audio);
            _wallTones.BindMode(() => host.Settings.WallTonesContinuous.Value ? PlayMode.Continuous : PlayMode.WhenMoving);
            _wallTones.BindVolume(() => host.Settings.WallToneVolume.Fraction);
            _overlay.With(_wallTones);
            // The sonar sweep: pings the scanner's offered set around the cursor, one thing at a time,
            // gated by the per-category toggles. Continuous when the player chose it, else only while the
            // cursor is gliding - the wall-tone rule.
            _sonar = new SonarSystem(_model, _env, _sources, host.LogWarning);
            _sonar.BindMode(() => host.Settings.SonarContinuous.Value ? PlayMode.Continuous : PlayMode.WhenMoving);
            _sonar.BindCategories(host.Settings.SonarCategoryEnabled);
            _sonar.BindRest(() => host.Settings.SonarRest.Value / 1000f);
            _sonar.BindVolume(() => host.Settings.SonarVolume.Fraction);
            _overlay.With(_sonar);
            _walk = new WalkInteract(host);
            // The review cursor: browses the same live registry the cursor senses, scoped by the same env
            // (in-frame, unfogged), anchored to the PLAYER - membership, sort, and spoken distances all
            // measure from where the character stands, since the walk a scanned thing supports starts there.
            // Its selection is a second point of attention: landing announces without moving the cursor or
            // the character, and I acts on the selection through the same walk-then-interact verb as Enter.
            _scanner = new Scanner(_model, _env, () => _env.PlayerPosition, host.Speech, _sources);
            _scanner.BindVolume(() => host.Settings.SonarVolume.Fraction);
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
            // Re-place the live one-shots first, every frame regardless of world/ownership state: a voice
            // fired a moment ago keeps tracking (and draining) through a conversation or an area exit.
            _sources.Tick();

            bool inWorld = _inWorld; // resolved this frame by ResolveOwnership, which always runs first
            if (inWorld && !_engaged) { _overlay.OnEnter(); _engaged = true; }
            else if (!inWorld && _engaged) { _overlay.OnExit(); _engaged = false; _walk.Abandon(); _scanner.Reset(); _sources.Clear(); }
            if (!inWorld) { _wasGliding = false; return; }

            // A save load, door transition, or fast travel repositions the character out from under the
            // cursor, which keeps its old spot and is left stranded far from the player. Watch the
            // character's own position between consecutive IN-WORLD frames: the baseline survives the
            // non-CLEAR views a reposition typically happens behind (a loading fade, the journal's travel
            // map, the pause overlay of a save load), so the first frame back in the world sees the jump. A
            // one-step jump past a walk stride is a load or teleport - a cutscene can walk the character,
            // but never metres in one frame - and a scene change is a reposition outright (its coordinates
            // are a different map's). Unpin the cursor back onto the character.
            Snv player = _overlay.Cursor.PlayerPosition;
            string scene = SceneName();
            // The jump also invalidates any still-playing cue tails (their listener just moved across the
            // map, or reads as the origin while the character is despawned mid-load) - stop tracking them.
            if (_hasLastPlayer && (scene != _lastScene || Snv.Distance(player, _lastPlayer) > RepositionJump))
            {
                _overlay.Cursor.Reset();
                _sources.Clear();
                // The map changed under the player (a door, stairs, quicktravel): speak the new location,
                // the same words the read-location key gives, so arrival orients without a keypress.
                // Queued - it follows whatever the transition itself is saying.
                if (scene != _lastScene) SpeakLocation(interrupt: false);
            }
            _lastPlayer = player;
            _lastScene = scene;
            _hasLastPlayer = true;

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

            // Hold the zoom at the area's maximum while we drive, so the cursor's roam window (the visible
            // frame) stays as wide and as consistent as the game allows. Only while owning, so dialogue and
            // cutscene zoom sequences are never fought.
            _env.PinZoom();

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

        /// <summary>Read the current location (the Read-location key). An explicit request, so it
        /// interrupts; the same words also speak automatically on a map change (see Tick).</summary>
        public void ReadLocation() => SpeakLocation(interrupt: true);

        // Speak the current location: the map's spoken name, the game's own localized area name with
        // hyphens read as spaces ("Whirling in Rags"), plus the floor word for a numbered interior level
        // so stacked scenes are distinguishable.
        private void SpeakLocation(bool interrupt)
        {
            string scene = SceneName();
            string localized = GameLocalization.Translate("Area Names/" + scene);
            string map = (string.IsNullOrEmpty(localized) ? scene : localized).Replace('-', ' ');
            _host.Speech.Speak(Strings.WorldLocation(map, EntityNaming.LevelLabel(scene)), interrupt: interrupt);
        }

        private static string SceneName() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        /// <summary>Cancel a committed walk and stop the character (the Stop key).</summary>
        public void Cancel() => _walk.Cancel();

        /// <summary>Walk-then-interact at the cursor: interact with the nearest actionable thing under the
        /// cursor, or walk to the bare ground there when nothing is close (the Enter verb).</summary>
        public void Interact()
        {
            if (!_engaged) return;
            Snv cursor = _overlay.Cursor.Position;
            Snv player = _overlay.Cursor.PlayerPosition;
            // The one thing under the cursor - the exact selection the cursor blip and spoken name use, so
            // Enter acts on precisely what was announced, never a different thing. Under() returns accessible
            // interactables and orbs, every one of which is an IWalkTarget, so this always takes when non-null.
            IWalkTarget target = _objects.Under(cursor, player) as IWalkTarget;
            // A thing under the cursor: walk to it and interact. Reachability is not pre-judged here - an
            // entity's Interact is the game's own click, which prices the approach itself and refuses with a
            // spoken can't-reach; an orb goes through the verb's watched walk, which reports its own outcome.
            // No target: a plain walk.
            if (target != null)
                _walk.BeginInteract(target, player);
            else
                _walk.BeginWalk(cursor, Strings.WorldMoving);
        }

        /// <summary>Walk to the cursor's spot without interacting, whatever stands there (the Walk verb):
        /// the same bare-ground move Enter makes on empty ground, for moving next to a thing - or through
        /// its neighbourhood - without triggering it.</summary>
        public void Walk()
        {
            if (!_engaged) return;
            _walk.BeginWalk(_overlay.Cursor.Position, Strings.WorldMoving);
        }

        // ---- the bookmarks menu's world reads and its walk (see Nav.BookmarksScreen) ----

        /// <summary>Whether a playable game is loaded - the bookmarks menu's gate. A player character
        /// alone does not prove it: the title screen keeps one parked at the origin of its "Lobby"
        /// shell scene (verified live), where a bookmark would be meaningless.</summary>
        public bool GameLoaded => _env.HasPlayer && SceneName() != LobbyScene;

        // The menu/credits shell scene's internal id (also hosts the endgame newspaper).
        private const string LobbyScene = "Lobby";

        /// <summary>The character's position in the mod's world frame, for capturing a bookmark.</summary>
        public Snv PlayerPosition => _env.PlayerPosition;

        /// <summary>The current scene id, the per-map key bookmark lists are scoped by.</summary>
        public string CurrentScene => SceneName();

        /// <summary>Whether a complete navmesh path connects the character to a stored point, for a
        /// bookmark row's spoken reachability.</summary>
        public bool CanReach(Snv point) => _env.PathComplete(_env.PlayerPosition, point);

        /// <summary>Walk to a stored point (a bookmark): the same bare-ground move as the Walk verb.
        /// Outside the free-roam world (the bookmarks menu also opens over a game menu or a
        /// conversation) the character cannot be driven, spoken as can't-reach.</summary>
        public void WalkTo(Snv point)
        {
            if (!_engaged)
            {
                _host.Speech.Speak(Strings.WorldUnreachable(null), interrupt: true);
                return;
            }
            _walk.BeginWalk(point, Strings.WorldMoving);
        }

        // ---- the scanner (review cursor) verbs, fired by the world keys ----

        /// <summary>Cycle the scanner selection through the current browse category (PageDown / PageUp).</summary>
        public void ScanNext() { if (_engaged) _scanner.StepItem(1); }
        public void ScanPrev() { if (_engaged) _scanner.StepItem(-1); }

        /// <summary>Step the scanner's browse category (Ctrl+PageDown / Ctrl+PageUp).</summary>
        public void ScanNextCategory() { if (_engaged) _scanner.StepCategory(1); }
        public void ScanPrevCategory() { if (_engaged) _scanner.StepCategory(-1); }

        /// <summary>Cycle a quick-nav group (comma people and interactables, period items, slash exits;
        /// Shift reverses), independent of the browse category.</summary>
        public void ScanPeople(int dir) { if (_engaged) _scanner.StepGroup(dir, ScanGroup.People); }
        public void ScanItems(int dir) { if (_engaged) _scanner.StepGroup(dir, ScanGroup.Items); }
        public void ScanExits(int dir) { if (_engaged) _scanner.StepGroup(dir, ScanGroup.Exits); }

        /// <summary>Walk to the scanned thing and interact (I) - the review counterpart of Enter, through the
        /// same walk-then-interact verb, so reachability is attempted and reported, never pre-judged.</summary>
        public void ScanInteract()
        {
            if (!_engaged) return;
            IWalkTarget target = _scanner.Selected as IWalkTarget;
            if (target == null) { _host.Speech.Speak(Strings.WorldScanNothing, interrupt: true); return; }
            _walk.BeginInteract(target, _overlay.Cursor.PlayerPosition);
        }

        /// <summary>Move the cursor to the scanned thing (J): onto its interaction point - the walkable
        /// stand-spot the game's click would walk the player to, the same point the scanner's readout
        /// measures - never the thing's own body, which can sit off-mesh (a wall fixture, a boat on
        /// water) and would strand the navmesh-clamped cursor. Reads the new spot the way a glide-stop
        /// or recenter does.</summary>
        public void ScanCursor()
        {
            if (!_engaged) return;
            IWorldItem target = _scanner.Selected;
            if (target == null) { _host.Speech.Speak(Strings.WorldScanNothing, interrupt: true); return; }
            _overlay.Cursor.Position = target.InteractionPoint(_overlay.Cursor.PlayerPosition);
            _overlay.AnnounceCurrent();
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

        /// <summary>Live sonar sweep state (mode, snapshot size, next index, timer).</summary>
        public string DevSonar() => _sonar.DevState();

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

        // Reachability audit: the things a sighted player could see and click that the scanner nonetheless
        // drops. An item that clears accessibility, fog, and the camera frame but fails ScanScope.Offered was
        // dropped by the reachability gate alone (the same predicate the scanner offers by, so this is exactly
        // what that gate hides). Each such row is cross-checked against the game's own click oracle
        // (EntityProxy.ClickOracleWouldAct): a SUSPECT row is one the game itself would walk over and act on,
        // so the mod is hiding something clickable - the class the Whirling dress shirt fell into. The oracle
        // over-accepts by design (its stand-point radius can grab an overhead or adjacent-level floor), so a
        // few suspects each visit are correct drops (the same structural cases); a genuinely new over-reject
        // shows up as a new suspect. Anchored to the player, like the scanner. Run after walking into an area
        // to sweep what that scene hides. On-demand only (heavy: it prices a click per hidden item).
        public string DevReach()
        {
            Snv from = _env.PlayerPosition;
            var sb = new System.Text.StringBuilder();
            int hidden = 0, suspects = 0;
            foreach (var it in _model.Items)
            {
                if (!it.IsAccessible || !it.IsVisible) continue;
                if (!_env.InView(it.Bounds.NearestPoint(from))) continue;
                if (ScanScope.Offered(it, from, _env)) continue;
                hidden++;
                bool oracle = it is EntityProxy ep && ep.ClickOracleWouldAct();
                if (oracle) suspects++;
                sb.Append(oracle ? "  SUSPECT  " : "  ok       ")
                  .Append(it.Category).Append("  '").Append(it.Name).Append("'  ")
                  .Append((it.Position - from).Length().ToString("F1")).Append(" m")
                  .Append(oracle ? "  (game would click it)" : "").Append('\n');
            }
            return $"in-frame, hidden by reachability: {hidden}; oracle-clickable suspects: {suspects}\n" + sb;
        }
    }
}
