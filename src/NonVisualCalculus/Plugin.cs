using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using NonVisualCalculus.Core.Settings;
using NonVisualCalculus.Core.Speech;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Dev;
using NonVisualCalculus.Host;
using NonVisualCalculus.Modularity;
using NonVisualCalculus.Speech;
using Il2CppInterop.Runtime.Injection;

namespace NonVisualCalculus
{
    /// <summary>
    /// The permanent host: only what can never reload. Stands up Prism, the speech pipeline, the dev
    /// server, the one injected pump, and the module loader, then hands per-frame work to the
    /// reloadable module. No feature logic lives here - that's in NonVisualCalculus.Module.
    /// </summary>
    [BepInPlugin(Guid, Name, BuildVersion.Value)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "com.rashad.nonvisualcalculus";
        public const string Name = "Non-Visual Calculus";
        // The version is generated from Directory.Build.props' <Version> at compile time
        // (BuildVersion.g.cs), so it lives in one place.
        public const string Version = BuildVersion.Value;

        internal static ManualLogSource Logger;

        private PrismBackend _prism;
        private Audio.NAudioEngine _audio;
        private ModuleLoader _loader;
#if DEBUG
        private DevServer _devServer;
#endif

        public override void Load()
        {
            Logger = Log;
            Log.LogInfo($"{Name} {Version} loading");

            // Headless/overnight dev runs set NVC_NO_SPEECH=1 so an unattended session doesn't
            // depend on a running screen reader. We skip Prism init (so it never logs a spurious "no
            // backend" and never touches NVDA) but still mute via the pipeline; spoken text is captured
            // for /speech regardless, since the dev tap is upstream of the backend.
            bool muteSpeech = System.Environment.GetEnvironmentVariable("NVC_NO_SPEECH") == "1";
            _prism = new PrismBackend(Log);
            if (muteSpeech)
                Log.LogInfo("Speech muted (NVC_NO_SPEECH=1); spoken text still captured for /speech");
            else
                _prism.Initialize();

            SpeechPipeline.Instance = new SpeechPipeline(_prism) { Muted = muteSpeech };

            // Settings persist through our BepInEx config file and are owned here (permanent) so they
            // survive a module hot-reload; the module reads them through IModHost.Settings.
            LegacyMigration.Run(BepInEx.Paths.ConfigPath, Config, Log);
            var settings = new ModSettings(new BepInExSettingsStore(Config));

            // The spatial-audio backend (sonar, wall tones). Native device, so it lives here in the host
            // beside Prism; the device opens lazily on first sound and self-disables if none is available.
            _audio = new Audio.NAudioEngine(Log);

            var host = new ModHost(Log, SpeechPipeline.Instance, settings, _audio);

            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            string modulePath = Path.Combine(pluginDir, "NonVisualCalculus.Module.dll");
            _loader = new ModuleLoader(modulePath, host, Log);

#if DEBUG
            // Dev-only: the loopback server exposes a C# eval REPL and forces runInBackground, so it
            // must never be in a player build. Started first so its speech tap catches the load line.
            _devServer = new DevServer(_loader, Log);
            _devServer.Start();
#endif

            SpeechPipeline.Instance.Speak(Strings.ModLoaded(Version), interrupt: true);

            // The module carries every feature; if it fails to load the player would otherwise get
            // silence with no signal, so say so out loud (the cause is in the log).
            if (!_loader.Load())
                SpeechPipeline.Instance.Speak(Strings.ModuleFailed, interrupt: false);

            ClassInjector.RegisterTypeInIl2Cpp<HostPump>();
            AddComponent<HostPump>();
            HostPump.Loader = _loader;
#if DEBUG
            HostPump.DevServer = _devServer;
#endif

            Log.LogInfo($"{Name} loaded");
        }
    }
}
