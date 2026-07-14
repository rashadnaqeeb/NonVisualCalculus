using System;

namespace NonVisualCalculus.Core.Audio
{
    /// <summary>
    /// The learn-sounds menu's wall-tone demo: sounds the four directional wall tones one at a time in
    /// the engine's fixed voice order (north, south, east, west), each at full proximity for
    /// <see cref="ToneSeconds"/> with a short rest between so they read as four distinct tones, scaled
    /// by the live wall-tone volume setting. The menu drives it with an unscaled per-frame tick (menus
    /// pause the game clock); the sequence releases its voices itself after the last tone, and
    /// <see cref="Stop"/> releases them early (the menu closed mid-demo).
    /// </summary>
    public sealed class WallTonePreview
    {
        public const float ToneSeconds = 0.5f;
        public const float RestSeconds = 0.25f;

        // The engine's voice order: 0 = north, 1 = south, 2 = east, 3 = west.
        private const int DirectionCount = 4;

        private readonly IAudioEngine _audio;
        private readonly Func<float> _volume;
        private readonly float[] _volumes = new float[DirectionCount];
        private IWallTones? _tones;
        private float _elapsed;

        public WallTonePreview(IAudioEngine audio, Func<float> volume)
        {
            _audio = audio;
            _volume = volume;
        }

        /// <summary>Whether a sequence is sounding (voices live).</summary>
        public bool Playing => _tones != null;

        /// <summary>Begin the sequence from the first tone; a sequence already sounding restarts.</summary>
        public void Start()
        {
            Stop();
            _tones = _audio.CreateWallTones();
            _elapsed = 0f;
            Drive();
        }

        /// <summary>Advance the sequence by <paramref name="dt"/> seconds (unscaled). Does nothing when
        /// no sequence is sounding.</summary>
        public void Tick(float dt)
        {
            if (_tones == null) return;
            _elapsed += dt;
            Drive();
        }

        /// <summary>Silence and release the voices - the sequence ran out, or its menu is gone.</summary>
        public void Stop()
        {
            _tones?.Dispose();
            _tones = null;
        }

        // One tone per slot: the direction sounds for ToneSeconds, then rests for RestSeconds before the
        // next. The volume is re-read every drive so a live setting change lands mid-demo.
        private void Drive()
        {
            const float slot = ToneSeconds + RestSeconds;
            int stage = (int)(_elapsed / slot);
            if (stage >= DirectionCount)
            {
                Stop();
                return;
            }
            Array.Clear(_volumes, 0, DirectionCount);
            if (_elapsed - stage * slot < ToneSeconds) _volumes[stage] = _volume();
            _tones!.Update(_volumes);
        }
    }
}
