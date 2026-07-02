using System;
using DiscoAccess.Core.Modularity;
using FortressOccident;       // DialogueImage
using HarmonyLib;
using Pages.Gameplay;         // FakeDialogueStartPage

namespace DiscoAccess.Module
{
    /// <summary>
    /// Speaks the literary quote the game presents as a full-screen picture, which a blind player
    /// otherwise never knows exists. The words are baked into a per-language sprite, so no text
    /// component ever holds them; the game displays it through two paths, both patched here: the
    /// new-game start page renders it directly (<see cref="FakeDialogueStartPage"/>, before the begin
    /// prompt), and the intro shows it as the <see cref="DialogueImage"/> overlay "furies". The spoken
    /// text is the game's own: the localization database carries the quote as the text term
    /// INTRO_FURIES_QUOTE alongside the image term the overlay resolves, so nothing is authored and a
    /// language switch follows the game. The feeders queue a marker; the pump drains it and the
    /// translation is read at speech time.
    ///
    /// Holds no native handle and injects no IL2CPP type, so it tears down cleanly on reload (its
    /// patches go with the module's Harmony instance, and the static back-reference dies with the
    /// collectible load context).
    /// </summary>
    internal sealed class QuoteReader : IDisposable
    {
        // The overlay name and the localization term carrying the same quote as text.
        private const string FuriesImage = "furies";
        private const string FuriesTextTerm = "INTRO_FURIES_QUOTE";

        // The live reader while patched, so the static Harmony postfixes can reach it; cleared on
        // dispose. The module reloads into a collectible context, so this static dies with it.
        private static QuoteReader _active;

        private readonly IModHost _host;
        // Set by the feeders (on the Unity main thread, the game's display path) and consumed by the
        // pump. A flag, not a queue: the two display paths can fire in the same flow, and the screen
        // shows one quote, so one frame speaks it once.
        private bool _pending;

        public QuoteReader(IModHost host) { _host = host; }

        /// <summary>Patch the quote's display paths through the module's own Harmony instance, so a
        /// reload's <c>UnpatchSelf</c> removes them.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(FakeDialogueStartPage), "DisplayFuriesQuote"),
                postfix: new HarmonyMethod(typeof(QuoteReader), nameof(OnStartPageQuote)));
            harmony.Patch(
                AccessTools.Method(typeof(DialogueImage), nameof(DialogueImage.Show)),
                postfix: new HarmonyMethod(typeof(QuoteReader), nameof(OnImageShown)));
            harmony.Patch(
                AccessTools.Method(typeof(DialogueImage), nameof(DialogueImage.ShowLocalized)),
                postfix: new HarmonyMethod(typeof(QuoteReader), nameof(OnImageShown)));
            harmony.Patch(
                AccessTools.Method(typeof(DialogueImage), nameof(DialogueImage.InstantShow)),
                postfix: new HarmonyMethod(typeof(QuoteReader), nameof(OnImageShown)));
        }

        /// <summary>Speak a quote displayed since last frame. Called from the pump each frame; the
        /// translation is resolved here, at speech time, in the current language.</summary>
        public void Drain()
        {
            if (!_pending)
                return;
            _pending = false;
            string quote = GameLocalization.Translate(FuriesTextTerm);
            if (string.IsNullOrEmpty(quote))
            {
                _host.LogWarning("QuoteReader: term " + FuriesTextTerm + " resolved to nothing; the quote goes unspoken.");
                return;
            }
            _host.Speech.Speak(quote, interrupt: false);
        }

        public void Dispose() => _active = null;

        // --- Harmony feeders. Static (they reach the live reader through _active). They only set the
        // flag - no game state is read here, so there is nothing to guard; the read that can fail
        // happens in Drain, which logs. ---

        // The new-game start page rendered the quote directly (its texture, not the overlay).
        private static void OnStartPageQuote()
        {
            QuoteReader self = _active;
            if (self == null) return;
            self._pending = true;
        }

        // A DialogueImage overlay came up; only the furies one is the quote (darkness, whiteness and
        // the title card carry no words to read).
        private static void OnImageShown(string imageName)
        {
            QuoteReader self = _active;
            if (self == null) return;
            if (imageName == FuriesImage)
                self._pending = true;
        }
    }
}
