using System;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using HarmonyLib;
using Sunshine;
using Voidforge;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Speaks the two container events the game marks with sound alone, via Harmony feeders drained from
    /// the pump: a locked container refusing to open (the game plays only a rattle - OnUse's locked
    /// branch; a pryable one routes to its prying conversation instead, which the dialogue screen reads),
    /// and the loot panel closing (whatever closed it: the Close button, taking the last item, Take all,
    /// or the game itself when the player walks out of range - every path funnels into
    /// <see cref="Sunshine.Container.Hide(ContainerSource)"/>). The panel's contents while open are read by
    /// <see cref="Nav.ContainerPanelScreen"/>. Owns no native handle; its patches ride the module's Harmony.
    /// </summary>
    internal sealed class ContainerReader : IDisposable
    {
        // The live reader while patched, so the static Harmony feeders can reach the instance flags;
        // cleared on dispose. The module reloads into a collectible context, so this static dies with it.
        private static ContainerReader _active;

        private readonly IModHost _host;
        // Set by the feeders (on the Unity main thread, the game's interaction path) and drained by the
        // pump. Flags, not queues: two locked rattles in one frame are one refusal to the player.
        private bool _lockedPending;
        private bool _closedPending;

        public ContainerReader(IModHost host) { _host = host; }

        /// <summary>Patch the locked refusal and the panel close through the module's own Harmony
        /// instance, so a reload's <c>UnpatchSelf</c> removes them.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(ContainerSource), nameof(ContainerSource.OnUse)),
                postfix: new HarmonyMethod(typeof(ContainerReader), nameof(OnContainerUsed)));
            harmony.Patch(
                AccessTools.Method(typeof(Sunshine.Container), nameof(Sunshine.Container.Hide),
                    new[] { typeof(ContainerSource) }),
                prefix: new HarmonyMethod(typeof(ContainerReader), nameof(OnPanelHiding)));
        }

        /// <summary>Speak what the feeders recorded since last frame. Called from the pump each frame,
        /// after the notification drain so a take reads "received ..." before "container closed".</summary>
        public void Drain()
        {
            if (_lockedPending)
            {
                _lockedPending = false;
                // The game's own tooltip word for a locked thing, so it localizes; authored fallback.
                string line = GameLocalization.Translate("TOOLTIP_LOCKED");
                _host.Speech.Speak(string.IsNullOrEmpty(line) ? Strings.StatusLocked : line, interrupt: false);
            }
            if (_closedPending)
            {
                _closedPending = false;
                _host.Speech.Speak(Strings.WorldContainerClosed, interrupt: false);
            }
        }

        public void Dispose() => _active = null;

        // --- Harmony feeders. Static (they reach the live reader through _active), and each guards its body
        // and logs any throw: they run on the game's interaction path, where an unlogged failure vanishes. ---

        // The arrival interaction with a container. Unlocked opens the panel (the screen announces itself);
        // locked only plays the rattle, so that is the case to voice.
        private static void OnContainerUsed(ContainerSource __instance)
        {
            ContainerReader self = _active;
            if (self == null || __instance == null) return;
            try
            {
                if (__instance.isLocked)
                    self._lockedPending = true;
            }
            catch (Exception e)
            {
                self._host.LogWarning("ContainerReader: reading a container use failed: " + e);
            }
        }

        // Every close funnels through Hide(source). Only a hide of the source the panel is actually
        // showing is a player-visible close (Hide also runs on teardown paths with the panel already
        // idle); checked in a prefix, before Hide clears the binding.
        private static void OnPanelHiding(ContainerSource source)
        {
            ContainerReader self = _active;
            if (self == null || source == null) return;
            try
            {
                Sunshine.Container panel = SingletonComponent<Sunshine.Container>.Singleton;
                if (panel == null || panel.Source == null || panel.Source.Pointer != source.Pointer) return;
                self._closedPending = true;
            }
            catch (Exception e)
            {
                self._host.LogWarning("ContainerReader: reading the panel close failed: " + e);
            }
        }
    }
}
