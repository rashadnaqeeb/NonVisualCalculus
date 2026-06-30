using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.World.Overlays;
using Xunit;

namespace DiscoAccess.Tests
{
    // Shares the static SpeechPipeline.Spoken tap with SpeechPipelineTests; one collection keeps the two
    // from running in parallel, so this class's Speak calls can't fire into that class's captured tap.
    [Collection("UsesSpeechPipeline")]
    public class OverlayFrameworkTests
    {
        // A scripted environment: fixed player position, controllable HasControl, and a TraceMove that
        // either passes the intended point through (open ground) or clamps to a wall.
        private sealed class FakeEnv : IWorldEnvironment
        {
            public Vector3 Player = new Vector3(0f, 0f, 0f);
            public bool Control = true;
            public Vector3? Wall; // when set, TraceMove returns this regardless of intent (blocked)
            public Vector3 PlayerPosition => Player;
            public bool HasControl => Control;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => Wall ?? intended;
        }

        private sealed class FakeBackend : ISpeechBackend
        {
            public readonly List<string> Spoken = new List<string>();
            public bool IsAvailable => true;
            public void Speak(string text, bool interrupt) => Spoken.Add(text);
            public void Stop() { }
        }

        // A system that yields a fixed line for the Point context, and records its play gate each tick.
        // Two distinct subclasses exist so an overlay can hold both (one system per concrete type).
        private class FakeSystem : OverlaySystem
        {
            private readonly string _line;
            public FakeSystem(string line) { _line = line; }
            public override string Name => "Fake";
            public override string Key => "fake";
            public bool LastShouldPlay { get; private set; }
            public override void Tick(float dt, Overlay overlay) => LastShouldPlay = ShouldPlay(overlay);
            public override IEnumerable<OverlayAnnouncement> Announce(OverlayContext ctx)
            {
                if (Enabled) yield return new OverlayAnnouncement(AnnouncementContext.Point, _line);
            }
        }

        private sealed class FakeSystemB : FakeSystem
        {
            public FakeSystemB(string line) : base(line) { }
            public override string Key => "fakeB";
        }

        // ---- cursor + motion ----

        [Fact]
        public void Cursor_DefaultsToPlayer_UntilMoved()
        {
            var env = new FakeEnv { Player = new Vector3(5f, 0f, 7f) };
            var cursor = new Cursor(env);
            Assert.Equal(new Vector3(5f, 0f, 7f), cursor.Position);
        }

        [Fact]
        public void Cursor_Glide_MovesAlongDirection_AtSpeed()
        {
            var env = new FakeEnv();
            var cursor = new Cursor(env);
            cursor.Glide(1f, 0f, dt: 1f, speed: 4f); // 4 m/s east for 1 s
            Assert.Equal(4f, cursor.Position.X, 3);
            Assert.Equal(0f, cursor.Position.Z, 3);
        }

        [Fact]
        public void Cursor_Glide_IsNavmeshClamped()
        {
            var env = new FakeEnv { Wall = new Vector3(1f, 0f, 0f) };
            var cursor = new Cursor(env);
            cursor.Glide(1f, 0f, dt: 1f, speed: 100f); // would shoot far east, but the wall clamps it
            Assert.Equal(new Vector3(1f, 0f, 0f), cursor.Position);
        }

        [Fact]
        public void MotionTracker_LingersThenClears()
        {
            var m = new MotionTracker();
            m.Update(new Vector3(0f, 0f, 0f), 0.1f);
            m.Update(new Vector3(1f, 0f, 0f), 0.1f); // moved
            Assert.True(m.MovingRecently);
            m.Update(new Vector3(1f, 0f, 0f), MotionTracker.LingerSec + 0.01f); // sat still past the linger
            Assert.False(m.MovingRecently);
        }

        // ---- play-mode gating ----

        [Fact]
        public void ShouldPlay_FollowsModeAndMotion()
        {
            var env = new FakeEnv();
            var overlay = new Overlay(env, new SpeechPipeline(new FakeBackend()));
            var sys = new FakeSystem("x");
            overlay.With(sys);

            var mode = PlayMode.Off;
            sys.BindMode(() => mode);

            // Off: never plays, even while moving.
            overlay.Tick(0.1f, 1f, 0f, speed: 4f);
            Assert.False(sys.LastShouldPlay);

            // Continuous: plays whether or not the cursor moved.
            mode = PlayMode.Continuous;
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.True(sys.LastShouldPlay);

            // WhenMoving: plays only while the cursor moved recently.
            mode = PlayMode.WhenMoving;
            overlay.Tick(0.1f, 1f, 0f, speed: 4f); // moving
            Assert.True(sys.LastShouldPlay);
            // Let the linger expire with no movement.
            overlay.Tick(MotionTracker.LingerSec + 0.1f, 0f, 0f, speed: 4f);
            Assert.False(sys.LastShouldPlay);
        }

        [Fact]
        public void ForceHeld_OverridesOffMode()
        {
            var env = new FakeEnv();
            var overlay = new Overlay(env, new SpeechPipeline(new FakeBackend()));
            var sys = new FakeSystem("x");
            overlay.With(sys);
            sys.BindMode(() => PlayMode.Off);

            sys.ForceHeld = true;
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.True(sys.LastShouldPlay);
        }

        [Fact]
        public void Cursor_DoesNotDriftWithoutControl()
        {
            var env = new FakeEnv { Control = false };
            var overlay = new Overlay(env, new SpeechPipeline(new FakeBackend()));
            overlay.Tick(1f, 1f, 0f, speed: 100f); // holding east, but no control
            Assert.Equal(env.Player, overlay.Cursor.Position);
        }

        // ---- announce pipeline ----

        [Fact]
        public void Announce_JoinsEnabledSystems_AndSpeaks()
        {
            var env = new FakeEnv();
            var backend = new FakeBackend();
            var overlay = new Overlay(env, new SpeechPipeline(backend));

            var a = new FakeSystem("alpha");
            var b = new FakeSystemB("beta");
            overlay.With(a).With(b);
            a.BindMode(() => PlayMode.Continuous);
            b.BindMode(() => PlayMode.Continuous);

            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "alpha; beta" }, backend.Spoken);
        }

        [Fact]
        public void Announce_SkipsDisabledSystems()
        {
            var env = new FakeEnv();
            var backend = new FakeBackend();
            var overlay = new Overlay(env, new SpeechPipeline(backend));

            var a = new FakeSystem("alpha");
            var b = new FakeSystemB("beta");
            overlay.With(a).With(b);
            a.BindMode(() => PlayMode.Continuous);
            b.BindMode(() => PlayMode.Off); // disabled -> yields nothing

            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "alpha" }, backend.Spoken);
        }

        [Fact]
        public void With_IsOnePerType_DuplicateReplaces()
        {
            var env = new FakeEnv();
            var overlay = new Overlay(env, new SpeechPipeline(new FakeBackend()));
            var first = new FakeSystem("one");
            var second = new FakeSystem("two");
            overlay.With(first).With(second);
            Assert.Same(second, overlay.Get<FakeSystem>());
        }
    }
}
