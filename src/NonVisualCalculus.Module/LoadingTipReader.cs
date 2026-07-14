using System;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using HarmonyLib;
// TipsDisplayer lives in the global namespace.

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Speaks the gameplay tip the game shows on the loading screen, which a sighted player reads while
    /// the load runs and a blind player otherwise never hears. The game picks one random tip per load
    /// (<see cref="TipsDisplayer"/>, a term from the lockit's Tips category resolved through I2) and
    /// shows it only on image loading screens - black-splash loads hide the tip text entirely. The
    /// feeder records the live displayer; the pump drains it a frame later and reads the on-screen
    /// text, honouring the same gate, so exactly what is displayed is what is spoken.
    ///
    /// Holds no native handle and injects no IL2CPP type, so it tears down cleanly on reload (its patch
    /// goes with the module's Harmony instance, and the static back-reference dies with the collectible
    /// load context).
    /// </summary>
    internal sealed class LoadingTipReader : IDisposable
    {
        // The live reader while patched, so the static Harmony postfix can reach it; cleared on
        // dispose. The module reloads into a collectible context, so this static dies with it.
        private static LoadingTipReader _active;

        private readonly IModHost _host;
        // The displayer whose tip is not yet spoken: a live component reference, read at speech time.
        // A slot, not a queue - one loading screen is up at a time.
        private TipsDisplayer _pending;

        public LoadingTipReader(IModHost host) { _host = host; }

        /// <summary>Patch the tip display through the module's own Harmony instance, so a reload's
        /// <c>UnpatchSelf</c> removes it.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(TipsDisplayer), "OnEnable"),
                postfix: new HarmonyMethod(typeof(LoadingTipReader), nameof(OnTipShown)));
        }

        /// <summary>Speak the tip if a loading screen came up since last frame. Called from the pump;
        /// the text is read here, at speech time, from the same component the screen renders.</summary>
        public void Drain()
        {
            TipsDisplayer displayer = _pending;
            if (displayer == null) return; // the Unity null also covers a displayer destroyed since
            _pending = null;
            try
            {
                // The game hides the tip on black-splash loads (tipText disabled); stay silent with it.
                TMPro.TextMeshProUGUI text = displayer.tipText;
                if (text == null || !text.enabled) return;
                if (string.IsNullOrEmpty(text.text))
                {
                    _host.LogWarning("LoadingTipReader: the loading screen's tip text is empty; nothing to speak.");
                    return;
                }
                _host.Speech.Speak(Strings.LoadingTip(GameLocalization.Spoken(text)), interrupt: false);
            }
            catch (Exception e)
            {
                _host.LogWarning("LoadingTipReader: reading the loading tip failed: " + e);
            }
        }

        public void Dispose() => _active = null;

        // Harmony feeder: static (it reaches the live reader through _active). It records the displayer
        // and nothing else - the reads that can fail happen in Drain, which logs.
        private static void OnTipShown(TipsDisplayer __instance)
        {
            LoadingTipReader self = _active;
            if (self == null) return;
            self._pending = __instance;
        }
    }
}
