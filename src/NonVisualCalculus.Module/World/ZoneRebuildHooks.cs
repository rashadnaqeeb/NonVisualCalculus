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
                postfix: new HarmonyMethod(typeof(ZoneRebuildHooks), nameof(OnNavmeshChanged)));
            harmony.Patch(
                AccessTools.Method(typeof(LuaBlockerSwitcher), nameof(LuaBlockerSwitcher.BooleanChanged)),
                postfix: new HarmonyMethod(typeof(ZoneRebuildHooks), nameof(OnNavmeshChanged)));
        }

        public void Dispose() => _active = null;

        private static void OnNavmeshChanged()
        {
            ZoneRebuildHooks self = _active;
            if (self == null) return;
            try { ZoneTracker.Active?.InvalidateSoon(); }
            catch (Exception e) { self._host.LogWarning("ZoneRebuildHooks: scheduling a zone rebuild failed: " + e); }
        }
    }
}
