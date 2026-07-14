using System;
using System.Collections.Generic;
using System.Numerics;
using NonVisualCalculus.Core.Audio;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class SpatialSourcesTests
    {
        private static (SpatialSources sources, FakeAudioEngine audio, List<string> warnings) Build()
        {
            var audio = new FakeAudioEngine { NextVoice = new FakeSpatialVoice() };
            var warnings = new List<string>();
            return (new SpatialSources(audio, warnings.Add), audio, warnings);
        }

        [Fact]
        public void Play_FiresImmediately_AtTheInitialPlacement()
        {
            var (sources, audio, _) = Build();

            // Source 5 m due east of the listener; gain halves per metre of distance.
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero, c => new Vector3(5f, 0f, 0f),
                         dist => 1f / (1f + dist), panWidth: 3f);

            var (cue, volume, placement) = Assert.Single(audio.Played);
            Assert.Equal(AudioCue.CursorEnter, cue);
            Assert.Equal(1f, placement.Pan, 3);
            Assert.Equal(1f / 6f, volume, 3);
        }

        [Fact]
        public void Tick_ReplacesAgainstTheMovedListener()
        {
            var (sources, audio, _) = Build();
            var listener = Vector3.Zero;
            var source = new Vector3(5f, 0f, 0f);
            sources.Play(AudioCue.CursorEnter, () => listener, c => source, _ => 1f, panWidth: 3f);

            // The listener walks east past the source: the voice must flip to its west (left).
            listener = new Vector3(10f, 0f, 0f);
            sources.Tick();

            var (cue, volume) = Assert.Single(audio.NextVoice!.Placements);
            Assert.Equal(-1f, cue.Pan, 3);
            Assert.True(cue.ItdSeconds < 0f); // the ear delay flips with the pan
        }

        [Fact]
        public void Tick_DropsFinishedVoices()
        {
            var (sources, audio, _) = Build();
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero, c => new Vector3(5f, 0f, 0f), _ => 1f, 3f);

            audio.NextVoice!.Finished = true;
            sources.Tick(); // dropped here
            sources.Tick(); // and never re-placed again
            Assert.Empty(audio.NextVoice.Placements);
        }

        [Fact]
        public void Tick_ThrowingSource_IsDroppedWithAWarning()
        {
            var (sources, audio, warnings) = Build();
            // Fires fine, then its proxy despawns: every later read throws.
            bool despawned = false;
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero,
                         c => despawned ? throw new InvalidOperationException("despawned") : new Vector3(5f, 0f, 0f),
                         _ => 1f, 3f);

            despawned = true;
            sources.Tick(); // the source throws: dropped, warned, no crash
            sources.Tick(); // and not retried
            Assert.Single(warnings);
            Assert.Empty(audio.NextVoice!.Placements);
        }

        [Fact]
        public void Tick_DeadEngine_DropsAllWithOneWarning()
        {
            var (sources, audio, warnings) = Build();
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero, c => new Vector3(5f, 0f, 0f), _ => 1f, 3f);

            // The output device dies mid-session: its voices' Read never runs again, so none can finish.
            audio.Available = false;
            sources.Tick(); // dropped, warned
            sources.Tick(); // empty now: no second warning, no per-frame zombie work
            Assert.Single(warnings);
            Assert.Empty(audio.NextVoice!.Placements);
        }

        [Fact]
        public void Clear_StopsTracking()
        {
            var (sources, audio, _) = Build();
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero, c => new Vector3(5f, 0f, 0f), _ => 1f, 3f);

            sources.Clear(); // the world exited / the player teleported
            sources.Tick();
            Assert.Empty(audio.NextVoice!.Placements);
        }

        [Fact]
        public void Play_WithoutAVoice_IsNotTracked()
        {
            var audio = new FakeAudioEngine(); // NextVoice null: device/asset unavailable
            var sources = new SpatialSources(audio, _ => { });
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero, c => new Vector3(5f, 0f, 0f), _ => 1f, 3f);

            sources.Tick(); // nothing to re-place, nothing to crash on
            Assert.Single(audio.Played); // the fire itself still reached the engine
        }

        [Fact]
        public void Play_ComputesTheSourceFromTheCurrentListener()
        {
            // The sourceAt function receives the live listener, so a wide thing can answer with its
            // nearest point: here it mirrors the listener's own X, staying 2 m north of it.
            var (sources, audio, _) = Build();
            var listener = new Vector3(7f, 0f, 0f);
            sources.Play(AudioCue.CursorEnter, () => listener, c => new Vector3(c.X, 0f, c.Z + 2f), _ => 1f, 3f);

            var (_, _, placement) = Assert.Single(audio.Played);
            Assert.Equal(0f, placement.Pan, 3);         // straight ahead of wherever the listener is
            Assert.Equal(0f, placement.RearShelfDb, 3); // ahead: bright
        }

        [Fact]
        public void Play_GainDistance_CountsElevation()
        {
            // A thing 4 m north and 3 m up: direction is planar (due north, centred) but the gain hears
            // the full 3D distance (5 m), matching the spoken readout's Geo.Distance - the ear and the
            // voice must agree on how far away it is.
            var (sources, audio, _) = Build();
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero, c => new Vector3(0f, 3f, 4f),
                         dist => dist, panWidth: 3f);

            var (_, volume, placement) = Assert.Single(audio.Played);
            Assert.Equal(0f, placement.Pan, 3);
            Assert.Equal(5f, volume, 3);
        }

        [Fact]
        public void Play_ThrowingSource_IsSkippedWithAWarning()
        {
            // The proxy despawned between the caller choosing it and the fire: no cue, no crash, warned.
            var (sources, audio, warnings) = Build();
            sources.Play(AudioCue.CursorEnter, () => Vector3.Zero,
                         c => throw new InvalidOperationException("despawned"), _ => 1f, 3f);

            Assert.Single(warnings);
            Assert.Empty(audio.Played);
            sources.Tick(); // nothing was tracked
            Assert.Empty(audio.NextVoice!.Placements);
        }
    }
}
