using System;
using HarmonyLib;

namespace DiscoAccess.Module.Input
{
    /// <summary>
    /// Mutes the game's own number-row response-select while our navigator owns the keyboard, so a digit
    /// only moves our cursor (the dialogue viewer's jump-to-choice, <see cref="DialogueChoiceInput"/>) and
    /// never clicks the option out from under it.
    ///
    /// DE selects a response by number through <c>PixelCrushers.UIButtonKeyTrigger</c>, a per-button
    /// MonoBehaviour whose <c>Update</c> reads <c>UnityEngine.Input.GetKeyDown(this.key)</c> directly and
    /// fires the button's click. That read is raw Unity input, NOT routed through InControl - so disabling
    /// InControl (which is how we take the keyboard everywhere else) leaves it firing, and the number press
    /// both moves our cursor and auto-commits the choice. A prefix that skips <c>Update</c> while we own the
    /// keyboard is the one place to stop it: the game never sees the press as a click, our reader still sees
    /// it as a move. Response buttons are <c>SunshineUIButtonKeyTrigger</c>, which inherits this base
    /// <c>Update</c> (it overrides only <c>OnInputKey</c>), so the single base patch covers them.
    ///
    /// Gated on the navigator owning the keyboard rather than on dialogue specifically: whenever we drive a
    /// screen we are the sole input authority, so any of the game's raw-input button hotkeys firing
    /// underneath is unwanted, not only in a conversation.
    /// </summary>
    internal static class ButtonKeyTriggerGuard
    {
        // Reads whether our navigator owns the keyboard this frame. Set at Apply; the prefix is static (a
        // Harmony hook), so it reaches live ownership through this delegate. Lives in the module's
        // collectible load context and dies with it on reload.
        private static Func<bool> _navigatorOwnsKeyboard;

        /// <summary>Patch DE's number-key response-select through the module's own Harmony instance, so a
        /// reload's <c>UnpatchSelf</c> removes it. <paramref name="navigatorOwnsKeyboard"/> reads the live
        /// keyboard ownership each frame.</summary>
        public static void Apply(Harmony harmony, Func<bool> navigatorOwnsKeyboard)
        {
            _navigatorOwnsKeyboard = navigatorOwnsKeyboard;
            harmony.Patch(
                AccessTools.Method(typeof(PixelCrushers.UIButtonKeyTrigger), "Update"),
                prefix: new HarmonyMethod(typeof(ButtonKeyTriggerGuard), nameof(Skip)));
        }

        // Return false to skip the game's Update - and with it the raw-input click - while our navigator owns
        // the keyboard. Runs on the Unity main thread every frame for every key-trigger button, so it stays a
        // bare flag read.
        private static bool Skip() => _navigatorOwnsKeyboard == null || !_navigatorOwnsKeyboard();
    }
}
