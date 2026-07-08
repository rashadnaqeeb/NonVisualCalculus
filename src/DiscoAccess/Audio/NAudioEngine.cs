using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using DiscoAccess.Core.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DiscoAccess.Audio
{
    /// <summary>
    /// Our own stereo audio backend for the spatial soundscape, independent of the game's audio so the cues
    /// aren't colored by its mixer/DSP. ONE shared <see cref="MixingSampleProvider"/> feeds ONE
    /// <see cref="WaveOutEvent"/>; every voice (one-shots, wall tones) is an input on that single mixer.
    /// Lives in the permanent host (the device is a native handle) and is lent to the module through
    /// <c>IModHost.Audio</c>. The device opens lazily on first use and self-disables on failure, so a machine
    /// with no audio device never crashes the mod. Ported from the WOTR exploration mod's NAudio engine; the
    /// wall tones loop WOTR's set-1 WAV assets, and the positional cues carry its full spatial model
    /// (constant-power pan + interaural time difference + the rear shelf - see <see cref="SpatialCue"/>).
    /// </summary>
    internal sealed class NAudioEngine : IAudioEngine, IDisposable
    {
        public const int Rate = 44100;

        private readonly ManualLogSource _log;
        private MixingSampleProvider _mixer;
        private IWavePlayer _out;
        private bool _failed;
        // WAVs decoded to mono once and reused (wall tones across world entries; cursor cues across the
        // session) - keyed by full path, so wall tones and cursor cues share one cache.
        private readonly Dictionary<string, float[]> _clipCache = new Dictionary<string, float[]>();
        // Paths whose decode has already been warned about, so a genuinely missing asset logs once instead of
        // once per glide-blip - the failure itself is not cached (see LoadMono).
        private readonly HashSet<string> _warnedClips = new HashSet<string>();
        private string _assetRoot;

        public NAudioEngine(ManualLogSource log) { _log = log; }

        // The WAV assets deploy beside this assembly under assets/audio: the set-1 wall tones, the cursor
        // enter/exit/impassable cues, and the per-category thing cues (sonar sweep + scanner review ping).
        private string AssetRoot => _assetRoot ??= Path.Combine(
            Path.GetDirectoryName(typeof(NAudioEngine).Assembly.Location) ?? ".", "assets", "audio");

        private string WallDir => Path.Combine(AssetRoot, "walltones", "1");
        private string CueDir => Path.Combine(AssetRoot, "cursor");
        private string ThingDir => Path.Combine(AssetRoot, "interactables");

        public bool Available => !_failed;

        // 100 ms buffer to ride through managed-thread (GC/CPU) pauses without underrunning into clicks.
        private bool EnsureStarted()
        {
            if (_out != null) return true;
            if (_failed) return false;
            try
            {
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2)) { ReadFully = true };
                var limiter = new SoftLimiter(_mixer);
                _out = new WaveOutEvent { DesiredLatency = 100, NumberOfBuffers = 4 };
                // A device dying mid-session (unplugged headphones, driver error) stops playback with an
                // exception; flag it so Available turns false - consumers (the tracked-source list) stand
                // down instead of feeding voices whose Read will never run again. A deliberate Stop/Dispose
                // raises the event with no exception and is not a failure.
                _out.PlaybackStopped += (s, e) =>
                {
                    if (e.Exception == null) return;
                    _failed = true;
                    _log?.LogWarning("[audio] output stopped (" + e.Exception.Message + "); spatial cues disabled");
                };
                _out.Init(limiter);
                _out.Play();
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _log?.LogWarning("[audio] output device unavailable; spatial cues disabled: " + e.Message);
                return false;
            }
        }

        internal void Add(ISampleProvider p) { if (EnsureStarted()) _mixer.AddMixerInput(p); }
        internal void Remove(ISampleProvider p)
        {
            try { _mixer?.RemoveMixerInput(p); }
            catch (Exception e) { _log?.LogWarning("[audio] mixer remove failed: " + e.Message); }
        }

        // Constant-power pan: a single source for the pan-to-(left,right) gain law, shared by the one-shot
        // and the wall-tone voices so they place a given bearing identically.
        internal static void PanGains(float pan, out float left, out float right)
        {
            float t = (pan + 1f) * 0.5f * (float)(Math.PI / 2.0); // -1 = hard left, +1 = hard right
            left = (float)Math.Cos(t);
            right = (float)Math.Sin(t);
        }

        public void PlayOneShot(float frequency, float seconds, float volume, float pan)
        {
            if (!EnsureStarted()) return;
            _mixer.AddMixerInput(new ToneShot(Rate, frequency, seconds, volume, pan));
        }

        public ISpatialVoice PlayCue(AudioCue cue, float volume, SpatialCue placement)
        {
            if (!EnsureStarted()) return null;
            float[] clip = LoadMono(CuePath(cue));
            if (clip.Length == 0) return null; // missing/unreadable asset already logged by LoadMono
            var voice = new PositionalEmitter(Rate, clip);
            voice.SetPlacement(placement, volume);
            _mixer.AddMixerInput(voice);
            return voice;
        }

        private string CuePath(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.CursorEnter: return Path.Combine(CueDir, "enter.wav");
                case AudioCue.CursorExit: return Path.Combine(CueDir, "exit.wav");
                case AudioCue.CursorImpassable: return Path.Combine(CueDir, "cursor_impassable.wav");
                case AudioCue.CursorFogEnter: return Path.Combine(CueDir, "fog_enter.wav");
                case AudioCue.CursorFogExit: return Path.Combine(CueDir, "fog_exit.wav");
                case AudioCue.ThingNpc: return Path.Combine(ThingDir, "npc.wav");
                case AudioCue.ThingContainer: return Path.Combine(ThingDir, "container.wav");
                case AudioCue.ThingOrb: return Path.Combine(ThingDir, "orb.wav");
                case AudioCue.ThingDoor: return Path.Combine(ThingDir, "door.wav");
                case AudioCue.ThingDoorOpen: return Path.Combine(ThingDir, "door_open.wav");
                default: return Path.Combine(ThingDir, "interactable.wav");
            }
        }

        // Decode a WAV to a mono float[] at the mixer rate, caching only successes. A load failure logs (once
        // per path) and yields an empty buffer (that voice goes silent) rather than crashing the audio thread.
        // The failure is deliberately not cached: a cursor cue loads lazily mid-session, so a transient lock
        // (a Debug redeploy, antivirus) must not silence it for the rest of the session - the next play retries.
        private float[] LoadMono(string path)
        {
            if (_clipCache.TryGetValue(path, out float[] cached)) return cached;
            float[] buf;
            try { buf = DecodeMono(path); }
            catch (Exception e)
            {
                if (_warnedClips.Add(path))
                    _log?.LogWarning("[audio] clip load failed (" + path + "): " + e.Message);
                return Array.Empty<float>();
            }
            _clipCache[path] = buf;
            return buf;
        }

        private static float[] DecodeMono(string path)
        {
            using (var reader = new AudioFileReader(path))
            {
                ISampleProvider sp = reader;
                if (sp.WaveFormat.SampleRate != Rate) sp = new WdlResamplingSampleProvider(sp, Rate);
                int channels = sp.WaveFormat.Channels;

                // Read the whole stream in blocks, growing one buffer with Array.Copy (no per-sample work, so
                // the one-time decode at world entry doesn't hitch the frame).
                var interleaved = new float[Rate * channels];
                int filled = 0;
                var tmp = new float[Rate * channels];
                int n;
                while ((n = sp.Read(tmp, 0, tmp.Length)) > 0)
                {
                    if (filled + n > interleaved.Length)
                        Array.Resize(ref interleaved, Math.Max(interleaved.Length * 2, filled + n));
                    Array.Copy(tmp, 0, interleaved, filled, n);
                    filled += n;
                }

                if (channels == 1)
                {
                    Array.Resize(ref interleaved, filled);
                    return interleaved;
                }
                int frames = filled / channels;
                var mono = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float s = 0f;
                    int b = f * channels;
                    for (int c = 0; c < channels; c++) s += interleaved[b + c];
                    mono[f] = s / channels;
                }
                return mono;
            }
        }

        public IWallTones CreateWallTones() { EnsureStarted(); return new WallTones(this); }

        public void Dispose()
        {
            try { _out?.Stop(); _out?.Dispose(); }
            catch (Exception e) { _log?.LogWarning("[audio] output dispose failed: " + e.Message); }
            _out = null;
            _mixer = null;
        }

        // The one overload guard on the whole soundscape, between the shared mixer and the device: below
        // the knee it is bit-transparent, above it the overshoot folds smoothly into the remaining
        // headroom (asymptote 1.0), so a loud moment (several wall tones plus a ping) rounds off instead
        // of hard-clipping into distortion at the float output. Per-voice clamps would distort each voice
        // alone and still let their SUM clip; one limiter at the output catches everything.
        private sealed class SoftLimiter : ISampleProvider
        {
            private const float Knee = 0.8f;
            private readonly ISampleProvider _source;

            public SoftLimiter(ISampleProvider source)
            {
                _source = source;
                WaveFormat = source.WaveFormat;
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int read = _source.Read(buffer, offset, count);
                for (int i = 0; i < read; i++)
                {
                    float s = buffer[offset + i];
                    float mag = s < 0f ? -s : s;
                    if (mag <= Knee) continue;
                    float soft = Knee + (1f - Knee) * (float)Math.Tanh((mag - Knee) / (1f - Knee));
                    buffer[offset + i] = s < 0f ? -soft : soft;
                }
                return read;
            }
        }

        // A generated sine one-shot with a short attack/release (so it doesn't click) and a constant-power
        // pan. Returns fewer than `count` samples once finished, so the shared mixer auto-removes it.
        private sealed class ToneShot : ISampleProvider
        {
            private readonly int _total, _attack, _release, _rate;
            private readonly float _freq, _gainL, _gainR;
            private int _pos;

            public ToneShot(int rate, float freq, float seconds, float vol, float pan)
            {
                _rate = rate;
                _freq = freq;
                _total = Math.Max(1, (int)(seconds * rate));
                _attack = Math.Min(_total / 2, (int)(0.005f * rate));
                _release = Math.Min(_total / 2, (int)(0.02f * rate));
                PanGains(pan, out float l, out float r);
                _gainL = vol * l;
                _gainR = vol * r;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int frames = count / 2, produced = 0;
                for (int f = 0; f < frames && _pos < _total; f++)
                {
                    float env = 1f;
                    if (_pos < _attack) env = _pos / (float)_attack;
                    else if (_pos > _total - _release) env = (_total - _pos) / (float)_release;
                    float s = (float)Math.Sin(2.0 * Math.PI * _freq * _pos / _rate) * env;
                    buffer[offset + f * 2] = s * _gainL;
                    buffer[offset + f * 2 + 1] = s * _gainR;
                    _pos++;
                    produced += 2;
                }
                return produced;
            }
        }

        // A spatialised, LIVE sampled one-shot, the WOTR PositionalEmitter. The decoded mono clip runs
        // through a high-shelf cut for the front/back cue (transparent ahead/at the side, darkening and
        // quietening behind - a shelf, not a lowpass, so bright narrowband cues stay audible), then splits
        // L/R with a constant-power pan and a fractional interaural delay on the FAR channel, read from a
        // tiny ring of recent SHELVED samples. Crucially the placement is re-settable while it plays:
        // SetPlacement (main thread, via SpatialSources) writes target gains/ITD/shelf and Read (audio
        // thread) ramps the current values toward them across each block, so a source tracks a moving
        // listener without clicks. Goes silent past the clip end, draining the delay tail, then returns
        // fewer than `count` samples so the shared mixer auto-removes it (like ToneShot).
        private sealed class PositionalEmitter : ISampleProvider, ISpatialVoice
        {
            private const int RingSize = 64;         // >= max ITD (~29 frames @ 44.1 kHz) + margin; power of two
            private const int RingMask = RingSize - 1;
            private const int TailFrames = RingSize; // drain the delay line after the clip ends

            private readonly float[] _buf;
            private readonly int _rate;
            private readonly float[] _ring = new float[RingSize];
            // Always in the signal path (identity at 0 dB), so a shelf ramping in never cold-starts.
            private readonly HighShelf _shelf = new HighShelf();

            // Targets - written by SetPlacement (main thread), read by Read (audio thread).
            private volatile float _tGainL, _tGainR, _tItd, _tShelfHz, _tShelfDb;
            // Current smoothed values - audio thread only.
            private float _cGainL, _cGainR, _cItd, _cShelfHz, _cShelfDb;
            private bool _primed;
            private int _frame;
            private volatile bool _finished;

            public PositionalEmitter(int rate, float[] buf)
            {
                _buf = buf;
                _rate = rate;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
            }

            public WaveFormat WaveFormat { get; }
            public bool Finished => _finished;

            public void SetPlacement(SpatialCue cue, float volume)
            {
                PanGains(cue.Pan, out float l, out float r);
                _tGainL = volume * l;
                _tGainR = volume * r;
                float itd = cue.ItdSeconds * _rate;
                float maxItd = RingSize - 2; // the interpolated read needs two ring samples
                _tItd = itd < -maxItd ? -maxItd : (itd > maxItd ? maxItd : itd);
                float hz = cue.RearShelfHz;
                float maxHz = _rate * 0.49f;
                _tShelfHz = hz < 100f ? 100f : (hz > maxHz ? maxHz : hz);
                float db = cue.RearShelfDb;
                _tShelfDb = db < -24f ? -24f : (db > 0f ? 0f : db);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int frames = count / 2;
                if (frames == 0) return 0;

                float tGainL = _tGainL, tGainR = _tGainR, tItd = _tItd;
                float tShelfHz = _tShelfHz, tShelfDb = _tShelfDb;
                if (!_primed)
                {
                    _cGainL = tGainL; _cGainR = tGainR; _cItd = tItd;
                    _cShelfHz = tShelfHz; _cShelfDb = tShelfDb;
                    _shelf.Set(_rate, _cShelfHz, _cShelfDb);
                    _primed = true;
                }

                // The shelf lerps once per block (retuning per sample is too costly; filter state is
                // preserved across retunes, so no click).
                if (Math.Abs(tShelfDb - _cShelfDb) > 0.05f || Math.Abs(tShelfHz - _cShelfHz) > 1f)
                {
                    _cShelfDb += (tShelfDb - _cShelfDb) * 0.5f;
                    _cShelfHz += (tShelfHz - _cShelfHz) * 0.5f;
                    _shelf.Set(_rate, _cShelfHz, _cShelfDb);
                }

                // Gains + ITD ramp linearly to target across the block - click-free moving source.
                float dGainL = (tGainL - _cGainL) / frames;
                float dGainR = (tGainR - _cGainR) / frames;
                float dItd = (tItd - _cItd) / frames;

                int produced = 0;
                int total = _buf.Length + TailFrames;
                for (int f = 0; f < frames; f++)
                {
                    if (_frame >= total) break;
                    _cGainL += dGainL; _cGainR += dGainR; _cItd += dItd;

                    // Shelf the dry clip for the rear cue, then feed the ring the delayed far ear reads
                    // from. The shelf is identity at 0 dB (ahead/beside, the common case), so it stays in
                    // the path and a source moving behind just deepens it - no cold start to mask.
                    float dry = _frame < _buf.Length ? _buf[_frame] : 0f;
                    float m = _shelf.Transform(dry);
                    _ring[_frame & RingMask] = m;

                    float mag = _cItd < 0f ? -_cItd : _cItd;
                    int whole = (int)mag;
                    if (whole > RingSize - 2) whole = RingSize - 2;
                    float frac = mag - whole;
                    int d0 = _frame - whole, d1 = d0 - 1;
                    float s0 = d0 >= 0 ? _ring[d0 & RingMask] : 0f;
                    float s1 = d1 >= 0 ? _ring[d1 & RingMask] : 0f;
                    float far = s0 + (s1 - s0) * frac;

                    bool delayLeft = _cItd >= 0f; // +ve = source east = right ear leads, left lags
                    buffer[offset + produced++] = (delayLeft ? far : m) * _cGainL;
                    buffer[offset + produced++] = (delayLeft ? m : far) * _cGainR;
                    _frame++;
                }
                if (_frame >= total) _finished = true;
                return produced;
            }
        }

        // Four looping mono WAV channels (WOTR's set-1 wall tones) summed to stereo at a fixed compass pan
        // (east hard right, west hard left, north/south centred), added as ONE mixer input. Volumes are set
        // live each frame as targets and smoothed per sample toward them (~10 ms one-pole), so a frame-rate
        // volume step - or the instant mute when control is lost - lands as a short fade, never a click;
        // each channel loops seamlessly so a voice coming back up is click-free.
        private sealed class WallTones : ISampleProvider, IWallTones
        {
            // Per-sample approach fraction for the ~10 ms volume smoothing at the mixer rate.
            private static readonly float VolumeSmoothing = 1f - (float)Math.Exp(-1.0 / (0.010 * Rate));

            private sealed class Channel
            {
                public float[] Buffer = Array.Empty<float>();
                public int Pos;
                public volatile float Volume; // target - written by Update (main thread)
                public float Current;         // smoothed - audio thread only
                public float L = 0.70710677f, R = 0.70710677f;
            }

            private readonly Channel[] _channels;
            private readonly NAudioEngine _engine;

            public WaveFormat WaveFormat { get; }

            public WallTones(NAudioEngine engine)
            {
                _engine = engine;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);
                _channels = new[]
                {
                    Make(engine, "north.wav", 0f),   // N: centred
                    Make(engine, "south.wav", 0f),   // S: centred
                    Make(engine, "east.wav", 1f),    // E: hard right
                    Make(engine, "west.wav", -1f),   // W: hard left
                };
                engine.Add(this);
            }

            private static Channel Make(NAudioEngine engine, string file, float pan)
            {
                PanGains(pan, out float l, out float r);
                return new Channel { Buffer = engine.LoadMono(Path.Combine(engine.WallDir, file)), L = l, R = r };
            }

            public void Update(float[] volumes)
            {
                for (int i = 0; i < _channels.Length && i < volumes.Length; i++)
                {
                    float v = volumes[i];
                    _channels[i].Volume = v < 0f ? 0f : (v > 1f ? 1f : v);
                }
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int frames = count / 2;
                for (int f = 0; f < frames; f++)
                {
                    float l = 0f, r = 0f;
                    for (int i = 0; i < _channels.Length; i++)
                    {
                        Channel c = _channels[i];
                        int len = c.Buffer.Length;
                        if (len == 0) continue;
                        c.Current += (c.Volume - c.Current) * VolumeSmoothing;
                        float s = c.Buffer[c.Pos] * c.Current;
                        c.Pos++;
                        if (c.Pos >= len) c.Pos = 0; // seamless loop
                        l += s * c.L;
                        r += s * c.R;
                    }
                    // No clamp: several loud tones may sum past 1.0, and the engine's output SoftLimiter
                    // rounds the overload off once for the whole mix.
                    buffer[offset + f * 2] = l;
                    buffer[offset + f * 2 + 1] = r;
                }
                return count; // ReadFully mixer: always full (silence when all volumes are 0)
            }

            public void Dispose() => _engine.Remove(this);
        }
    }
}
