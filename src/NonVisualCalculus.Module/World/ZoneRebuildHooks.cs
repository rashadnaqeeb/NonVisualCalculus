using System;
using NonVisualCalculus.Core.Modularity;
using HarmonyLib;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// Rebuilds the zone map when the game changes the navmesh outside a door swing: a story-state
    /// switch (<c>AreaStatePlaceholder.SwitchTo</c>, whose StateSwitchers carve obstacles - the
    /// waterlock gate, the eternite boards, the church's drug lab) and a Lua blocker toggle
    /// (<c>LuaBlockerSwitcher.BooleanChanged</c>, the flashlight gate and the fortress floor and path
    /// blockers that follow the character between levels). Both are postfixes that only schedule the
    /// tracker's settled rebuild, on this load's Harmony so a reload unpatches them. Owns nothing.
    /// </summary>
    internal sealed class ZoneRebuildHooks : IDisposable
    {
        private static ZoneRebuildHooks _active;
        private readonly IModHost _host;

        public ZoneRebuildHooks(IModHost host) { _host = host; }

        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(Sunshine.AreaStatePlaceholder), nameof(Sunshine.AreaStatePlaceholder.SwitchTo)),
                postfix: new HarmonyMethod(typeof(ZoneRebuildHooks), nameof(OnStateSwitched)));
            harmony.Patch(
                AccessTools.Method(typeof(LuaBlockerSwitcher), nameof(LuaBlockerSwitcher.BooleanChanged)),
                postfix: new HarmonyMethod(typeof(ZoneRebuildHooks), nameof(OnBooleanChanged)));
        }

        public void Dispose() => _active = null;

        // Only a switch whose StateSwitchers live in the active scene can have carved its navmesh; a
        // switch for another area (the game applies every area's state on a load) must not cost a rebuild.
        private static void OnStateSwitched(string areaName)
        {
            ZoneRebuildHooks self = _active;
            if (self == null) return;
            try
            {
                var loc = Sunshine.AreaStatePlaceholder.GetLocation(areaName);
                if (loc == null) return;
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                foreach (var sw in loc.stateSwitchers)
                    if (sw != null && sw.gameObject.scene == scene) { ZoneTracker.Active?.InvalidateSoon(); return; }
            }
            catch (Exception e) { self._host.LogWarning("ZoneRebuildHooks: reading a state switch failed: " + e); }
        }

        // The game raises BooleanChanged on every switcher for every Lua boolean that changes; the
        // switcher itself only acts on its own name, and only that toggles obstacles.
        private static void OnBooleanChanged(LuaBlockerSwitcher __instance, string boolean)
        {
            ZoneRebuildHooks self = _active;
            if (self == null || __instance == null) return;
            try
            {
                if (__instance.booleanName == boolean && __instance.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                    ZoneTracker.Active?.InvalidateSoon();
            }
            catch (Exception e) { self._host.LogWarning("ZoneRebuildHooks: reading a blocker toggle failed: " + e); }
        }
    }
}
