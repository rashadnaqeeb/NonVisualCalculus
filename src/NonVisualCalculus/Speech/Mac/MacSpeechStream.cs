using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace NonVisualCalculus.Speech.Mac
{
    /// <summary>
    /// A speech queue with no gaps, for the system voice on macOS.
    ///
    /// AVSpeechSynthesizer's own queue starts and stops the audio output around every utterance,
    /// which costs about 120 ms of silence per line for the compact voices and about 250 ms for
    /// Eloquence (measured August 2026 in the Say the Spire 2 port: four short lines took 1612 ms
    /// to play for 602 ms of speech); a screen reader user hears speech stopping and restarting on
    /// every line, and nothing on the utterance API changes it. So each message is rendered
    /// instead, with writeUtterance:toBufferCallback:, which delivers the same audio the voice
    /// would play in a fraction of its playing time with no silence in front and a few tens of
    /// milliseconds behind. The samples are trimmed (<see cref="SpeechAudio.Trim"/>) and scheduled
    /// back-to-back on an AVAudioPlayerNode of our own, in the voice's native format (Eloquence
    /// renders at 16 kHz, the compact voices at 22.05 kHz; the engine's mixer resamples to the
    /// output device). Our own AVAudioEngine, rather than the game's audio, keeps speech clear of
    /// the game's bus and gives it a volume of its own.
    ///
    /// One render runs at a time. AVSpeech delivers the buffers on a thread of its own through an
    /// Objective-C block built by hand here; the block writes into the <see cref="Render"/> in
    /// flight, and <see cref="Update"/> collects the result once per frame and starts the next. A
    /// render cannot be aborted (stopSpeakingAtBoundary: does not touch a write), so when one must
    /// be abandoned, on an interrupt or because it stalled, its synthesizer and block are retired
    /// with it and a fresh synthesizer takes the next message at once; a retired set is reaped, its
    /// block reused, only once no more callbacks can come - AVSpeech's nil end marker has arrived,
    /// or the render has been quiet for the stall timeout - so nothing the callback thread may
    /// still touch is freed under it. A render whose AUDIO is complete (a zero-frame buffer also
    /// marks that) but whose nil marker has not yet arrived is scheduled at once and its
    /// synthesizer retired the same way, never reused: a trailing callback into a reused block
    /// would land on the NEXT render's handle and end it with someone else's marker. Stopping the
    /// player node discards its scheduled buffers, which is the interrupt. Everything except the
    /// block callback happens on the main thread. Only constructed on macOS.
    /// </summary>
    public sealed class MacSpeechStream
    {
        private const double InitialRate = 22050; // the player's format until the first render says otherwise
        private const float GapSeconds = 0.05f; // silence between utterances, as a screen reader would pause
        private const double StallTimeoutSeconds = 5; // a render that delivers nothing for this long is abandoned

        // Resolved when the type is first touched, which MacSystemVoice.Start guarantees happens
        // after ObjC.LoadSpeechFrameworks.
        private static readonly IntPtr ClassPcmBuffer = ObjC.Class("AVAudioPCMBuffer");
        private static readonly IntPtr SelFloatChannelData = ObjC.Sel("floatChannelData");
        private static readonly IntPtr SelFrameLength = ObjC.Sel("frameLength");
        private static readonly IntPtr SelSetFrameLength = ObjC.Sel("setFrameLength:");
        private static readonly IntPtr SelScheduleBuffer = ObjC.Sel("scheduleBuffer:completionHandler:");
        private static readonly IntPtr SelFormat = ObjC.Sel("format");
        private static readonly IntPtr SelSampleRate = ObjC.Sel("sampleRate");
        private static readonly IntPtr SelWrite = ObjC.Sel("writeUtterance:toBufferCallback:");
        private static readonly IntPtr SelUtteranceWithString = ObjC.Sel("speechUtteranceWithString:");
        private static readonly IntPtr SelSetVoice = ObjC.Sel("setVoice:");
        private static readonly IntPtr SelSetRate = ObjC.Sel("setRate:");
        private static readonly IntPtr SelVoiceWithIdentifier = ObjC.Sel("voiceWithIdentifier:");
        private static readonly IntPtr SelStop = ObjC.Sel("stop");
        private static readonly IntPtr SelPlay = ObjC.Sel("play");
        private static readonly IntPtr SelIsRunning = ObjC.Sel("isRunning");
        private static readonly IntPtr SelStartAndReturnError = ObjC.Sel("startAndReturnError:");
        private static readonly IntPtr SelAlloc = ObjC.Sel("alloc");
        private static readonly IntPtr SelInitBuffer = ObjC.Sel("initWithPCMFormat:frameCapacity:");
        private static readonly IntPtr SelIsKindOfClass = ObjC.Sel("isKindOfClass:");
        private static readonly IntPtr ClassUtterance = ObjC.Class("AVSpeechUtterance");
        private static readonly IntPtr ClassVoice = ObjC.Class("AVSpeechSynthesisVoice");

        private IntPtr _engine; // owned (+1)
        private IntPtr _player; // owned (+1)
        private IntPtr _format; // owned (+1): float32, mono, at _formatRate; the player's connection to the mixer
        private double _formatRate;
        private float _volume = 1f; // gain on the rendered samples; 1 is the voice's own loudness

        private IntPtr _synth; // owned (+1): the AVSpeechSynthesizer rendering now, or Zero until the next render needs one; never speaks aloud
        private IntPtr _block; // the callback block for _synth, or Zero; see MakeBlock
        private Render _inFlight;
        private readonly List<Retired> _retired = new List<Retired>();
        private readonly Stack<IntPtr> _freeBlocks = new Stack<IntPtr>();
        private bool _broken; // a renderer could not be created; see Enqueue

        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private IntPtr _voice; // owned (+1): the AVSpeechSynthesisVoice for _resolvedVoice, or Zero for AVSpeech's default
        private string _resolvedVoice; // the identifier _voice was resolved from

        private struct Pending
        {
            public string Text;
            public string Voice;
            public float Rate;
        }

        private sealed class Retired
        {
            public IntPtr Synth;
            public IntPtr Block;
            public Render Render;
        }

        public MacSpeechStream()
        {
            try
            {
                _engine = ObjC.Alloc("AVAudioEngine", "init");
                _player = ObjC.Alloc("AVAudioPlayerNode", "init");
                ObjC.Send(_engine, ObjC.Sel("attachNode:"), _player);
                Connect(InitialRate);
                if (!ObjC.SendBool(_engine, SelStartAndReturnError, IntPtr.Zero))
                {
                    throw new InvalidOperationException("AVAudioEngine failed to start");
                }

                EnsureRenderer();
            }
            catch
            {
                // Rethrown for MacSystemVoice, which logs it and falls back to AVSpeech's own queue.
                Dispose();
                throw;
            }
        }

        /// <summary>Playback volume for the lines rendered from here on: 1 is the voice's own
        /// loudness, above it the samples are amplified (<see cref="SpeechAudio.Scale"/>). The
        /// samples are scaled rather than the player node, whose volume stops at 1.</summary>
        public void SetVolume(float volume)
        {
            _volume = SpeechAudio.Clamp(volume, 0f, MacSystemVoice.MaxVolume);
        }

        /// <summary>
        /// Queue a message in <paramref name="voiceIdentifier"/> (null for AVSpeech's default) at
        /// <paramref name="rate"/> on AVSpeech's [0, 1] scale. False if the stream can no longer
        /// render, in which case the caller should drop it and speak another way.
        /// </summary>
        public bool Enqueue(string text, string voiceIdentifier, float rate)
        {
            if (_broken)
            {
                return false;
            }

            if (text != null && text.Trim().Length > 0)
            {
                Pending item;
                item.Text = text;
                item.Voice = voiceIdentifier;
                item.Rate = SpeechAudio.Clamp(rate, 0f, 1f);
                _pending.Enqueue(item);
                StartNext();
            }

            return !_broken;
        }

        /// <summary>Drop everything queued, abandon the render in flight, and stop what is playing.</summary>
        public void Clear()
        {
            _pending.Clear();
            if (_inFlight != null)
            {
                Retire();
            }

            ObjC.Send(_player, SelStop);
        }

        /// <summary>Once per frame: reap retired renders, collect a finished one, and start the next.</summary>
        public void Update()
        {
            Reap();
            string problem = Interlocked.Exchange(ref _callbackProblem, null);
            if (problem != null)
            {
                Log.Error("speech: the render callback failed: " + problem);
            }

            Render render = _inFlight;
            if (render != null)
            {
                if (render.Done)
                {
                    Finish(render);
                }
                else if (render.SecondsSinceProgress > StallTimeoutSeconds)
                {
                    Log.Error(
                        "speech: a render stalled"
                            + (render.Problem == null ? "" : " (" + render.Problem + ")")
                            + "; abandoning it"
                    );
                    Retire();
                }
            }

            StartNext();
        }

        public void Dispose()
        {
            Clear();
            try
            {
                if (_engine != IntPtr.Zero)
                {
                    ObjC.Send(_engine, SelStop);
                }
            }
            catch (Exception e)
            {
                Log.Info("speech: AVAudioEngine stop failed: " + e.Message);
            }

            ObjC.Release(_player);
            ObjC.Release(_engine);
            ObjC.Release(_format);
            ObjC.Release(_voice);
            ObjC.Release(_synth);
            _player = _engine = _format = _voice = _synth = IntPtr.Zero;
            _block = IntPtr.Zero;
            // Renders still in flight keep their synthesizer, block and handle: the callback
            // thread may be inside them. A few hundred bytes each, only on unload while something
            // was being rendered. Blocks are never freed at all: the runtime reads a global
            // block's flags on release, so a freed one could be read after its owner is gone.
            Reap();
            if (_retired.Count > 0)
            {
                Log.Info("speech: " + _retired.Count + " render(s) still in flight at dispose; left to finish on their own");
            }

            _retired.Clear();
        }

        // ---- rendering ----

        /// <summary>A synthesizer and block for the next render, made on demand; false, and the
        /// stream broken, if they cannot be.</summary>
        private bool EnsureRenderer()
        {
            if (_synth != IntPtr.Zero)
            {
                return true;
            }

            try
            {
                _synth = ObjC.Alloc("AVSpeechSynthesizer", "init");
                _block = _freeBlocks.Count > 0 ? _freeBlocks.Pop() : MakeBlock();
                return true;
            }
            catch (Exception e)
            {
                Log.Error("speech: no renderer; the stream is unusable from here on: " + e.Message);
                ObjC.Release(_synth);
                _synth = IntPtr.Zero;
                _broken = true;
                return false;
            }
        }

        private void StartNext()
        {
            if (_inFlight != null || _pending.Count == 0 || !EnsureRenderer())
            {
                return;
            }

            Pending next = _pending.Dequeue();
            IntPtr utterance = MakeUtterance(next.Text, ResolveVoice(next.Voice), next.Rate);
            if (utterance == IntPtr.Zero)
            {
                Log.Error("speech: AVSpeechUtterance returned nil; dropping the message");
                return;
            }

            Render render = new Render();
            Marshal.WriteIntPtr(_block, BlockHandleOffset, GCHandle.ToIntPtr(render.Handle));
            _inFlight = render;
            ObjC.Send(_synth, SelWrite, utterance, _block);
        }

        /// <summary>An autoreleased AVSpeechUtterance for <paramref name="text"/> on
        /// <paramref name="voice"/> (Zero for AVSpeech's default) at <paramref name="rate"/>, or Zero.</summary>
        public static IntPtr MakeUtterance(string text, IntPtr voice, float rate)
        {
            IntPtr utterance = ObjC.Send(
                ClassUtterance,
                SelUtteranceWithString,
                ObjC.NSString(text)
            );
            if (utterance == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (voice != IntPtr.Zero)
            {
                ObjC.Send(utterance, SelSetVoice, voice);
            }

            ObjC.SendFloat(utterance, SelSetRate, rate);
            return utterance;
        }

        /// <summary>The autoreleased AVSpeechSynthesisVoice for an identifier, or Zero if AVSpeech
        /// has no such voice.</summary>
        public static IntPtr LookupVoice(string identifier)
        {
            return ObjC.Send(ClassVoice, SelVoiceWithIdentifier, ObjC.NSString(identifier));
        }

        /// <summary>The voice object for an identifier, retained and cached for the identifier
        /// last used; Zero for null or for a voice AVSpeech does not have.</summary>
        private IntPtr ResolveVoice(string identifier)
        {
            if (identifier == _resolvedVoice)
            {
                return _voice;
            }

            ObjC.Release(_voice);
            _voice = IntPtr.Zero;
            _resolvedVoice = identifier;
            if (identifier == null)
            {
                return IntPtr.Zero;
            }

            _voice = ObjC.Retain(LookupVoice(identifier));
            if (_voice == IntPtr.Zero)
            {
                Log.Info("speech: AVSpeech has no voice " + identifier + "; its default stands in");
            }

            return _voice;
        }

        /// <summary>Schedule the finished render's speech, and free its block for reuse only if
        /// its nil end marker has arrived; a render finished by a zero-frame buffer alone retires
        /// its synthesizer and block instead, still wearing its handle, so the trailing marker
        /// lands on it and not on the next render.</summary>
        private void Finish(Render render)
        {
            _inFlight = null;
            if (render.Ended)
            {
                Marshal.WriteIntPtr(_block, BlockHandleOffset, IntPtr.Zero);
                render.Handle.Free();
            }
            else
            {
                Retired retired = new Retired();
                retired.Synth = _synth;
                retired.Block = _block;
                retired.Render = render;
                _retired.Add(retired);
                _synth = IntPtr.Zero;
                _block = IntPtr.Zero;
            }

            if (render.Problem != null)
            {
                Log.Error("speech: rendered speech unusable: " + render.Problem);
                return;
            }

            try
            {
                Schedule(
                    SpeechAudio.Trim(render.Samples, render.Count, render.SampleRate),
                    render.SampleRate
                );
            }
            catch (Exception e)
            {
                Log.Error("speech: could not play rendered speech: " + e.Message);
            }
        }

        /// <summary>Abandon the render in flight together with its synthesizer and block; the
        /// next render gets new ones.</summary>
        private void Retire()
        {
            Render render = _inFlight;
            if (render == null)
            {
                return;
            }

            Retired retired = new Retired();
            retired.Synth = _synth;
            retired.Block = _block;
            retired.Render = render;
            _retired.Add(retired);
            _inFlight = null;
            _synth = IntPtr.Zero;
            _block = IntPtr.Zero;
        }

        /// <summary>Release retired synthesizers whose render can get no more callbacks - its nil
        /// end marker arrived, or it has been quiet past the stall timeout - and put their blocks
        /// up for reuse.</summary>
        private void Reap()
        {
            for (int i = _retired.Count - 1; i >= 0; i--)
            {
                Retired retired = _retired[i];
                if (
                    !retired.Render.Ended
                    && retired.Render.SecondsSinceProgress <= StallTimeoutSeconds
                )
                {
                    continue;
                }

                Marshal.WriteIntPtr(retired.Block, BlockHandleOffset, IntPtr.Zero);
                retired.Render.Handle.Free();
                ObjC.Release(retired.Synth);
                _freeBlocks.Push(retired.Block);
                _retired.RemoveAt(i);
            }
        }

        // ---- playback ----

        /// <summary>Connect the player to the mixer in float32 mono at <paramref name="sampleRate"/>,
        /// replacing any earlier connection.</summary>
        private void Connect(double sampleRate)
        {
            IntPtr format = ObjC.SendDoubleUInt(
                ObjC.Send(ObjC.Class("AVAudioFormat"), ObjC.Sel("alloc")),
                ObjC.Sel("initStandardFormatWithSampleRate:channels:"),
                sampleRate,
                1
            );
            if (format == IntPtr.Zero)
            {
                throw new InvalidOperationException("AVAudioFormat init returned nil for " + sampleRate + " Hz");
            }

            if (_format != IntPtr.Zero)
            {
                ObjC.Send(_engine, ObjC.Sel("disconnectNodeOutput:"), _player);
            }

            ObjC.Release(_format);
            _format = format;
            _formatRate = sampleRate;
            ObjC.Send(
                _engine,
                ObjC.Sel("connect:to:format:"),
                _player,
                ObjC.Send(_engine, ObjC.Sel("mainMixerNode")),
                _format
            );
        }

        /// <summary>Copy the samples plus the gap into an AVAudioPCMBuffer and queue it on the
        /// player node, which plays it after whatever it already holds.</summary>
        private void Schedule(float[] trimmed, double sampleRate)
        {
            if (trimmed.Length == 0)
            {
                return; // nothing to say
            }

            // The engine stops on its own after an output device change; bring it back first.
            // Scheduling on a node whose engine is not running raises an Objective-C exception,
            // which managed code cannot catch.
            if (
                !ObjC.SendBool(_engine, SelIsRunning)
                && !ObjC.SendBool(_engine, SelStartAndReturnError, IntPtr.Zero)
            )
            {
                Log.Error("speech: AVAudioEngine failed to restart; dropping the message");
                return;
            }

            // A voice with another native rate: the player's buffers must match its connection,
            // so reconnect. Whatever the old voice still had queued is dropped; a voice change
            // comes from the OS settings, between sessions.
            if (sampleRate != _formatRate)
            {
                ObjC.Send(_player, SelStop);
                Connect(sampleRate);
            }

            int frames = trimmed.Length + (int)(GapSeconds * sampleRate);
            IntPtr buffer = ObjC.Send(
                ObjC.Send(ClassPcmBuffer, SelAlloc),
                SelInitBuffer,
                _format,
                new IntPtr(frames)
            );
            if (buffer == IntPtr.Zero)
            {
                Log.Error("speech: AVAudioPCMBuffer init returned nil");
                return;
            }

            IntPtr channels = ObjC.Send(buffer, SelFloatChannelData);
            if (channels == IntPtr.Zero)
            {
                Log.Error("speech: AVAudioPCMBuffer has no float channel data");
                ObjC.Release(buffer);
                return;
            }

            IntPtr channel = Marshal.ReadIntPtr(channels);
            SpeechAudio.Scale(trimmed, _volume);
            Marshal.Copy(trimmed, 0, channel, trimmed.Length);
            int gap = frames - trimmed.Length;
            Marshal.Copy(
                new float[gap],
                0,
                new IntPtr(channel.ToInt64() + (long)trimmed.Length * sizeof(float)),
                gap
            );
            ObjC.Send(buffer, SelSetFrameLength, new IntPtr(frames));

            ObjC.Send(_player, SelScheduleBuffer, buffer, IntPtr.Zero);
            ObjC.Release(buffer); // the node holds its own reference until played
            ObjC.Send(_player, SelPlay); // idempotent while playing; needed after a stop or an engine restart
        }

        // ---- the callback block ----

        // An Objective-C block is a struct the runtime and the callee both read: isa, flags,
        // reserved, the invoke function, a descriptor, then any captured variables. This one is a
        // global block (never copied, never freed by the runtime) whose single captured variable
        // is a GCHandle to the Render it is serving, rewritten between renders. Layout per the
        // Clang Block ABI; the signature says "void, taking the block and an object".
        private const int BlockSize = 40;
        private const int BlockHandleOffset = 32;
        private const int BlockIsGlobal = 1 << 28;
        private const int BlockHasSignature = 1 << 30;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BufferCallback(IntPtr block, IntPtr buffer);

        // Kept alive for the process: the function pointer below must stay valid.
        private static readonly BufferCallback OnBufferDelegate = OnBuffer;
        private static readonly IntPtr OnBufferPointer = Marshal.GetFunctionPointerForDelegate(OnBufferDelegate);
        private static readonly IntPtr Descriptor = MakeDescriptor();
        private static readonly IntPtr GlobalBlockClass = ObjC.Symbol("_NSConcreteGlobalBlock");
        private static string _callbackProblem; // set on AVSpeech's thread when a callback fails outside any render; logged by Update

        private static IntPtr MakeDescriptor()
        {
            IntPtr descriptor = Marshal.AllocHGlobal(24);
            Marshal.WriteIntPtr(descriptor, 0, IntPtr.Zero); // reserved
            Marshal.WriteIntPtr(descriptor, 8, new IntPtr(BlockSize)); // size of the block literal
            Marshal.WriteIntPtr(descriptor, 16, Marshal.StringToHGlobalAnsi("v16@?0@8")); // signature
            return descriptor;
        }

        private static IntPtr MakeBlock()
        {
            if (GlobalBlockClass == IntPtr.Zero)
            {
                throw new InvalidOperationException("_NSConcreteGlobalBlock not found");
            }

            IntPtr block = Marshal.AllocHGlobal(BlockSize);
            Marshal.WriteIntPtr(block, 0, GlobalBlockClass);
            Marshal.WriteInt32(block, 8, BlockIsGlobal | BlockHasSignature);
            Marshal.WriteInt32(block, 12, 0);
            Marshal.WriteIntPtr(block, 16, OnBufferPointer);
            Marshal.WriteIntPtr(block, 24, Descriptor);
            Marshal.WriteIntPtr(block, BlockHandleOffset, IntPtr.Zero);
            return block;
        }

        /// <summary>AVSpeech's thread hands over one AVAudioBuffer per call, then an empty one at
        /// the end. Nothing may throw out of here.</summary>
        private static void OnBuffer(IntPtr block, IntPtr buffer)
        {
            IntPtr pool = ObjC.AutoreleasePoolPush();
            Render render = null;
            try
            {
                IntPtr handle = Marshal.ReadIntPtr(block, BlockHandleOffset);
                if (handle == IntPtr.Zero)
                {
                    return; // a buffer for a render already collected
                }

                render = GCHandle.FromIntPtr(handle).Target as Render;
                if (render != null)
                {
                    render.Append(buffer);
                }
            }
            catch (Exception e)
            {
                if (render != null)
                {
                    render.Fail(e.Message);
                }
                else
                {
                    Interlocked.Exchange(ref _callbackProblem, e.Message);
                }
            }
            finally
            {
                ObjC.AutoreleasePoolPop(pool);
            }
        }

        /// <summary>One utterance being rendered: the samples AVSpeech's thread appends, read on
        /// the main thread once <see cref="Done"/>.</summary>
        private sealed class Render
        {
            private readonly object _lock = new object();
            private float[] _samples = new float[4096];
            private int _count;
            private volatile bool _done;
            private volatile bool _ended;
            private string _problem;
            private long _lastProgress = Stopwatch.GetTimestamp();

            public GCHandle Handle;
            public double SampleRate;

            /// <summary>The audio is complete: a zero-frame buffer or the nil marker arrived.</summary>
            public bool Done
            {
                get { return _done; }
            }

            /// <summary>The nil end marker arrived: AVSpeech promises no further callbacks.</summary>
            public bool Ended
            {
                get { return _ended; }
            }

            public string Problem
            {
                get
                {
                    lock (_lock)
                    {
                        return _problem;
                    }
                }
            }

            public double SecondsSinceProgress
            {
                get
                {
                    return (Stopwatch.GetTimestamp() - Thread.VolatileRead(ref _lastProgress))
                        / (double)Stopwatch.Frequency;
                }
            }

            /// <summary>The samples so far, and how many; only read once <see cref="Done"/>, after
            /// which the callback thread writes no more.</summary>
            public float[] Samples
            {
                get { return _samples; }
            }

            public int Count
            {
                get { return _count; }
            }

            public Render()
            {
                Handle = GCHandle.Alloc(this);
            }

            public void Append(IntPtr buffer)
            {
                Thread.VolatileWrite(ref _lastProgress, Stopwatch.GetTimestamp());
                if (buffer == IntPtr.Zero)
                {
                    // Not documented, but the only sensible meaning of a nil buffer: the render is
                    // over, nothing more is coming.
                    _done = true;
                    _ended = true;
                    return;
                }

                if (_done)
                {
                    // The audio was already closed by a zero-frame buffer; the main thread may be
                    // reading the samples, so nothing after that marker is written.
                    return;
                }

                if (!ObjC.SendBool(buffer, SelIsKindOfClass, ClassPcmBuffer))
                {
                    Fail("AVSpeech delivered a buffer that is not PCM");
                    return;
                }

                int frames = (int)(ObjC.Send(buffer, SelFrameLength).ToInt64() & 0xFFFFFFFF); // AVAudioFrameCount, 32-bit
                if (frames == 0)
                {
                    _done = true;
                    return;
                }

                double rate = ObjC.SendReturningDouble(ObjC.Send(buffer, SelFormat), SelSampleRate);
                IntPtr channels = ObjC.Send(buffer, SelFloatChannelData);
                if (channels == IntPtr.Zero)
                {
                    Fail("AVSpeech delivered a buffer without float channel data");
                    return;
                }

                lock (_lock)
                {
                    if (SampleRate <= 0)
                    {
                        SampleRate = rate;
                    }
                    else if (rate != SampleRate)
                    {
                        _problem = "sample rate changed from " + SampleRate + " to " + rate + " within one utterance";
                        return;
                    }

                    if (_count + frames > _samples.Length)
                    {
                        Array.Resize(ref _samples, Math.Max(_samples.Length * 2, _count + frames));
                    }

                    Marshal.Copy(Marshal.ReadIntPtr(channels), _samples, _count, frames); // channel 0; speech voices are mono
                    _count += frames;
                }
            }

            /// <summary>Record what went wrong; the render still waits for AVSpeech's end marker so
            /// its block is not reused early.</summary>
            public void Fail(string problem)
            {
                lock (_lock)
                {
                    if (_problem == null)
                    {
                        _problem = problem;
                    }
                }
            }
        }
    }
}
