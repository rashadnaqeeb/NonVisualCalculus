using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiscoAccess.Core.Settings;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.Strings;
using DiscoAccess.Dev;
using DiscoAccess.Host;
using DiscoAccess.Modularity;
using DiscoAccess.Speech;
using Il2CppInterop.Runtime.Injection;

namespace DiscoAccess
{
    /// <summary>
    /// The permanent host: only what can never reload. Stands up Prism, the speech pipeline, the dev
    /// server, the one injected pump, and the module loader, then hands per-frame work to the
    /// reloadable module. No feature logic lives here - that's in DiscoAccess.Module.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "com.rashad.discoaccess";
        public const string Name = "DiscoAccess";
        public const string Version = "0.1.0";

        internal static ManualLogSource Logger;

        private PrismBackend _prism;
        private ModuleLoader _loader;
#if DEBUG
        private DevServer _devServer;
#endif

        public override void Load()
        {
            Logger = Log;
            Log.LogInfo($"{Name} {Version} loading");

            // Headless/overnight dev runs set DISCOACCESS_NO_SPEECH=1 so an unattended session doesn't
            // depend on a running screen reader. We skip Prism init (so it never logs a spurious "no
            // backend" and never touches NVDA) but still mute via the pipeline; spoken text is captured
            // for /speech regardless, since the dev tap is upstream of the backend.
            bool muteSpeech = System.Environment.GetEnvironmentVariable("DISCOACCESS_NO_SPEECH") == "1";
            _prism = new PrismBackend(Log);
            if (muteSpeech)
                Log.LogInfo("Speech muted (DISCOACCESS_NO_SPEECH=1); spoken text still captured for /speech");
            else
                _prism.Initialize();

            SpeechPipeline.Instance = new SpeechPipeline(_prism) { Muted = muteSpeech };

            // Settings persist through our BepInEx config file and are owned here (permanent) so they
            // survive a module hot-reload; the module reads them through IModHost.Settings.
            var settings = new ModSettings(new BepInExSettingsStore(Config));

            var host = new ModHost(Log, SpeechPipeline.Instance, settings);

            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            string modulePath = Path.Combine(pluginDir, "DiscoAccess.Module.dll");
            _loader = new ModuleLoader(modulePath, host, Log);

#if DEBUG
            // Dev-only: the loopback server exposes a C# eval REPL and forces runInBackground, so it
            // must never be in a player build. Started first so its speech tap catches the load line.
            _devServer = new DevServer(_loader, Log);
            _devServer.Start();
#endif

            SpeechPipeline.Instance.Speak(Strings.ModLoaded, interrupt: true);

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
