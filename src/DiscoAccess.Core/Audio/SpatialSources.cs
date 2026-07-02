using System;
using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.World;

namespace DiscoAccess.Core.Audio
{
    /// <summary>
    /// Keeps positional one-shots alive as scene SOURCES, the WOTR model: while a voice is still audible
    /// it is re-spatialised every frame against the moving listener, so pan / gain / ITD / front-back
    /// filter follow the cursor (and a walking player) instead of freezing at fire time. Ticked on the
    /// main thread from the world reader's frame loop; the per-voice updates are smoothed inside
    /// <see cref="ISpatialVoice"/> so movement never clicks.
    ///
    /// A source is described by three live functions so the caller keeps all the geometry/volume maths and
    /// nothing is cached: <c>listener</c> (where it's heard from now), <c>sourceAt</c> (the source's world
    /// point given the listener - lets a wide thing track its nearest point, a compact one just return its
    /// centre), and <c>gain</c> (distance to volume, including the caller's own falloff and volume
    /// setting). Self-cleaning: a voice that reports Finished is dropped, and a source whose functions
    /// throw (its proxy despawned mid-ping) is dropped with a warning, letting the voice drain on its own.
    /// </summary>
    public sealed class SpatialSources
    {
        private sealed class Src
        {
            public ISpatialVoice Voice = null!;
            public Func<Vector3> Listener = null!;
            public Func<Vector3, Vector3> SourceAt = null!;
            public Func<float, float> Gain = null!;
            public float PanWidth;
        }

        private readonly IAudioEngine _audio;
        private readonly Action<string> _warn;
        // Main-thread only (Play from the sensing systems' tick, Tick from the frame loop) - no lock needed.
        private readonly List<Src> _live = new List<Src>();

        public SpatialSources(IAudioEngine audio, Action<string> warn)
        {
            _audio = audio;
            _warn = warn;
        }

        /// <summary>Fire a tracked positional one-shot. Plays immediately at its current placement and is
        /// then re-placed each frame until it finishes. No-op if the engine couldn't start the voice, or
        /// if the source's functions throw at fire time (its proxy despawned since the caller chose it) -
        /// the same skip a source dying mid-cue gets in <see cref="Tick"/>.</summary>
        public void Play(AudioCue cue, Func<Vector3> listener, Func<Vector3, Vector3> sourceAt,
                         Func<float, float> gain, float panWidth)
        {
            var src = new Src { Listener = listener, SourceAt = sourceAt, Gain = gain, PanWidth = panWidth };
            SpatialCue placement;
            float volume;
            try { (placement, volume) = Placement(src); }
            catch (Exception e)
            {
                _warn("[spatial-src] source dropped at fire time: " + e.Message);
                return;
            }
            ISpatialVoice? voice = _audio.PlayCue(cue, volume, placement);
            if (voice == null) return;
            src.Voice = voice;
            _live.Add(src);
        }

        /// <summary>Stop tracking every live source (the world exited, the player teleported): their
        /// placements are meaningless against the new listener, so the voices just drain where they last
        /// sounded.</summary>
        public void Clear() => _live.Clear();

        /// <summary>Re-spatialise every live source against the current listener. Drops finished voices.</summary>
        public void Tick()
        {
            // A dead output device (it stops mid-session with an error; the engine logs it) means Read
            // never runs again, so no voice can ever finish - stand down instead of re-placing zombies
            // every frame forever.
            if (_live.Count > 0 && !_audio.Available)
            {
                _live.Clear();
                _warn("[spatial-src] audio engine unavailable; dropped all tracked cues");
                return;
            }

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Src src = _live[i];
                if (src.Voice.Finished) { _live.RemoveAt(i); continue; }
                try
                {
                    (SpatialCue placement, float volume) = Placement(src);
                    src.Voice.SetPlacement(placement, volume);
                }
                catch (Exception e)
                {
                    // A stale source (its proxy despawned mid-cue, the area changed) - stop tracking and let
                    // the voice drain at its last placement.
                    _live.RemoveAt(i);
                    _warn("[spatial-src] source dropped mid-cue: " + e.Message);
                }
            }
        }

        // The one placement computation both the fire (Play) and every re-place (Tick) go through, so the
        // initial and tracked placements can never disagree. Direction (pan, ITD, rear shelf) is planar -
        // a flat-map question - but the gain distance is 3D, matching the spoken readout's Geo.Distance:
        // a thing up a ledge must SOUND as far as speech says it is, or the two senses contradict.
        private (SpatialCue placement, float volume) Placement(Src src)
        {
            Vector3 c = src.Listener();
            Vector3 s = src.SourceAt(c);
            float dx = s.X - c.X, dy = s.Y - c.Y, dz = s.Z - c.Z;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return (Spatial.Cue(dx, dz, src.PanWidth), src.Gain(dist));
        }
    }
}
