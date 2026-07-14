using System.Collections.Generic;
using System.Linq;
using NonVisualCalculus.Core.Audio;

namespace NonVisualCalculus.Tests
{
    /// <summary>
    /// The one recording <see cref="IAudioEngine"/> stub shared by every suite, so an interface change is
    /// a single edit instead of a fan-out across per-suite copies. Records every cue with its full
    /// placement (<see cref="Played"/>, with <see cref="Cues"/> as the names-only view), hands
    /// <see cref="NextVoice"/> back from PlayCue (null by default: the untracked, engine-couldn't-start
    /// case), and counts wall-tone voice creation.
    /// </summary>
    internal sealed class FakeAudioEngine : IAudioEngine
    {
        public bool Available { get; set; } = true;

        /// <summary>What PlayCue returns; set a <see cref="FakeSpatialVoice"/> to test tracking.</summary>
        public FakeSpatialVoice? NextVoice;

        public readonly List<(AudioCue cue, float volume, SpatialCue placement)> Played =
            new List<(AudioCue, float, SpatialCue)>();

        /// <summary>The fired cue names in order, for the common name-only assertions.</summary>
        public IEnumerable<AudioCue> Cues => Played.Select(p => p.cue);

        public int Created;
        public readonly FakeWallTones Tones = new FakeWallTones();

        public void PlayOneShot(float frequency, float seconds, float volume, float pan) { }

        public ISpatialVoice? PlayCue(AudioCue cue, float volume, SpatialCue placement)
        {
            Played.Add((cue, volume, placement));
            return NextVoice;
        }

        public IWallTones CreateWallTones()
        {
            Created++;
            return Tones;
        }
    }

    /// <summary>A tracked voice recording every re-placement, never finishing until told to.</summary>
    internal sealed class FakeSpatialVoice : ISpatialVoice
    {
        public bool Finished { get; set; }
        public readonly List<(SpatialCue cue, float volume)> Placements =
            new List<(SpatialCue, float)>();
        public void SetPlacement(SpatialCue cue, float volume) => Placements.Add((cue, volume));
    }

    /// <summary>Wall-tone voices recording the volumes driven into them.</summary>
    internal sealed class FakeWallTones : IWallTones
    {
        public float[] Last = System.Array.Empty<float>();
        public int Updates;
        public bool Disposed;
        public void Update(float[] volumes) { Last = (float[])volumes.Clone(); Updates++; }
        public void Dispose() => Disposed = true;
    }
}
