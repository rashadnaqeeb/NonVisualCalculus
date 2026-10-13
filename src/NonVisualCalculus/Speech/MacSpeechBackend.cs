using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using NonVisualCalculus.Core.Speech;
using NonVisualCalculus.Speech.Mac;

namespace NonVisualCalculus.Speech
{
    /// <summary>
    /// The speech backend on macOS, chosen at startup in this order:
    /// <list type="number">
    /// <item>VoiceOver through Prism, when the player has asked for it and the VoiceOver process runs.</item>
    /// <item>A running screen reader that ships a Prism plugin. Screen readers link their plugin under
    /// <c>~/Library/Application Support/&lt;reader&gt;/prism/</c>; every library found there joins Prism's
    /// compiled-in backends, and the highest-priority plugin backend that initializes (it only does while
    /// its reader runs) speaks. This is the Mac counterpart of NVDA on Windows: the player's own reader,
    /// with its voice, rate, dictionary and speech history.</item>
    /// <item>The system voice (<see cref="MacSystemVoice"/>), which the mod renders and plays itself so
    /// queued lines follow each other with no gap.</item>
    /// </list>
    /// Never Prism's "best" backend: its VoiceOver backend speaks through AppleScript, which launches
    /// VoiceOver when it is not running, and it judges VoiceOver running by a presence flag that another
    /// screen reader may keep set. VoiceOver is therefore picked by name and only while its process runs.
    /// VoiceOver is opt-in because its announcement API cannot queue, and the mod's speech leans on a queue.
    /// </summary>
    public sealed class MacSpeechBackend : ISpeechBackend
    {
        private const string BuiltInVoiceOverPrefix = "VoiceOver";

        private readonly ManualLogSource _log;
        private bool _preferVoiceOver;
        private IntPtr _ctx;
        private IntPtr _backend;
        private PrismNative.BackendFeatures _features;
        private MacSystemVoice _systemVoice;

        public MacSpeechBackend(ManualLogSource log, bool preferVoiceOver)
        {
            _log = log;
            _preferVoiceOver = preferVoiceOver;
        }

        public bool IsAvailable => _systemVoice != null || _backend != IntPtr.Zero;

        /// <summary>What is speaking, for the log.</summary>
        public string Description { get; private set; } = "none";

        /// <summary>
        /// True while the mod's own system voice speaks. A screen reader cuts its speech on a key press
        /// itself; the system voice has nobody else to, so the host stops it on every key press.
        /// </summary>
        public bool StopsOnAnyKey => _systemVoice != null;

        public bool Initialize()
        {
            Mac.Log.Install(_log.LogInfo, _log.LogWarning, _log.LogError);

            if (_preferVoiceOver)
            {
                if (VoiceOverIsRunning())
                {
                    if (TryStartPrism(voiceOver: true))
                        return true;
                }
                else
                {
                    _log.LogInfo("Speech: VoiceOver is not running, so it is not used (the mod never starts it)");
                }
            }

            if (TryStartPrism(voiceOver: false))
                return true;

            return TryStartSystemVoice();
        }

        /// <summary>Whether VoiceOver was asked for when the backend was last started.</summary>
        public bool PrefersVoiceOver => _preferVoiceOver;

        /// <summary>Tear down whatever speaks and choose again; how the VoiceOver setting applies at once.</summary>
        public void Reinitialize(bool preferVoiceOver)
        {
            Shutdown();
            _preferVoiceOver = preferVoiceOver;
            Description = "none";
            Initialize();
        }

        /// <summary>Once per frame: the system voice plays what the frame queued and renders the next line.</summary>
        public void Update() => _systemVoice?.Update();

        public void Speak(string text, bool interrupt)
        {
            if (_systemVoice != null)
            {
                _systemVoice.Speak(text, interrupt);
                return;
            }

            if (_backend == IntPtr.Zero)
                return;

            try
            {
                // A screen reader's interrupting output cuts the utterance in flight and may resume its
                // queue afterwards; the mod's interrupt means everything pending goes, as on NVDA.
                if (interrupt && (_features & PrismNative.BackendFeatures.SupportsStop) != 0)
                    PrismNative.BackendStop(_backend);

                var err = (_features & PrismNative.BackendFeatures.SupportsOutput) != 0
                    ? PrismNative.BackendOutput(_backend, text, interrupt)
                    : PrismNative.BackendSpeak(_backend, text, interrupt);
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
            if (_systemVoice != null)
            {
                _systemVoice.Silence();
                return;
            }

            if (_backend == IntPtr.Zero || (_features & PrismNative.BackendFeatures.SupportsStop) == 0)
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

        private bool TryStartSystemVoice()
        {
            var voice = new MacSystemVoice();
            if (!voice.Start())
            {
                _log.LogError("Speech: no speech is available (no screen reader plugin answered and the system voice failed)");
                return false;
            }

            _systemVoice = voice;
            Description = "system voice, " + voice.Description;
            _log.LogInfo("Speech: " + Description);
            return true;
        }

        /// <summary>
        /// Stand up one Prism backend: VoiceOver by name, or else the best screen-reader plugin backend.
        /// False, with the reason logged, when none answers.
        /// </summary>
        private bool TryStartPrism(bool voiceOver)
        {
            try
            {
                IntPtr registry = voiceOver ? IntPtr.Zero : BuildPluginRegistry(out var pluginCount);
                if (!voiceOver && registry == IntPtr.Zero)
                    return false;

                var config = new PrismNative.PrismConfig { Version = PrismNative.ConfigVersion, Registry = registry };
                _ctx = PrismNative.InitWithConfig(ref config);
                if (registry != IntPtr.Zero)
                    PrismNative.RegistryRelease(registry);
                if (_ctx == IntPtr.Zero)
                {
                    _log.LogError("Prism: prism_init returned null context");
                    return false;
                }

                foreach (var candidate in Candidates(voiceOver))
                {
                    IntPtr backend = PrismNative.RegistryCreate(_ctx, candidate.Id);
                    if (backend == IntPtr.Zero)
                    {
                        _log.LogInfo($"Prism: backend '{candidate.Name}' could not be created");
                        continue;
                    }

                    var err = PrismNative.BackendInitialize(backend);
                    if (err != PrismNative.PrismError.Ok && err != PrismNative.PrismError.AlreadyInitialized)
                    {
                        // A screen reader's plugin answers BackendNotAvailable while its reader is not running.
                        _log.LogInfo($"Prism: backend '{candidate.Name}' did not initialize: {err}");
                        PrismNative.BackendFree(backend);
                        continue;
                    }

                    _backend = backend;
                    _features = (PrismNative.BackendFeatures)PrismNative.BackendGetFeatures(backend);
                    Description = "Prism backend " + candidate.Name;
                    _log.LogInfo("Speech: " + Description);
                    return true;
                }

                Shutdown();
                return false;
            }
            catch (DllNotFoundException)
            {
                _log.LogError("Prism: libprism.dylib not found (expected beside the plugin)");
                Shutdown();
                return false;
            }
            catch (Exception ex)
            {
                _log.LogError("Prism: initialization error: " + ex);
                Shutdown();
                return false;
            }
        }

        private readonly struct Candidate
        {
            public readonly ulong Id;
            public readonly string Name;
            public readonly int Priority;

            public Candidate(ulong id, string name, int priority)
            {
                Id = id;
                Name = name;
                Priority = priority;
            }
        }

        /// <summary>The backends to try, best first: VoiceOver alone, or every plugin backend.</summary>
        private IEnumerable<Candidate> Candidates(bool voiceOver)
        {
            var all = new List<Candidate>();
            ulong count = (ulong)PrismNative.RegistryCount(_ctx);
            for (ulong i = 0; i < count; i++)
            {
                ulong id = PrismNative.RegistryIdAt(_ctx, (UIntPtr)i);
                all.Add(new Candidate(id, PrismNative.RegistryName(_ctx, id) ?? "", PrismNative.RegistryPriority(_ctx, id)));
            }

            if (voiceOver)
                return all.Where(c => c.Name.StartsWith(BuiltInVoiceOverPrefix, StringComparison.Ordinal));

            var builtIn = BuiltInBackendNames();
            return all.Where(c => !builtIn.Contains(c.Name)).OrderByDescending(c => c.Priority);
        }

        /// <summary>The names of Prism's compiled-in backends, read from its global registry.</summary>
        private static HashSet<string> BuiltInBackendNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            IntPtr ctx = PrismNative.Init(IntPtr.Zero);
            if (ctx == IntPtr.Zero)
                return names;
            try
            {
                ulong count = (ulong)PrismNative.RegistryCount(ctx);
                for (ulong i = 0; i < count; i++)
                    names.Add(PrismNative.RegistryName(ctx, PrismNative.RegistryIdAt(ctx, (UIntPtr)i)) ?? "");
            }
            finally
            {
                PrismNative.Shutdown(ctx);
            }

            return names;
        }

        /// <summary>
        /// A registry of Prism's compiled-in backends plus every screen-reader plugin found; Zero when no
        /// plugin loaded, since the compiled-in set alone has nothing this path would pick.
        /// </summary>
        private IntPtr BuildPluginRegistry(out int pluginCount)
        {
            pluginCount = 0;
            IntPtr builder = PrismNative.RegistryBuilderNew();
            if (builder == IntPtr.Zero)
            {
                _log.LogWarning("Prism: no registry builder; screen reader plugins cannot be loaded");
                return IntPtr.Zero;
            }

            foreach (string path in PluginPaths())
            {
                var err = PrismNative.RegistryBuilderAddLibrary(builder, path, out var added);
                if (err == PrismNative.PrismError.Ok)
                {
                    pluginCount++;
                    _log.LogInfo($"Prism: screen reader plugin loaded ({added} backend(s)): {path}");
                }
                else
                {
                    _log.LogInfo($"Prism: screen reader plugin not loaded ({err}): {path}");
                }
            }

            IntPtr registry = pluginCount > 0 ? PrismNative.RegistryFreeze(builder) : IntPtr.Zero;
            PrismNative.RegistryBuilderFree(builder);
            if (pluginCount == 0)
                _log.LogInfo("Prism: no screen reader plugin found");
            return registry;
        }

        /// <summary>Every <c>*.dylib</c> under <c>~/Library/Application Support/*/prism/</c>.</summary>
        private IEnumerable<string> PluginPaths()
        {
            string home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home))
                home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string support = Path.Combine(home, "Library", "Application Support");
            if (!Directory.Exists(support))
                return Array.Empty<string>();

            var paths = new List<string>();
            foreach (string app in Directory.EnumerateDirectories(support))
            {
                string prismDir = Path.Combine(app, "prism");
                if (!Directory.Exists(prismDir))
                    continue;
                try
                {
                    paths.AddRange(Directory.EnumerateFiles(prismDir, "*.dylib"));
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"Prism: could not list {prismDir}: {ex.Message}");
                }
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        /// <summary>Whether the VoiceOver process is running, asked of the running applications rather
        /// than the presence flag another screen reader may keep set.</summary>
        private bool VoiceOverIsRunning()
        {
            IntPtr pool = ObjC.AutoreleasePoolPush();
            try
            {
                ObjC.LoadSpeechFrameworks();
                IntPtr running = ObjC.Send(
                    ObjC.Class("NSRunningApplication"),
                    ObjC.Sel("runningApplicationsWithBundleIdentifier:"),
                    ObjC.NSString("com.apple.VoiceOver"));
                return ObjC.Count(running) > 0;
            }
            catch (Exception ex)
            {
                _log.LogError("Speech: asking whether VoiceOver runs failed: " + ex.Message);
                return false;
            }
            finally
            {
                ObjC.AutoreleasePoolPop(pool);
            }
        }

        public void Shutdown()
        {
            _systemVoice?.Stop();
            _systemVoice = null;
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
        }
    }
}
