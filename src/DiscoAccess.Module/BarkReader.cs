using System;
using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Text;
using DiscoAccess.Module.World;
using HarmonyLib;
using PixelCrushers.DialogueSystem; // Subtitle
using Sunshine;                     // FoBarkUI

namespace DiscoAccess.Module
{
    /// <summary>
    /// Speaks the world's background barks - the TV, ambient NPC chatter, proximity remarks - which a sighted
    /// player reads off the floating world-space text and a blind player otherwise never hears. Every idle,
    /// switcher-driven, and Lua-variable bark is delivered through <see cref="FoBarkUI.Bark"/> (the game's one
    /// <c>IBarkUI</c> implementation), so a single Harmony feeder there pushes each bark's plain text into a
    /// queue the pump drains, spoken queued so it never cuts off the player.
    ///
    /// The reader is gated two ways at drain time: the player's <c>ReadAmbientDialogue</c> setting, and the
    /// world being the live layer (free-roam with control, no menu or conversation over it). Barks that arrive
    /// while the world is not active are dropped rather than flushed late, since a stale ambient line spoken
    /// out of context is worse than silence. Consecutive identical barks (the idle loop repeating its line)
    /// are deduped; genuinely distinct barks that land close together are all kept.
    ///
    /// Composition (what words are said) lives in Core (<see cref="BarkText"/>); this side only extracts live
    /// state and speaks, per the adapter/composition split. Holds no native handle and injects no IL2CPP type,
    /// so it tears down cleanly on reload (its patch rides the module's Harmony instance, and the static
    /// back-reference dies with the collectible load context).
    /// </summary>
    internal sealed class BarkReader : IDisposable
    {
        // The live reader while patched, so the static Harmony postfix can reach the instance queue; cleared
        // on dispose. The module reloads into a collectible context, so this static dies with it.
        private static BarkReader _active;

        private readonly IModHost _host;
        // Composed bark lines queued by the patch (on the Unity main thread, the game's bark path) and drained
        // by the pump. No Unity reference crosses this boundary.
        private readonly Queue<string> _barks = new Queue<string>();
        // The last bark composed, to suppress the idle loop re-speaking the same line back to back. Bookkeeping
        // for dedup only; never read back to the player, so it is not stale game state shown as truth.
        private string _lastBark;

        public BarkReader(IModHost host) { _host = host; }

        /// <summary>Patch the game's bark UI through the module's own Harmony instance, so a reload's
        /// <c>UnpatchSelf</c> removes it.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(FoBarkUI), nameof(FoBarkUI.Bark)),
                postfix: new HarmonyMethod(typeof(BarkReader), nameof(OnBark)));
        }

        /// <summary>Speak any barks queued since last frame, queued so they never interrupt. Silent unless the
        /// player enabled ambient reading and the world is the live layer; barks captured while it is not (a
        /// menu, a conversation, a cutscene over the world) are dropped rather than spoken late. Called from
        /// the pump each frame.</summary>
        public void Drain()
        {
            bool active = _host.Settings.ReadAmbientDialogue.Value && WorldReader.Active?.OwnsKeyboard == true;
            if (!active)
            {
                _barks.Clear();
                return;
            }
            while (_barks.Count > 0)
                _host.Speech.Speak(_barks.Dequeue(), interrupt: false);
        }

        public void Dispose() => _active = null;

        // Harmony feeder. Static (it reaches the live reader through _active), and guards its body and logs any
        // throw: this runs on the game's bark path, where an unlogged failure vanishes.
        //
        // Every bark the game shows lands here with its Subtitle. Read the floating text and the speaker's
        // display name (barks from ambient sources like the TV carry none), compose, and queue - unless it
        // just repeats the previous bark, which the idle loop does on its timer.
        private static void OnBark(Subtitle subtitle)
        {
            BarkReader self = _active;
            if (self == null || subtitle == null) return;
            try
            {
                string line = BarkText.Compose(subtitle.speakerInfo?.Name, subtitle.formattedText?.text);
                if (string.IsNullOrEmpty(line) || line == self._lastBark)
                    return;
                self._lastBark = line;
                self._barks.Enqueue(line);
            }
            catch (Exception e)
            {
                self._host.LogWarning("BarkReader: reading a world bark failed: " + e);
            }
        }
    }
}
