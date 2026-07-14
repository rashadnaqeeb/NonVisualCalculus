using NonVisualCalculus.Core.Audio;
using NonVisualCalculus.Core.Settings;
using NonVisualCalculus.Core.Speech;

namespace NonVisualCalculus.Core.Modularity
{
    /// <summary>
    /// The services the permanent host lends to a reloadable module: a logging seam (kept here so
    /// Core stays free of any BepInEx/Unity reference), the shared speech pipeline, and the mod settings.
    /// The host implements this; the module receives it in <see cref="IModModule.Load"/> and calls back
    /// through it. Loaded in the default load context so host and module agree on this interface's identity.
    /// </summary>
    public interface IModHost
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);

        /// <summary>The running mod version ("1.0.0"), for the launch line and the update check.</summary>
        string ModVersion { get; }

        /// <summary>The single funnel for everything the mod says (the host owns its lifetime).</summary>
        SpeechPipeline Speech { get; }

        /// <summary>The mod's settings, owned by the host so they outlive a module reload and persist
        /// through the host's config file.</summary>
        ModSettings Settings { get; }

        /// <summary>The spatial-audio backend (sonar, wall tones). Host-owned (native device handle), so it
        /// survives a module reload; the module's sensing systems play through it.</summary>
        IAudioEngine Audio { get; }
    }
}
