using System;

namespace NonVisualCalculus.Core.Audio
{
    /// <summary>
    /// The audio backend for the spatial soundscape (sonar pings, wall tones), owned by the host (the
    /// output device is a native handle, so it lives beside Prism and never reloads). The sensing systems
    /// compute the placement (pan + interaural delay + rear filter, a <see cref="SpatialCue"/>) and volume
    /// themselves (via <see cref="NonVisualCalculus.Core.World.Spatial"/>) and hand the already-placed sound
    /// here; the engine just plays it. Kept an interface so Core stays engine-free and the implementation
    /// (NAudio) lives in the host.
    /// </summary>
    public interface IAudioEngine
    {
        /// <summary>Whether the output device is usable (false if it failed to open).</summary>
        bool Available { get; }

        /// <summary>Fire a positional one-shot already spatialized by the caller: a generated tone of
        /// <paramref name="frequency"/> Hz for <paramref name="seconds"/>, at <paramref name="volume"/>
        /// (0..1) and stereo <paramref name="pan"/> (-1 left .. 1 right). Procedural for now (per-category
        /// pitch); a sampled variant joins it when sound assets are authored.</summary>
        void PlayOneShot(float frequency, float seconds, float volume, float pan);

        /// <summary>Fire a named one-shot cue (a sampled WAV the engine owns) already spatialized by the
        /// caller: at <paramref name="volume"/> (0..1) and the full stereo <paramref name="placement"/>
        /// (pan, interaural delay, rear lowpass). Used for the cursor's enter/exit blips and the scanner's
        /// review ping. Returns the live voice handle so a tracked source (<see cref="SpatialSources"/>)
        /// can re-place it each frame while it plays; null (nothing plays) when the device or the asset is
        /// unavailable, rather than throwing.</summary>
        ISpatialVoice? PlayCue(AudioCue cue, float volume, SpatialCue placement);

        /// <summary>Create the four directional wall-tone voices (driven each frame). Dispose removes them
        /// from the mixer.</summary>
        IWallTones CreateWallTones();
    }

    /// <summary>Four looping directional wall-tone voices, driven each frame with a 0..1 volume per
    /// direction (index 0 = north, 1 = south, 2 = east, 3 = west). The engine places each at its fixed
    /// compass pan; Dispose stops and releases them.</summary>
    public interface IWallTones : IDisposable
    {
        void Update(float[] volumes);
    }
}
