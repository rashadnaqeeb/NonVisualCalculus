using System;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using HarmonyLib;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Speaks an authored description of a silent cinematic scene when it starts. These scenes are staged
    /// camera-and-animation sequences with no game text - a sighted player watches them, a blind player
    /// gets silence - so the words come from the mod's strings table (<see cref="Strings"/>), authored
    /// from watching each scene frame by frame.
    ///
    /// Covered: the new-game wake-up (the room reveal after the opening dream dialogue). Its trigger is
    /// <see cref="WhirlingNewGameManager.TequilaHasWokenUp"/>, the method the "open your eyes" click's
    /// Lua sequencer command (TequilaWakeUp) invokes to start the reveal - the white flash, the hangover
    /// filter, and the get-up that follows. The skip-intro new-game path calls it too (with skipIntro
    /// true); nothing calls it on a save load.
    ///
    /// Holds no native handle and injects no IL2CPP type, so it tears down cleanly on reload (its patch
    /// rides the module's Harmony instance, and the static back-reference dies with the collectible load
    /// context).
    /// </summary>
    internal sealed class CutsceneReader : IDisposable
    {
        // The live reader while patched, so the static Harmony postfix can reach it; cleared on dispose.
        private static CutsceneReader _active;

        private readonly IModHost _host;
        // Set by the feeder (on the Unity main thread, the game's cutscene path) and consumed by the
        // pump. The description to speak, or null when nothing is pending.
        private string _pending;

        public CutsceneReader(IModHost host) { _host = host; }

        /// <summary>Patch the cutscene entry points through the module's own Harmony instance, so a
        /// reload's <c>UnpatchSelf</c> removes them.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(WhirlingNewGameManager), nameof(WhirlingNewGameManager.TequilaHasWokenUp)),
                postfix: new HarmonyMethod(typeof(CutsceneReader), nameof(OnNewGameWakeUp)));
        }

        /// <summary>Speak a description queued since last frame, queued so it never cuts off the dialogue
        /// line the player just left. Called from the pump each frame.</summary>
        public void Drain()
        {
            if (_pending == null)
                return;
            string text = _pending;
            _pending = null;
            _host.Speech.Speak(text, interrupt: false);
        }

        public void Dispose() => _active = null;

        // Harmony feeder. Static (it reaches the live reader through _active); only sets the pending
        // description, so there is no game-state read here to fail.
        private static void OnNewGameWakeUp()
        {
            CutsceneReader self = _active;
            if (self == null) return;
            self._pending = Strings.CutsceneNewGameWakeUp;
        }
    }
}
