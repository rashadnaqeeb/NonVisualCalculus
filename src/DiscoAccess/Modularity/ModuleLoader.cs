using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using BepInEx.Logging;
using DiscoAccess.Core.Modularity;

namespace DiscoAccess.Modularity
{
    /// <summary>
    /// Loads, unloads, and reloads the feature module without restarting the game. The module DLL is
    /// read into memory and loaded from its bytes (never from its path) so the on-disk file stays
    /// unlocked and <c>dotnet build</c> can overwrite it while the game runs. Each load gets a fresh
    /// collectible <see cref="AssemblyLoadContext"/>; only the module DLL lives there. Every shared
    /// dependency (Core, the Il2Cpp proxies, BepInEx, Harmony) resolves back to its already-loaded
    /// default-context copy, which is what keeps interface type identity stable across the boundary.
    /// </summary>
    internal sealed class ModuleLoader
    {
        private readonly string _modulePath;
        private readonly IModHost _host;
        private readonly ManualLogSource _log;

        private ModuleAlc _alc;

        public ModuleLoader(string modulePath, IModHost host, ManualLogSource log)
        {
            _modulePath = modulePath;
            _host = host;
            _log = log;
        }

        /// <summary>The live module instance, or null if no load has succeeded. Read fresh each frame.</summary>
        public IModModule Module { get; private set; }

        /// <summary>How many loads have succeeded this process (1 = the boot load; each successful
        /// reload increments). Dev introspection, so a driver can tell a reload actually swapped.</summary>
        public int Generation { get; private set; }

        /// <summary>The module DLL path loads read from, for dev introspection (its write time tells a
        /// driver whether the bytes a reload picked up are the build it just made).</summary>
        public string ModulePath => _modulePath;

        /// <summary>Load the module from its current bytes. Returns false (and logs) on any failure.</summary>
        public bool Load()
        {
            ModuleAlc candidateAlc = null;
            try
            {
                if (!File.Exists(_modulePath))
                {
                    _log.LogError("ModuleLoader: module DLL not found at " + _modulePath);
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(_modulePath);
                candidateAlc = new ModuleAlc();
                Assembly asm;
                using (var ms = new MemoryStream(bytes))
                    asm = candidateAlc.LoadFromStream(ms);

                Type type = asm.GetTypes().FirstOrDefault(
                    t => typeof(IModModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                if (type == null)
                {
                    _log.LogError("ModuleLoader: no IModModule implementor in " + asm.GetName().Name);
                    candidateAlc.Unload();
                    return false;
                }

                var module = (IModModule)Activator.CreateInstance(type);
                module.Load(_host);

                // Swap in only once the new module is fully live, so a failed reload (locked / corrupt /
                // half-written DLL) leaves the running module untouched rather than tearing it down.
                DisposeCurrent();
                _alc = candidateAlc;
                Module = module;
                Generation++;
                _log.LogInfo("ModuleLoader: loaded " + type.FullName + " (generation " + Generation + ")");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError("ModuleLoader.Load failed: " + ex);
                if (candidateAlc != null)
                {
                    try
                    {
                        candidateAlc.Unload();
                    }
                    catch (Exception e)
                    {
                        _log.LogWarning("ModuleLoader: candidate ALC unload threw: " + e);
                    }
                }
                return false;
            }
        }

        /// <summary>Tear down the current module and drop its collectible context (for shutdown).</summary>
        public void Unload() => DisposeCurrent();

        /// <summary>Rebuild from the current bytes, swapping the new module in only if it loads cleanly.</summary>
        public bool Reload() => Load();

        // Dispose the live module and unload its context (old types leak until GC).
        private void DisposeCurrent()
        {
            if (Module != null)
            {
                try
                {
                    Module.Dispose();
                }
                catch (Exception ex)
                {
                    _log.LogError("ModuleLoader: module Dispose threw: " + ex);
                }
                Module = null;
            }

            if (_alc != null)
            {
                try
                {
                    _alc.Unload();
                }
                catch (Exception ex)
                {
                    _log.LogWarning("ModuleLoader: ALC unload threw: " + ex);
                }
                _alc = null;
            }
        }

        // Collectible context for the module alone. Returning null from Load defers every dependency to
        // the default context, so the module shares the host's single copy of Core / the Il2Cpp proxies
        // / BepInEx rather than loading its own.
        private sealed class ModuleAlc : AssemblyLoadContext
        {
            public ModuleAlc() : base(isCollectible: true) { }

            protected override Assembly Load(AssemblyName assemblyName) => null;
        }
    }
}
