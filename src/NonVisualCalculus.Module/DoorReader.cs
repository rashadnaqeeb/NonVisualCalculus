using System;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using FortressOccident;
using HarmonyLib;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Speaks the locked-door refusal, which the game marks with sound alone: <see cref="Door.OnUse"/>'s
    /// locked branch plays a rattle and returns without toggling, and KimDoor's OnUse override is nothing
    /// but that rattle - every locked path funnels through <see cref="Door.PlayDoorSound"/> with the
    /// "locked" sound key, so that is the one patch. A Harmony feeder drained from the pump (the
    /// <see cref="ContainerReader"/> pattern). Owns no native handle; its patch rides the module's Harmony.
    /// </summary>
    internal sealed class DoorReader : IDisposable
    {
        // The game's sound key for the locked-door rattle (Door.OnUse's locked branch, KimDoor.OnUse).
        private const string LockedSound = "locked";

        // The live reader while patched, so the static Harmony feeder can reach the instance flag;
        // cleared on dispose. The module reloads into a collectible context, so this static dies with it.
        private static DoorReader _active;

        private readonly IModHost _host;
        // Set by the feeder (on the Unity main thread, the game's interaction path) and drained by the
        // pump. A flag, not a queue: two locked rattles in one frame are one refusal to the player.
        private bool _lockedPending;

        public DoorReader(IModHost host) { _host = host; }

        /// <summary>Patch the locked refusal through the module's own Harmony instance, so a reload's
        /// <c>UnpatchSelf</c> removes it.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(Door), nameof(Door.PlayDoorSound)),
                prefix: new HarmonyMethod(typeof(DoorReader), nameof(OnDoorSound)));
        }

        /// <summary>Speak what the feeder recorded since last frame. Called from the pump each frame.</summary>
        public void Drain()
        {
            if (!_lockedPending) return;
            _lockedPending = false;
            // The game's own tooltip word for a locked thing, so it localizes; authored fallback. The
            // same line the locked-container refusal speaks - one word for one kind of refusal.
            string line = GameLocalization.Translate("TOOLTIP_LOCKED");
            _host.Speech.Speak(string.IsNullOrEmpty(line) ? Strings.StatusLocked : line, interrupt: false);
        }

        public void Dispose() => _active = null;

        // The Harmony feeder. Static (it reaches the live reader through _active), guarding its body and
        // logging any throw: it runs on the game's interaction path, where an unlogged failure vanishes.
        //
        // A prefix, not a postfix: the method consults ignoreNextSoundPlay (set around scene-load state
        // restoration so a restored door does not creak) and resets it before returning, so reading it
        // here mirrors the game's own suppression - "locked" is spoken exactly when the player hears the
        // rattle, never on a silent restoration call.
        private static void OnDoorSound(Door __instance, string sound)
        {
            DoorReader self = _active;
            if (self == null || __instance == null) return;
            try
            {
                if (sound == LockedSound && !__instance.ignoreNextSoundPlay)
                    self._lockedPending = true;
            }
            catch (Exception e)
            {
                self._host.LogWarning("DoorReader: reading a door sound failed: " + e);
            }
        }
    }
}
