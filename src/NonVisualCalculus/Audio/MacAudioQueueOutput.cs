using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace NonVisualCalculus.Audio
{
    /// <summary>
    /// The macOS output device for <see cref="NAudioEngine"/>'s mixer, standing in for NAudio's
    /// WaveOutEvent (winmm, Windows only). An AudioQueue on the default output device pulls 32-bit float
    /// samples from the provider on the queue's own thread, the way WaveOutEvent's thread does: a few
    /// short buffers cycle through the queue, each refilled from the provider as it comes back.
    /// </summary>
    internal sealed unsafe class MacAudioQueueOutput : IWavePlayer
    {
        private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
        private const uint FormatLinearPcm = 0x6C70636D; // 'lpcm'
        private const uint FormatFlagIsFloat = 1;
        private const uint FormatFlagIsPacked = 8;

        private readonly int _bufferCount;
        private readonly int _framesPerBuffer;
        private ISampleProvider _source;
        private IntPtr _queue;
        private GCHandle _self;
        private float[] _scratch;
        private volatile bool _stopping;

        public MacAudioQueueOutput(int desiredLatencyMs, int bufferCount)
        {
            _bufferCount = bufferCount;
            _framesPerBuffer = Math.Max(256, desiredLatencyMs * NAudioEngine.Rate / 1000 / bufferCount);
        }

        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public float Volume { get; set; } = 1f;

        public WaveFormat OutputWaveFormat => _source?.WaveFormat;

        public void Init(IWaveProvider waveProvider) => Init(waveProvider.ToSampleProvider());

        public void Init(ISampleProvider source)
        {
            var format = source.WaveFormat;
            if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
                throw new ArgumentException("MacAudioQueueOutput plays 32-bit float only, got " + format);

            _source = source;
            int channels = format.Channels;
            _scratch = new float[_framesPerBuffer * channels];

            var description = new AudioStreamBasicDescription
            {
                SampleRate = format.SampleRate,
                FormatId = FormatLinearPcm,
                FormatFlags = FormatFlagIsFloat | FormatFlagIsPacked,
                BytesPerPacket = (uint)(4 * channels),
                FramesPerPacket = 1,
                BytesPerFrame = (uint)(4 * channels),
                ChannelsPerFrame = (uint)channels,
                BitsPerChannel = 32,
            };

            _self = GCHandle.Alloc(this);
            IntPtr queue;
            Check(AudioQueueNewOutput(&description, &OnBufferDone, GCHandle.ToIntPtr(_self),
                IntPtr.Zero, IntPtr.Zero, 0, &queue), "AudioQueueNewOutput");
            _queue = queue;

            uint bytes = (uint)(_framesPerBuffer * channels * 4);
            for (int i = 0; i < _bufferCount; i++)
            {
                AudioQueueBuffer* buffer;
                Check(AudioQueueAllocateBuffer(_queue, bytes, &buffer), "AudioQueueAllocateBuffer");
                Fill(buffer);
                Check(AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero), "AudioQueueEnqueueBuffer");
            }
        }

        public void Play()
        {
            if (_queue == IntPtr.Zero)
                throw new InvalidOperationException("Init must come before Play");
            Check(AudioQueueStart(_queue, IntPtr.Zero), "AudioQueueStart");
            PlaybackState = PlaybackState.Playing;
        }

        public void Pause()
        {
            if (_queue == IntPtr.Zero)
                return;
            Check(AudioQueuePause(_queue), "AudioQueuePause");
            PlaybackState = PlaybackState.Paused;
        }

        public void Stop()
        {
            if (_queue == IntPtr.Zero || PlaybackState == PlaybackState.Stopped)
                return;
            _stopping = true;
            Check(AudioQueueStop(_queue, 1), "AudioQueueStop");
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }

        public void Dispose()
        {
            _stopping = true;
            if (_queue != IntPtr.Zero)
            {
                // Disposing immediately also releases the buffers; no callback runs after it returns.
                AudioQueueDispose(_queue, 1);
                _queue = IntPtr.Zero;
            }

            if (_self.IsAllocated)
                _self.Free();
            PlaybackState = PlaybackState.Stopped;
        }

        private void Fill(AudioQueueBuffer* buffer)
        {
            int wanted = _scratch.Length;
            int read = 0;
            if (!_stopping && PlaybackState != PlaybackState.Paused)
                read = _source.Read(_scratch, 0, wanted);
            float volume = Volume;
            float* destination = (float*)buffer->AudioData;
            for (int i = 0; i < read; i++)
                destination[i] = _scratch[i] * volume;
            for (int i = read; i < wanted; i++)
                destination[i] = 0f;
            buffer->AudioDataByteSize = (uint)(wanted * 4);
        }

        // Runs on the AudioQueue's thread each time a buffer has played.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void OnBufferDone(IntPtr userData, IntPtr queue, AudioQueueBuffer* buffer)
        {
            MacAudioQueueOutput self = null;
            try
            {
                self = (MacAudioQueueOutput)GCHandle.FromIntPtr(userData).Target;
                if (self == null || self._stopping)
                    return;
                self.Fill(buffer);
                int status = AudioQueueEnqueueBuffer(queue, buffer, 0, IntPtr.Zero);
                if (status != 0)
                    throw new InvalidOperationException("AudioQueueEnqueueBuffer failed: " + status);
            }
            catch (Exception ex)
            {
                // An exception must not cross into the native callback; report it as a stopped device.
                if (self != null)
                {
                    self._stopping = true;
                    self.PlaybackStopped?.Invoke(self, new StoppedEventArgs(ex));
                }
                else
                {
                    Plugin.Logger?.LogWarning("[audio] AudioQueue callback failed: " + ex);
                }
            }
        }

        private static void Check(int status, string call)
        {
            if (status != 0)
                throw new InvalidOperationException(call + " failed with OSStatus " + status);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioStreamBasicDescription
        {
            public double SampleRate;
            public uint FormatId;
            public uint FormatFlags;
            public uint BytesPerPacket;
            public uint FramesPerPacket;
            public uint BytesPerFrame;
            public uint ChannelsPerFrame;
            public uint BitsPerChannel;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioQueueBuffer
        {
            public uint AudioDataBytesCapacity;
            public IntPtr AudioData;
            public uint AudioDataByteSize;
            public IntPtr UserData;
            public uint PacketDescriptionCapacity;
            public IntPtr PacketDescriptions;
            public uint PacketDescriptionCount;
        }

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueNewOutput(AudioStreamBasicDescription* format,
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, AudioQueueBuffer*, void> callback, IntPtr userData,
            IntPtr runLoop, IntPtr runLoopMode, uint flags, IntPtr* queue);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, AudioQueueBuffer** buffer);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueEnqueueBuffer(IntPtr queue, AudioQueueBuffer* buffer, uint packetCount, IntPtr packets);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueuePause(IntPtr queue);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueStop(IntPtr queue, byte immediate);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueDispose(IntPtr queue, byte immediate);
    }
}
