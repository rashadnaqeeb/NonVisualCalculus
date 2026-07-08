using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiscoAccess.Core.Audio;
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
        // A scripted environment: fixed player position, controllable HasControl, a TraceMove that either
        // passes the intended point through (open ground) or clamps to a wall, and scriptable view/fog
        // bounds (everything in view and unfogged by default).
        private sealed class FakeEnv : IWorldEnvironment
        {
            public Vector3 Player = new Vector3(0f, 0f, 0f);
            public bool Control = true;
            public Vector3? Wall; // when set, TraceMove returns this regardless of intent (blocked)
            public Func<Vector3, bool> ViewFn = _ => true;
            public Func<Vector3, Vector3> ClampFn = p => p;
            public Func<Vector3, bool> FogFn = _ => false;
            public Vector3 PlayerPosition => Player;
            public bool HasControl => Control;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => Wall ?? intended;
            public float WallDistance(Vector3 from, Vector3 direction, float range) => range; // open in every direction
            public bool InView(Vector3 point) => ViewFn(point);
            public Vector3 ClampToView(Vector3 point) => ClampFn(point);
            public bool IsFogged(Vector3 point) => FogFn(point);
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

        private static Overlay NewOverlay(FakeEnv env, ISpeechBackend? backend = null, FakeAudioEngine? audio = null)
            => new Overlay(env, new SpeechPipeline(backend ?? new FakeBackend()),
                           new SpatialSources(audio ?? new FakeAudioEngine(), _ => { }));

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
        public void OnExit_UnpinsCursor_BackOntoPlayer()
        {
            var env = new FakeEnv();
            var overlay = NewOverlay(env);
            overlay.Tick(1f, 1f, 0f, speed: 4f); // glide east: the cursor pins away from the player
            Assert.True(overlay.Cursor.IsPinned);

            overlay.OnExit(); // the world view closed (a conversation, a menu)

            env.Player = new Vector3(3f, 0f, 3f);
            Assert.False(overlay.Cursor.IsPinned);
            Assert.Equal(env.Player, overlay.Cursor.Position); // reopens riding the character
        }

        // ---- the senses' bounds: view edge, fog, and the impassable bump ----

        [Fact]
        public void Glide_BlockedAtViewEdge_StaysAndBumpsOnce()
        {
            var env = new FakeEnv { ViewFn = p => p.X <= 2f };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);

            overlay.Tick(1f, 1f, 0f, speed: 4f); // one 4 m step east lands out of view: refused
            Assert.Equal(env.Player, overlay.Cursor.Position);
            var (cue, _, placement) = Assert.Single(audio.Played);
            Assert.Equal(AudioCue.CursorImpassable, cue);
            Assert.True(placement.Pan > 0f); // bumped toward the refused east

            overlay.Tick(1f, 1f, 0f, speed: 4f); // still holding into the edge: no drone
            Assert.Single(audio.Played);

            overlay.Tick(0.1f, 0f, 0f, speed: 4f); // releasing re-arms
            overlay.Tick(1f, 1f, 0f, speed: 4f);
            Assert.Equal(2, audio.Played.Count);
        }

        [Fact]
        public void Glide_Diagonal_SlidesAlongViewEdge_Silently()
        {
            var env = new FakeEnv { ViewFn = p => p.X <= 0.5f };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);

            overlay.Tick(1f, 1f, 1f, speed: 4f); // northeast: east is out of view, north still passes
            Assert.Equal(0f, overlay.Cursor.Position.X, 3);
            Assert.True(overlay.Cursor.Position.Z > 1f); // slid north along the edge
            Assert.Empty(audio.Played);
        }

        [Fact]
        public void Glide_IntoFog_StaysAndBumps()
        {
            // One 4 m stride lands 4 m deep in fog - past the fringe in a single step - so it refuses whole.
            var env = new FakeEnv { FogFn = p => p.X > 2f };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);

            overlay.Tick(1f, 1f, 0f, speed: 4f);
            Assert.Equal(env.Player, overlay.Cursor.Position);
            Assert.Equal(AudioCue.CursorImpassable, Assert.Single(audio.Cues));
        }

        [Fact]
        public void Glide_EntersFogToTheFringe_ThenBumps()
        {
            // Fog starts at x=2; small strokes may nose FogFringe metres into it (the dim ground a sighted
            // player sees past an unseen zone's edge) before the Fog block refuses.
            var env = new FakeEnv { FogFn = p => p.X > 2f };
            var cursor = new Cursor(env);

            GlideOutcome last = default;
            for (int i = 0; i < 100; i++)
            {
                last = cursor.Glide(1f, 0f, dt: 0.1f, speed: 1f); // 0.1 m strokes east
                if (!last.Moved) break;
            }
            Assert.Equal(GlideBlock.Fog, last.Block);
            Assert.InRange(cursor.Position.X, 2f + Cursor.FogFringe - 0.2f, 2f + Cursor.FogFringe + 0.01f);
        }

        [Fact]
        public void FogFringe_ResetsOnClearGround()
        {
            var env = new FakeEnv { FogFn = p => p.X > 2f };
            var cursor = new Cursor(env);

            for (int i = 0; i < 100 && cursor.Glide(1f, 0f, 0.1f, 1f).Moved; i++) { } // reach the fringe limit
            // Retreat is always passable: a stroke back toward the entry point shrinks the fringe distance,
            // so a cursor at the limit is never frozen in the murk.
            for (int i = 0; i < 30; i++) cursor.Glide(-1f, 0f, 0.1f, 1f);              // back out to x=1
            Assert.True(cursor.Position.X < 2f);

            GlideOutcome last = default;
            for (int i = 0; i < 100; i++)
            {
                last = cursor.Glide(1f, 0f, 0.1f, 1f);
                if (!last.Moved) break;
            }
            Assert.Equal(GlideBlock.Fog, last.Block); // a fresh full fringe, not a spent one
            Assert.InRange(cursor.Position.X, 2f + Cursor.FogFringe - 0.2f, 2f + Cursor.FogFringe + 0.01f);
        }

        [Fact]
        public void FogReveal_FreesAParkedCursor()
        {
            // The player walked out and revealed the zone while the cursor sat at its fringe limit: the
            // next stroke moves freely, with no stale depth held over.
            var env = new FakeEnv { FogFn = p => p.X > 2f };
            var cursor = new Cursor(env);
            for (int i = 0; i < 100 && cursor.Glide(1f, 0f, 0.1f, 1f).Moved; i++) { }

            env.FogFn = _ => false;
            var outcome = cursor.Glide(1f, 0f, dt: 1f, speed: 4f);
            Assert.True(outcome.Moved);
            Assert.Equal(GlideBlock.None, outcome.Block);
            Assert.True(cursor.Position.X > 2f + Cursor.FogFringe);
        }

        // ---- the unrestricted cursor (testing aid): bounds pass, fog cues sound the crossings ----

        [Fact]
        public void UnrestrictedGlide_PassesViewEdgeAndFog_WithoutBumping()
        {
            var env = new FakeEnv { ViewFn = p => p.X <= 2f, FogFn = p => p.X > 3f };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);
            overlay.Cursor.BindUnrestricted(() => true);

            overlay.Tick(1f, 1f, 0f, speed: 8f); // one 8 m step east: out of view AND deep in fog
            Assert.Equal(8f, overlay.Cursor.Position.X, 3);
            Assert.DoesNotContain(AudioCue.CursorImpassable, audio.Cues);
        }

        [Fact]
        public void UnrestrictedCrossing_PlaysFogEnter_ThenExitOnReturn()
        {
            var env = new FakeEnv { FogFn = p => p.X > 2f };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);
            overlay.Cursor.BindUnrestricted(() => true);

            overlay.Tick(1f, 1f, 0f, speed: 4f); // to x=4: fogged ground
            Assert.Equal(new[] { AudioCue.CursorFogEnter }, audio.Cues);

            overlay.Tick(1f, 1f, 0f, speed: 4f); // deeper (x=8): still outside, no re-fire
            Assert.Single(audio.Played);

            overlay.Tick(2f, -1f, 0f, speed: 4f); // back to x=0: clear ground
            Assert.Equal(new[] { AudioCue.CursorFogEnter, AudioCue.CursorFogExit }, audio.Cues);
        }

        [Fact]
        public void UnrestrictedCursor_IsNotFrameDragged()
        {
            var env = new FakeEnv { ViewFn = p => p.X <= 3f, ClampFn = p => new Vector3(3f, p.Y, p.Z) };
            var overlay = NewOverlay(env);
            overlay.Cursor.BindUnrestricted(() => true);
            overlay.Cursor.Position = new Vector3(5f, 0f, 0f);

            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Equal(new Vector3(5f, 0f, 0f), overlay.Cursor.Position); // left where it roamed
        }

        [Fact]
        public void UnrestrictedFogCues_InertWhileInputInactive()
        {
            var env = new FakeEnv { FogFn = p => p.X > 2f };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);
            overlay.Cursor.BindUnrestricted(() => true);
            overlay.Cursor.Position = new Vector3(4f, 0f, 0f); // planted on fogged ground
            overlay.InputActive = false; // a menu floats over the world

            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Empty(audio.Played); // the crossing is judged only while driven

            overlay.InputActive = true; // the menu closed: the pending crossing cues now
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Equal(new[] { AudioCue.CursorFogEnter }, audio.Cues);
        }

        [Fact]
        public void TurningRestrictionBackOn_PlaysFogExit_AndFrameDragsHome()
        {
            var env = new FakeEnv { ViewFn = p => p.X <= 3f, ClampFn = p => new Vector3(3f, p.Y, p.Z) };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);
            bool unrestricted = true;
            overlay.Cursor.BindUnrestricted(() => unrestricted);

            overlay.Tick(1f, 1f, 0f, speed: 8f); // roam out of view (x=8)
            Assert.Equal(new[] { AudioCue.CursorFogEnter }, audio.Cues);

            unrestricted = false; // the toggle turned off with the cursor stranded outside
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Equal(new Vector3(3f, 0f, 0f), overlay.Cursor.Position); // dragged back in view
            Assert.Equal(new[] { AudioCue.CursorFogEnter, AudioCue.CursorFogExit }, audio.Cues);
        }

        [Fact]
        public void Glide_WallStop_StaysSilent()
        {
            var env = new FakeEnv { Wall = Vector3.Zero }; // navmesh clamps every step back to the origin
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);

            overlay.Tick(1f, 1f, 0f, speed: 4f); // pinned, but by a wall: the wall tones own that story
            Assert.Empty(audio.Played);
        }

        [Fact]
        public void FrameDrag_PullsCursorBackInView()
        {
            var env = new FakeEnv();
            var overlay = NewOverlay(env);
            overlay.Cursor.Position = new Vector3(5f, 0f, 0f);

            env.ViewFn = p => p.X <= 3f; // the frame moved (the character walked west)
            env.ClampFn = p => new Vector3(3f, p.Y, p.Z);
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Equal(new Vector3(3f, 0f, 0f), overlay.Cursor.Position);
        }

        [Fact]
        public void FrameDrag_AndBump_InertWhileInputInactive()
        {
            var env = new FakeEnv { ViewFn = p => p.X <= 3f, ClampFn = p => new Vector3(3f, p.Y, p.Z) };
            var audio = new FakeAudioEngine();
            var overlay = NewOverlay(env, audio: audio);
            overlay.Cursor.Position = new Vector3(5f, 0f, 0f);
            overlay.InputActive = false; // a menu floats over the world

            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Equal(new Vector3(5f, 0f, 0f), overlay.Cursor.Position); // not dragged
            Assert.Empty(audio.Played);
        }

        [Fact]
        public void MotionTracker_LingersThenClears()
        {
            var m = new MotionTracker();
            m.Update(0.1f, moving: true); // keys held
            Assert.True(m.MovingRecently);
            m.Update(0.1f, moving: false); // released: the linger holds it
            Assert.True(m.MovingRecently);
            m.Update(MotionTracker.LingerSec, moving: false); // released past the linger
            Assert.False(m.MovingRecently);
        }

        [Fact]
        public void KeylessCursorMoves_DoNotCountAsMoving()
        {
            // The cursor repositioned with no keys held - a recenter, a scanner landing, the frame-drag -
            // must not wake the WhenMoving systems.
            var env = new FakeEnv();
            var overlay = NewOverlay(env);
            var sys = new FakeSystem("x");
            overlay.With(sys);
            sys.BindMode(() => PlayMode.WhenMoving);

            overlay.Cursor.Position = new Vector3(5f, 0f, 0f); // planted, not glided
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.False(sys.LastShouldPlay);

            env.ViewFn = p => p.X <= 3f; // the character walked: the frame-drag pulls the cursor
            env.ClampFn = p => new Vector3(3f, p.Y, p.Z);
            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.Equal(new Vector3(3f, 0f, 0f), overlay.Cursor.Position); // dragged
            Assert.False(sys.LastShouldPlay);                               // still silent
        }

        [Fact]
        public void HeldKeysAgainstAWall_StillCountAsMoving()
        {
            var env = new FakeEnv { Wall = Vector3.Zero }; // every step clamps back to the origin
            var overlay = NewOverlay(env);
            var sys = new FakeSystem("x");
            overlay.With(sys);
            sys.BindMode(() => PlayMode.WhenMoving);

            overlay.Tick(0.1f, 1f, 0f, speed: 4f); // holding east into the wall: no motion, still driving
            Assert.True(sys.LastShouldPlay);
        }

        [Fact]
        public void FrameDrag_LeavesAnUnpinnedCursorAlone()
        {
            // An unpinned cursor rides the player; clamping it against a camera that hasn't caught up to a
            // just-repositioned character would pin it somewhere stale.
            var env = new FakeEnv { ViewFn = _ => false, ClampFn = _ => new Vector3(99f, 0f, 0f) };
            var overlay = NewOverlay(env);

            overlay.Tick(0.1f, 0f, 0f, speed: 4f);
            Assert.False(overlay.Cursor.IsPinned);
            Assert.Equal(env.Player, overlay.Cursor.Position);
        }

        // ---- play-mode gating ----

        [Fact]
        public void ShouldPlay_FollowsModeAndMotion()
        {
            var env = new FakeEnv();
            var overlay = NewOverlay(env);
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
            var overlay = NewOverlay(env);
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
            var overlay = NewOverlay(env);
            overlay.Tick(1f, 1f, 0f, speed: 100f); // holding east, but no control
            Assert.Equal(env.Player, overlay.Cursor.Position);
        }

        // ---- announce pipeline ----

        [Fact]
        public void Announce_JoinsEnabledSystems_AndSpeaks()
        {
            var env = new FakeEnv();
            var backend = new FakeBackend();
            var overlay = NewOverlay(env, backend);

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
            var overlay = NewOverlay(env, backend);

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
            var overlay = NewOverlay(env);
            var first = new FakeSystem("one");
            var second = new FakeSystem("two");
            overlay.With(first).With(second);
            Assert.Same(second, overlay.Get<FakeSystem>());
        }
    }
}
