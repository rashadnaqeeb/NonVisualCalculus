using System;
using System.Collections.Generic;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// The one sensory layer over the scene: a <see cref="Cursor"/> plus a set of <see cref="OverlaySystem"/>s
    /// (one per type). It owns no behavior beyond moving the cursor, fanning lifecycle/tick out to its
    /// systems, keeping the cursor inside the visible frame, and running the announce pipeline. The game's
    /// camera stays slaved to the character (the mod never drives it), so the frame is a stable window
    /// around the player's body and the cursor's whole world is rendered, revealed, and actable. There is
    /// no overlay-cycling here (unlike the WOTR reference): Disco has a single implicit overlay whose
    /// systems are toggled from the mod settings menu, so the framework is just this container, owned and
    /// gated by the Module's world reader.
    /// </summary>
    public sealed class Overlay
    {
        // The impassable bump's spatialization: panned toward the refused point from the cursor, at the
        // cursor-cue level. The distance is under a step, so the gain is flat - no falloff. The volumes
        // are public so the learn-sounds menu previews each cue at exactly its in-world level.
        public const float ImpassableVolume = 0.8f;
        private const float ImpassablePanWidth = 3f;

        // The unrestricted cursor's fog enter/exit cues: at the cursor itself, so centred and flat.
        public const float FogCueVolume = 0.8f;

        private readonly List<OverlaySystem> _systems = new List<OverlaySystem>(); // ordered (readout order)
        private readonly Dictionary<Type, OverlaySystem> _byType = new Dictionary<Type, OverlaySystem>();
        private readonly IWorldEnvironment _env;
        private readonly SpeechPipeline _speech;
        private readonly SpatialSources _cues;
        private readonly MotionTracker _motion = new MotionTracker();
        private bool _wasBlocked; // last stroke frame ended pinned - the bump fires on the transition
        private bool _wasOutside; // unrestricted cursor was beyond the senses - the fog cues fire on the transitions

        public Cursor Cursor { get; }

        /// <summary>Whether the player held the cursor movement keys recently — drives the systems'
        /// <see cref="PlayMode.WhenMoving"/> gate. Deliberate gliding only: a recenter, a reposition
        /// reset, or the frame-drag moving the cursor does not count.</summary>
        public bool CursorMovingRecently => _motion.MovingRecently;

        /// <summary>Whether the player controls the character right now (cursor can move, scripted scene is
        /// not playing) — systems read it to decide what to suppress.</summary>
        public bool HasControl => _env.HasControl;

        /// <summary>Whether the world is the live keyboard owner this frame, set by the world reader. False
        /// when a menu or popup floats over the world (the game view still reads in-world, but the overlay is
        /// no longer being driven), so audio systems mute rather than droning under the menu. Defaults true so
        /// a directly-driven overlay (tests, dev hooks) plays.</summary>
        public bool InputActive { get; set; } = true;

        public Overlay(IWorldEnvironment env, SpeechPipeline speech, SpatialSources cues)
        {
            _env = env;
            _speech = speech;
            _cues = cues;
            Cursor = new Cursor(env);
        }

        /// <summary>Add a system (one per concrete type; a duplicate replaces the prior instance).</summary>
        public Overlay With(OverlaySystem system)
        {
            if (system == null) return this;
            var t = system.GetType();
            if (_byType.TryGetValue(t, out var existing)) _systems.Remove(existing);
            _byType[t] = system;
            _systems.Add(system);
            return this;
        }

        /// <summary>The single system of type T, or null. Deterministic by one-per-type.</summary>
        public T? Get<T>() where T : OverlaySystem
            => _byType.TryGetValue(typeof(T), out var s) ? (T)s : null;

        public void OnEnter() { foreach (var s in _systems) s.OnEnter(this); }

        public void OnExit()
        {
            foreach (var s in _systems) s.OnExit(this);
            _motion.Reset();
            _wasBlocked = false;
            _wasOutside = false;
            // Leaving the world drops the remembered spot: when the view reopens (a conversation ends, a
            // menu closes), the cursor is back on the character, where the next action starts from.
            Cursor.Reset();
        }

        /// <summary>One frame: glide the cursor by the held direction (only while in control, so it can't
        /// drift in a cutscene), keep it inside the visible frame, refresh the moving signal from the held
        /// keys, then tick every system so they read the up-to-date cursor. A stroke pinned at the frame
        /// edge or against fogged ground bumps once on the transition (re-armed by releasing the keys), so
        /// holding into the boundary doesn't drone. <paramref name="dirX"/>/<paramref name="dirZ"/> are the
        /// held movement vector (east/north positive); <paramref name="speed"/> is metres/second.</summary>
        public void Tick(float dt, float dirX, float dirZ, float speed)
        {
            // The moving signal is the held keys, not the cursor's position: holding counts even when the
            // cursor can't advance (blocked against a wall) so the WhenMoving systems don't stutter, and a
            // keyless position change (recenter, reposition reset, frame-drag) never counts, so those
            // systems sound only for deliberate movement.
            bool intent = dirX != 0f || dirZ != 0f;
            bool driven = InputActive && _env.HasControl;
            GlideOutcome outcome = default;
            if (_env.HasControl) outcome = Cursor.Glide(dirX, dirZ, dt, speed);

            // The frame invariant: a walking character slides the window, so a pinned cursor rides its edge
            // rather than falling out of the senses. Silent - the glide-stop and recenter readouts answer
            // "where am I". Only while driven, so a dialogue or menu camera never drags the cursor around;
            // only while pinned, so an unpinned cursor (riding the player) is never re-pinned against a
            // camera that hasn't caught up to a just-repositioned character. An unrestricted cursor is
            // allowed out of the frame, so it is not dragged back either.
            if (driven && Cursor.IsPinned && !Cursor.Unrestricted && !_env.InView(Cursor.Position))
                Cursor.Position = _env.ClampToView(Cursor.Position);

            _motion.Update(dt, intent);
            for (int i = 0; i < _systems.Count; i++) _systems[i].Tick(dt, this);

            bool blocked = intent && outcome.Block != GlideBlock.None;
            if (driven && blocked && !_wasBlocked)
                _cues.Play(AudioCue.CursorImpassable,
                           () => Cursor.Position,
                           _ => outcome.BlockedToward,
                           _ => ImpassableVolume,
                           ImpassablePanWidth);
            _wasBlocked = blocked;

            // The unrestricted cursor's boundary sense: crossing out past the edge of the senses (off the
            // visible frame or onto fogged ground - exactly where a restricted glide refuses and bumps)
            // plays the fog-enter cue, and coming back in plays the exit cue. Pinned only, so a camera
            // mid-catch-up under an unpinned cursor never reads as a crossing; the transition is judged
            // only while driven, so a menu opening over the world neither cues nor swallows one.
            bool outside = Cursor.Unrestricted && Cursor.IsPinned
                && (!_env.InView(Cursor.Position) || _env.IsFogged(Cursor.Position));
            if (driven && outside != _wasOutside)
            {
                _cues.Play(outside ? AudioCue.CursorFogEnter : AudioCue.CursorFogExit,
                           () => Cursor.Position,
                           p => p,
                           _ => FogCueVolume,
                           ImpassablePanWidth);
                _wasOutside = outside;
            }
        }

        /// <summary>Gather every system's announcements for the request, keep those matching the requested
        /// context, and speak the composed line (interrupting, since this is a navigation readout).</summary>
        public void Announce(AnnouncementContext want)
        {
            var ctx = new OverlayContext(this, Cursor.Position, _env.PlayerPosition, want);
            var parts = new List<string>();
            foreach (var s in _systems)
                foreach (var a in s.Announce(ctx))
                    if (a != null && a.Context == want && !string.IsNullOrEmpty(a.Text)) parts.Add(a.Text);
            if (parts.Count > 0) _speech.Speak(Text.SpokenLine.Join("; ", parts), interrupt: true);
        }

        public void AnnounceCurrent() => Announce(AnnouncementContext.Point);

        /// <summary>Snap the cursor back onto the player and read the new spot.</summary>
        public void Recenter()
        {
            Cursor.Recenter();
            AnnounceCurrent();
        }
    }
}
