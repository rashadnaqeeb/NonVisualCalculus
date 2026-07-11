using DiscoAccess.Core.Audio;
using Xunit;

namespace DiscoAccess.Tests
{
    /// <summary>
    /// The learn-sounds menu's wall-tone demo: four tones in the engine's voice order (north, south,
    /// east, west), each sounding alone for its tone window with a silent rest between, voices released
    /// when the sequence runs out or is stopped early.
    /// </summary>
    public class WallTonePreviewTests
    {
        private const float Slot = WallTonePreview.ToneSeconds + WallTonePreview.RestSeconds;

        private sealed class FakeEngine : IAudioEngine
        {
            public FakeTones? Tones;
            public int Created;

            public bool Available => true;
            public void PlayOneShot(float frequency, float seconds, float volume, float pan) { }
            public ISpatialVoice? PlayCue(AudioCue cue, float volume, SpatialCue placement) => null;

            public IWallTones CreateWallTones()
            {
                Created++;
                return Tones = new FakeTones();
            }
        }

        private sealed class FakeTones : IWallTones
        {
            public readonly float[] Last = new float[4];
            public bool Disposed;

            public void Update(float[] volumes) => volumes.CopyTo(Last, 0);
            public void Dispose() => Disposed = true;
        }

        private static void AssertOnly(FakeTones tones, int direction, float volume)
        {
            for (int i = 0; i < 4; i++)
                Assert.Equal(i == direction ? volume : 0f, tones.Last[i]);
        }

        [Fact]
        public void Start_SoundsNorthAlone()
        {
            var engine = new FakeEngine();
            var preview = new WallTonePreview(engine, () => 0.5f);

            preview.Start();

            Assert.True(preview.Playing);
            AssertOnly(engine.Tones!, 0, 0.5f);
        }

        [Fact]
        public void Sequence_RunsNorthSouthEastWest_ThenReleases()
        {
            var engine = new FakeEngine();
            var preview = new WallTonePreview(engine, () => 1f);

            preview.Start();
            var tones = engine.Tones!;
            for (int direction = 1; direction < 4; direction++)
            {
                preview.Tick(Slot);
                AssertOnly(tones, direction, 1f);
            }

            preview.Tick(Slot);
            Assert.True(tones.Disposed);
            Assert.False(preview.Playing);
        }

        [Fact]
        public void RestBetweenTones_IsSilentButStillPlaying()
        {
            var engine = new FakeEngine();
            var preview = new WallTonePreview(engine, () => 1f);

            preview.Start();
            preview.Tick(WallTonePreview.ToneSeconds);

            Assert.True(preview.Playing);
            Assert.All(engine.Tones!.Last, v => Assert.Equal(0f, v));
        }

        [Fact]
        public void Stop_MidSequence_ReleasesVoices()
        {
            var engine = new FakeEngine();
            var preview = new WallTonePreview(engine, () => 1f);

            preview.Start();
            preview.Stop();

            Assert.True(engine.Tones!.Disposed);
            Assert.False(preview.Playing);

            // A tick after stop is inert: no new voices, no update.
            preview.Tick(0.1f);
            Assert.Equal(1, engine.Created);
        }

        [Fact]
        public void Start_WhileSounding_RestartsWithFreshVoices()
        {
            var engine = new FakeEngine();
            var preview = new WallTonePreview(engine, () => 1f);

            preview.Start();
            var first = engine.Tones!;
            preview.Tick(Slot); // partway in, on the second tone

            preview.Start();

            Assert.True(first.Disposed);
            Assert.Equal(2, engine.Created);
            AssertOnly(engine.Tones!, 0, 1f); // back at north
        }

        [Fact]
        public void Volume_IsReadLive_EachDrive()
        {
            var engine = new FakeEngine();
            float volume = 0.2f;
            var preview = new WallTonePreview(engine, () => volume);

            preview.Start();
            AssertOnly(engine.Tones!, 0, 0.2f);

            volume = 0.9f;
            preview.Tick(Slot);
            AssertOnly(engine.Tones!, 1, 0.9f);
        }
    }
}
