using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NonVisualCalculus.Speech
{
    /// <summary>
    /// Thin P/Invoke layer over prism.dll (https://github.com/ethindp/prism) - a unified native
    /// abstraction over screen readers / TTS (NVDA, JAWS, SAPI, OneCore, ...). Adapted from the
    /// WrathAccess binding: hand-written P/Invoke against the native dll (framework-agnostic, matches
    /// our net6 / IL2CPP host). prism.dll sits next to disco.exe and talks to the screen reader itself.
    /// On macOS the same binding loads libprism.dylib from the plugin folder; the registry builder and
    /// enumeration calls below are the 0.18 API the Mac library has and only the Mac path calls.
    /// </summary>
    internal static class PrismNative
    {
        private const string Dll = "prism";

        // Must match PrismError in prism.h (third_party/prism/include/prism.h) for the running version.
        public enum PrismError : int
        {
            Ok = 0,
            NotInitialized,
            InvalidParam,
            NotImplemented,
            NoVoices,
            VoiceNotFound,
            SpeakFailure,
            MemoryFailure,
            RangeOutOfBounds,
            Internal,
            NotSpeaking,
            NotPaused,
            AlreadyPaused,
            InvalidUtf8,
            InvalidOperation,
            AlreadyInitialized,
            BackendNotAvailable,
            Unknown,
            InvalidAudioFormat,
            InternalBackendLimitExceeded,
            BackendEnteredUndefinedState,
            // 0.18 additions (the registry builder's library loading); only the macOS library reports them.
            LibraryLoadFailed,
            LibraryInvalid,
            IncompatibleAbi,
            Count,
        }

        [Flags]
        public enum BackendFeatures : ulong
        {
            SupportsSpeak = 1UL << 2,
            SupportsOutput = 1UL << 5,
            SupportsStop = 1UL << 7,
        }

        /// <summary>
        /// PrismConfig as of PRISM_CONFIG_VERSION 3 (prism 0.18). Only Version and Registry are set:
        /// prism_config_init's defaults are zero for every other field, and a null availability callback
        /// leaves the polling fields inert. A null Registry means the library's global one.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PrismConfig
        {
            public byte Version;
            public IntPtr Registry;
            public IntPtr AvailabilityCallback;
            public IntPtr AvailabilityUserdata;
            public uint AvailabilityPollIntervalMs;
            public uint AvailabilityDebounceSamples;
            public uint AvailabilityBackoffMaxMs;
            [MarshalAs(UnmanagedType.I1)]
            public bool AvailabilityAutoPowerManage;
        }

        public const byte ConfigVersion = 3;

        [DllImport(Dll, EntryPoint = "prism_init")]
        public static extern IntPtr Init(IntPtr config);

        [DllImport(Dll, EntryPoint = "prism_init")]
        public static extern IntPtr InitWithConfig(ref PrismConfig config);

        // A registry built by the application starts seeded with the compiled-in backends; a library
        // added to it joins them. Freezing spends the builder, which must still be freed; a context
        // bound to the registry holds its own reference, so ours may be released after prism_init.
        [DllImport(Dll, EntryPoint = "prism_registry_builder_new")]
        public static extern IntPtr RegistryBuilderNew();

        // priorityOverride negative keeps the plugin's own priority. On any error nothing is added.
        [DllImport(Dll, EntryPoint = "prism_registry_builder_add_library")]
        private static extern PrismError RegistryBuilderAddLibraryRaw(IntPtr builder, byte[] utf8Path, int priorityOverride, out UIntPtr outCount);

        public static PrismError RegistryBuilderAddLibrary(IntPtr builder, string path, out UIntPtr count) =>
            RegistryBuilderAddLibraryRaw(builder, ToUtf8(path), -1, out count);

        [DllImport(Dll, EntryPoint = "prism_registry_freeze")]
        public static extern IntPtr RegistryFreeze(IntPtr builder);

        [DllImport(Dll, EntryPoint = "prism_registry_builder_free")]
        public static extern void RegistryBuilderFree(IntPtr builder);

        [DllImport(Dll, EntryPoint = "prism_registry_release")]
        public static extern void RegistryRelease(IntPtr registry);

        [DllImport(Dll, EntryPoint = "prism_registry_count")]
        public static extern UIntPtr RegistryCount(IntPtr ctx);

        [DllImport(Dll, EntryPoint = "prism_registry_id_at")]
        public static extern ulong RegistryIdAt(IntPtr ctx, UIntPtr index);

        [DllImport(Dll, EntryPoint = "prism_registry_name")]
        private static extern IntPtr RegistryNameRaw(IntPtr ctx, ulong id);

        public static string RegistryName(IntPtr ctx, ulong id) => FromUtf8(RegistryNameRaw(ctx, id));

        [DllImport(Dll, EntryPoint = "prism_registry_priority")]
        public static extern int RegistryPriority(IntPtr ctx, ulong id);

        [DllImport(Dll, EntryPoint = "prism_registry_create")]
        public static extern IntPtr RegistryCreate(IntPtr ctx, ulong id);

        [DllImport(Dll, EntryPoint = "prism_backend_get_features")]
        public static extern ulong BackendGetFeatures(IntPtr backend);

        [DllImport(Dll, EntryPoint = "prism_backend_speak")]
        private static extern PrismError BackendSpeakRaw(IntPtr backend, byte[] textUtf8, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        public static PrismError BackendSpeak(IntPtr backend, string text, bool interrupt) =>
            BackendSpeakRaw(backend, ToUtf8(text), interrupt);

        [DllImport(Dll, EntryPoint = "prism_shutdown")]
        public static extern void Shutdown(IntPtr ctx);

        [DllImport(Dll, EntryPoint = "prism_registry_create_best")]
        public static extern IntPtr RegistryCreateBest(IntPtr ctx);

        [DllImport(Dll, EntryPoint = "prism_backend_initialize")]
        public static extern PrismError BackendInitialize(IntPtr backend);

        [DllImport(Dll, EntryPoint = "prism_backend_free")]
        public static extern void BackendFree(IntPtr backend);

        [DllImport(Dll, EntryPoint = "prism_backend_name")]
        private static extern IntPtr BackendNameRaw(IntPtr backend);

        public static string BackendName(IntPtr backend) => FromUtf8(BackendNameRaw(backend));

        // Output = route through the screen reader (respects the user's SR settings + braille).
        [DllImport(Dll, EntryPoint = "prism_backend_output")]
        private static extern PrismError BackendOutputRaw(IntPtr backend, byte[] textUtf8, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        public static PrismError BackendOutput(IntPtr backend, string text, bool interrupt) =>
            BackendOutputRaw(backend, ToUtf8(text), interrupt);

        [DllImport(Dll, EntryPoint = "prism_backend_stop")]
        public static extern PrismError BackendStop(IntPtr backend);

        public static byte[] ToUtf8(string s)
        {
            // Native side expects null-terminated UTF-8.
            int len = Encoding.UTF8.GetByteCount(s);
            var buf = new byte[len + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, buf, 0);
            return buf;
        }

        public static string FromUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) len++;
            if (len == 0) return string.Empty;
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
