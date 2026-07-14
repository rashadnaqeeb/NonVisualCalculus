using BepInEx.Logging;
using NonVisualCalculus.Core.Audio;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Settings;
using NonVisualCalculus.Core.Speech;

namespace NonVisualCalculus.Modularity
{
    /// <summary>
    /// The host's <see cref="IModHost"/>: routes the module's logging through the BepInEx logger and
    /// hands it the shared speech pipeline and settings. Lives in the default load context with Core, so the
    /// module (in its collectible context) sees the same <see cref="IModHost"/> type identity.
    /// </summary>
    internal sealed class ModHost : IModHost
    {
        private readonly ManualLogSource _log;

        public ModHost(ManualLogSource log, SpeechPipeline speech, ModSettings settings, IAudioEngine audio)
        {
            _log = log;
            Speech = speech;
            Settings = settings;
            Audio = audio;
        }

        public SpeechPipeline Speech { get; }

        public ModSettings Settings { get; }

        public IAudioEngine Audio { get; }

        public string ModVersion => Plugin.Version;

        public void LogInfo(string message) => _log.LogInfo(message);
        public void LogWarning(string message) => _log.LogWarning(message);
        public void LogError(string message) => _log.LogError(message);
    }
}
