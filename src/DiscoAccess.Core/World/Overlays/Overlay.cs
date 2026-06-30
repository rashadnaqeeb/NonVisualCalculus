using System;
using System.Collections.Generic;
using DiscoAccess.Core.Speech;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// The one sensory layer over the scene: a <see cref="Cursor"/> plus a set of <see cref="OverlaySystem"/>s
    /// (one per type). It owns no behavior beyond moving the cursor, fanning lifecycle/tick out to its
    /// systems, and running the announce pipeline. There is no overlay-cycling here (unlike the WOTR
    /// reference): Disco has a single implicit overlay whose systems are toggled from the mod settings menu,
    /// so the framework is just this container, owned and gated by the Module's world reader.
    /// </summary>
    public sealed class Overlay
    {
        private readonly List<OverlaySystem> _systems = new List<OverlaySystem>(); // ordered (readout order)
        private readonly Dictionary<Type, OverlaySystem> _byType = new Dictionary<Type, OverlaySystem>();
        private readonly IWorldEnvironment _env;
        private readonly SpeechPipeline _speech;
        private readonly MotionTracker _motion = new MotionTracker();

        public Cursor Cursor { get; }

        /// <summary>Whether the cursor moved recently — drives the systems' <see cref="PlayMode.WhenMoving"/>
        /// gate.</summary>
        public bool CursorMovingRecently => _motion.MovingRecently;

        /// <summary>Whether the player controls the character right now (cursor can move, scripted scene is
        /// not playing) — systems read it to decide what to suppress.</summary>
        public bool HasControl => _env.HasControl;

        public Overlay(IWorldEnvironment env, SpeechPipeline speech)
        {
            _env = env;
            _speech = speech;
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

        /// <summary>The system with the given settings key (e.g. "sonar"), or null.</summary>
        public OverlaySystem? GetSystem(string key)
        {
            foreach (var s in _systems) if (s.Key == key) return s;
            return null;
        }

        public void OnEnter() { foreach (var s in _systems) s.OnEnter(this); }

        public void OnExit()
        {
            foreach (var s in _systems) s.OnExit(this);
            _motion.Reset();
        }

        /// <summary>One frame: glide the cursor by the held direction (only while in control, so it can't
        /// drift in a cutscene), refresh the moving signal from the fresh position, then tick every system
        /// so they read the up-to-date cursor. <paramref name="dirX"/>/<paramref name="dirZ"/> are the held
        /// movement vector (east/north positive); <paramref name="speed"/> is metres/second.</summary>
        public void Tick(float dt, float dirX, float dirZ, float speed)
        {
            bool intent = dirX != 0f || dirZ != 0f;
            if (_env.HasControl) Cursor.Glide(dirX, dirZ, dt, speed);
            _motion.Update(Cursor.Position, dt, intent && _env.HasControl);
            for (int i = 0; i < _systems.Count; i++) _systems[i].Tick(dt, this);
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
            if (parts.Count > 0) _speech.Speak(string.Join("; ", parts), interrupt: true);
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
