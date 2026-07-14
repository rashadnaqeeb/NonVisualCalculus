using System;
using BepInEx.Logging;
using NonVisualCalculus.Core.Speech;

namespace NonVisualCalculus.Speech
{
    /// <summary>
    /// ISpeechBackend over Prism. Owns the native context + chosen backend lifecycle. Policy lives in
    /// the Core SpeechPipeline (clean / dedup / interrupt); this only emits.
    /// </summary>
    public sealed class PrismBackend : ISpeechBackend
    {
        private readonly ManualLogSource _log;
        private IntPtr _ctx;
        private IntPtr _backend;

        public bool IsAvailable { get; private set; }

        public PrismBackend(ManualLogSource log)
        {
            _log = log;
        }

        public bool Initialize()
        {
            try
            {
                _ctx = PrismNative.Init(IntPtr.Zero);
                if (_ctx == IntPtr.Zero)
                {
                    _log.LogError("Prism: prism_init returned null context");
                    return false;
                }

                _backend = PrismNative.RegistryCreateBest(_ctx);
                if (_backend == IntPtr.Zero)
                {
                    _log.LogError("Prism: no usable speech backend (is a screen reader running?)");
                    return false;
                }

                // create_best already returns an initialized backend; calling initialize again is
                // harmless and reports AlreadyInitialized, which we treat as success.
                var err = PrismNative.BackendInitialize(_backend);
                if (err != PrismNative.PrismError.Ok && err != PrismNative.PrismError.AlreadyInitialized)
                {
                    _log.LogError("Prism: backend initialize failed: " + err);
                    return false;
                }

                IsAvailable = true;
                _log.LogInfo("Prism backend ready: " + PrismNative.BackendName(_backend));
                return true;
            }
            catch (DllNotFoundException)
            {
                _log.LogError("Prism: prism.dll not found (expected next to disco.exe)");
                return false;
            }
            catch (Exception ex)
            {
                _log.LogError("Prism: initialization error: " + ex);
                return false;
            }
        }

        public void Speak(string text, bool interrupt)
        {
            if (!IsAvailable)
                return;
            try
            {
                var err = PrismNative.BackendOutput(_backend, text, interrupt);
                if (err != PrismNative.PrismError.Ok)
                    _log.LogWarning($"Prism: output returned {err}, line not spoken: {text}");
            }
            catch (Exception ex)
            {
                _log.LogWarning("Prism: speak failed: " + ex.Message);
            }
        }

        public void Stop()
        {
            if (!IsAvailable)
                return;
            try
            {
                PrismNative.BackendStop(_backend);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Prism: stop failed: " + ex.Message);
            }
        }

        public void Shutdown()
        {
            if (_backend != IntPtr.Zero)
            {
                PrismNative.BackendFree(_backend);
                _backend = IntPtr.Zero;
            }
            if (_ctx != IntPtr.Zero)
            {
                PrismNative.Shutdown(_ctx);
                _ctx = IntPtr.Zero;
            }
            IsAvailable = false;
        }
    }
}
