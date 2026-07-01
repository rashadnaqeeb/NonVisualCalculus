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
    /// wall tones loop WOTR's set-1 WAV assets, while the one-shot cue is still generated procedurally.
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
        private string _wallDir;
        private string _cueDir;

        public NAudioEngine(ManualLogSource log) { _log = log; }

        // The set-1 wall-tone WAVs deploy beside this assembly under assets/audio/walltones/1.
        private string WallDir => _wallDir ??= Path.Combine(
            Path.GetDirectoryName(typeof(NAudioEngine).Assembly.Location) ?? ".",
            "assets", "audio", "walltones", "1");

        // The cursor enter/exit cue WAVs deploy beside this assembly under assets/audio/cursor.
        private string CueDir => _cueDir ??= Path.Combine(
            Path.GetDirectoryName(typeof(NAudioEngine).Assembly.Location) ?? ".",
            "assets", "audio", "cursor");

        public bool Available => !_failed;

        // 100 ms buffer to ride through managed-thread (GC/CPU) pauses without underrunning into clicks.
        private bool EnsureStarted()
        {
            if (_out != null) return true;
            if (_failed) return false;
            try
            {
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2)) { ReadFully = true };
                _out = new WaveOutEvent { DesiredLatency = 100, NumberOfBuffers = 4 };
                _out.Init(_mixer);
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

        public void PlayCue(AudioCue cue, float volume, float pan)
        {
            if (!EnsureStarted()) return;
            float[] clip = LoadMono(Path.Combine(CueDir, CueFile(cue)));
            if (clip.Length == 0) return; // missing/unreadable asset already logged by LoadMono
            _mixer.AddMixerInput(new ClipShot(Rate, clip, volume, pan));
        }

        private static string CueFile(AudioCue cue) => cue == AudioCue.CursorEnter ? "enter.wav" : "exit.wav";

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

        // A sampled one-shot: a decoded mono clip played once at a constant-power pan. Returns fewer than
        // `count` samples once the clip is exhausted, so the shared mixer auto-removes it (like ToneShot).
        private sealed class ClipShot : ISampleProvider
        {
            private readonly float[] _buf;
            private readonly float _gainL, _gainR;
            private int _pos;

            public ClipShot(int rate, float[] buf, float vol, float pan)
            {
                _buf = buf;
                PanGains(pan, out float l, out float r);
                _gainL = vol * l;
                _gainR = vol * r;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int frames = count / 2, produced = 0;
                for (int f = 0; f < frames && _pos < _buf.Length; f++)
                {
                    float s = _buf[_pos++];
                    buffer[offset + f * 2] = s * _gainL;
                    buffer[offset + f * 2 + 1] = s * _gainR;
                    produced += 2;
                }
                return produced;
            }
        }

        // Four looping mono WAV channels (WOTR's set-1 wall tones) summed to stereo at a fixed compass pan
        // (east hard right, west hard left, north/south centred), added as ONE mixer input. Volumes are set
        // live each frame; each channel loops seamlessly so a voice coming back up is click-free.
        private sealed class WallTones : ISampleProvider, IWallTones
        {
            private sealed class Channel
            {
                public float[] Buffer = Array.Empty<float>();
                public int Pos;
                public volatile float Volume;
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
                        float s = c.Buffer[c.Pos] * c.Volume;
                        c.Pos++;
                        if (c.Pos >= len) c.Pos = 0; // seamless loop
                        l += s * c.L;
                        r += s * c.R;
                    }
                    buffer[offset + f * 2] = l > 1f ? 1f : (l < -1f ? -1f : l);
                    buffer[offset + f * 2 + 1] = r > 1f ? 1f : (r < -1f ? -1f : r);
                }
                return count; // ReadFully mixer: always full (silence when all volumes are 0)
            }

            public void Dispose() => _engine.Remove(this);
        }
    }
}
