using System.Numerics;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.World;
using DiscoAccess.Core.World.Overlays;
using DiscoAccess.Core.World.Overlays.Systems;
using Xunit;

namespace DiscoAccess.Tests
{
    public class WallToneSystemTests
    {
        private const float Range = 3.048f; // WOTR's 10 ft sense range, in metres

        // Scriptable walls per cardinal (distance in metres) and a control toggle. WallDistance is keyed off
        // the unit direction the system passes (N=+Z, S=-Z, E=+X, W=-X).
        private sealed class FakeEnv : IWorldEnvironment
        {
            public bool Control = true;
            public float North = 100f, South = 100f, East = 100f, West = 100f;
            public Vector3 PlayerPosition => Vector3.Zero;
            public bool HasControl => Control;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => intended;
            public float WallDistance(Vector3 from, Vector3 dir, float range)
            {
                if (dir.Z > 0.5f) return North;
                if (dir.Z < -0.5f) return South;
                if (dir.X > 0.5f) return East;
                return West;
            }
            public void FocusCamera(Vector3 point) { }
            public void ReleaseCamera() { }
        }

        private sealed class FakeTones : IWallTones
        {
            public float[] Last = System.Array.Empty<float>();
            public int Updates;
            public bool Disposed;
            public void Update(float[] volumes) { Last = (float[])volumes.Clone(); Updates++; }
            public void Dispose() => Disposed = true;
        }

        private sealed class FakeAudio : IAudioEngine
        {
            public int Created;
            public readonly FakeTones Tones = new FakeTones();
            public bool Available => true;
            public void PlayOneShot(float frequency, float seconds, float volume, float pan) { }
            public void PlayCue(AudioCue cue, float volume, float pan) { }
            public IWallTones CreateWallTones() { Created++; return Tones; }
        }

        private sealed class FakeBackend : ISpeechBackend
        {
            public bool IsAvailable => true;
            public void Speak(string text, bool interrupt) { }
            public void Stop() { }
        }

        private static Overlay NewOverlay(FakeEnv env) => new Overlay(env, new SpeechPipeline(new FakeBackend()));

        [Fact]
        public void Continuous_MapsEachCardinalToTheProximityCurve()
        {
            var env = new FakeEnv { North = 0f, South = 100f, East = Range / 2f, West = 100f };
            var audio = new FakeAudio();
            var sys = new WallToneSystem(env, audio);
            sys.BindMode(() => PlayMode.Continuous);
            var overlay = NewOverlay(env);
            overlay.With(sys);

            overlay.Tick(0.1f, 0f, 0f, 4f);

            Assert.Equal(1, audio.Created);
            float[] v = audio.Tones.Last;
            Assert.Equal(Spatial.ProximityVolume(0f, Range), v[0], 3);        // N: at the wall, full
            Assert.Equal(0f, v[1], 3);                                        // S: beyond range, silent
            Assert.Equal(Spatial.ProximityVolume(Range / 2f, Range), v[2], 3);// E: half range
            Assert.Equal(0f, v[3], 3);                                        // W: beyond range, silent
        }

        [Fact]
        public void Volume_ScalesEveryVoice()
        {
            var env = new FakeEnv { North = 0f };
            var audio = new FakeAudio();
            var sys = new WallToneSystem(env, audio);
            sys.BindMode(() => PlayMode.Continuous);
            sys.BindVolume(() => 0.5f);
            var overlay = NewOverlay(env);
            overlay.With(sys);

            overlay.Tick(0.1f, 0f, 0f, 4f);

            Assert.Equal(Spatial.ProximityVolume(0f, Range) * 0.5f, audio.Tones.Last[0], 3);
        }

        [Fact]
        public void NoControl_StandsDownWithoutBuildingVoices()
        {
            var env = new FakeEnv { North = 0f, Control = false };
            var audio = new FakeAudio();
            var sys = new WallToneSystem(env, audio);
            sys.BindMode(() => PlayMode.Continuous);
            var overlay = NewOverlay(env);
            overlay.With(sys);

            overlay.Tick(0.1f, 0f, 0f, 4f);

            Assert.Equal(0, audio.Created);       // nothing built while standing down
            Assert.Equal(0, audio.Tones.Updates); // and no voices driven
        }

        [Fact]
        public void LosingControl_MutesButKeepsTheVoices()
        {
            var env = new FakeEnv { North = 0f };
            var audio = new FakeAudio();
            var sys = new WallToneSystem(env, audio);
            sys.BindMode(() => PlayMode.Continuous);
            var overlay = NewOverlay(env);
            overlay.With(sys);

            overlay.Tick(0.1f, 0f, 0f, 4f);       // plays, builds the voices
            Assert.True(audio.Tones.Last[0] > 0f);

            env.Control = false;
            overlay.Tick(0.1f, 0f, 0f, 4f);       // cutscene: muted, voices kept

            Assert.Equal(1, audio.Created);       // not rebuilt
            Assert.Equal(new[] { 0f, 0f, 0f, 0f }, audio.Tones.Last);
        }

        [Fact]
        public void NotInputActive_MutesEvenWithControlAndContinuous()
        {
            var env = new FakeEnv { North = 0f };
            var audio = new FakeAudio();
            var sys = new WallToneSystem(env, audio);
            sys.BindMode(() => PlayMode.Continuous);
            var overlay = NewOverlay(env);
            overlay.With(sys);

            overlay.Tick(0.1f, 0f, 0f, 4f);       // owning: plays
            Assert.True(audio.Tones.Last[0] > 0f);

            overlay.InputActive = false;          // a menu floats over the world
            overlay.Tick(0.1f, 0f, 0f, 4f);

            Assert.Equal(1, audio.Created);       // voices kept
            Assert.Equal(new[] { 0f, 0f, 0f, 0f }, audio.Tones.Last);
        }

        [Fact]
        public void WhenMoving_PlaysWhileGlidingAndMutesAtRest()
        {
            var env = new FakeEnv { North = 0f };
            var audio = new FakeAudio();
            var sys = new WallToneSystem(env, audio);
            sys.BindMode(() => PlayMode.WhenMoving);
            var overlay = NewOverlay(env);
            overlay.With(sys);

            overlay.Tick(0.1f, 1f, 0f, 4f);       // gliding: plays
            Assert.True(audio.Tones.Last[0] > 0f);

            overlay.Tick(MotionTracker.LingerSec + 0.1f, 0f, 0f, 4f); // stopped past the linger: muted
            Assert.Equal(new[] { 0f, 0f, 0f, 0f }, audio.Tones.Last);
        }
    }
}
