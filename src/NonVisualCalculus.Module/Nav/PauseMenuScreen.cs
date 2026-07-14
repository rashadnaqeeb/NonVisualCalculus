using System;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The in-game pause menu, which the game draws with the same MAINMENU view as the title menu but with
    /// in-game actions (Continue to resume, Quick Save, Save Game, the return-to-title Main Menu button). It
    /// reuses <see cref="MainMenuScreen"/>'s button list but announces itself as the pause menu and, unlike
    /// the title menu, wraps it in a closeable root so Escape resumes the game the game's own way (the
    /// MAINMENU view's CloseOnEscapeKey). It is the more specific MAINMENU screen, registered before the
    /// title-menu fallback. It adds one entry of the mod's own above Options: the learn-game-sounds menu
    /// (a <see cref="LearnSoundsScreen"/> overlay, opened through the toggle the ScreenManager hands in).
    /// </summary>
    public sealed class PauseMenuScreen : MainMenuScreen
    {
        private readonly Action<ModOverlay> _toggleOverlay;

        public PauseMenuScreen(Action<ModOverlay> toggleOverlay) => _toggleOverlay = toggleOverlay;

        public override string ScreenName => Strings.ScreenPauseMenu;

        // This is the pause menu, not the title menu, when the return-to-title "Main Menu" button is present
        // and active: that action exists only while a game is loaded. (A game-session signal would read
        // cleaner, but DE has none we could find: GameMenuManager exists at the title too, and every
        // location scene is always loaded additively, so neither distinguishes. If this name match ever goes
        // stale and the title-menu fallback claims the pause overlay, the player is not stranded: its
        // Continue button still resumes, and an unconsumed Escape is handed back to the game, which resumes.)
        public override bool AppliesNow()
        {
            Transform content = MenuContent();
            if (content == null) return false;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                Selectable selectable = child.GetComponent<Selectable>();
                if (selectable != null && selectable.interactable && child.gameObject.name == "Main Menu")
                    return true;
            }
            return false;
        }

        // Wrap the shared button list in a closeable root so Escape resumes the game, with the
        // learn-game-sounds entry slotted in above Options.
        public override Container BuildRoot(IModHost host)
        {
            var root = new ScreenRoot();
            root.Add(BuildList(host, new ModActionButton(
                () => Strings.ScreenLearnSounds,
                () => _toggleOverlay(new LearnSoundsScreen()))));
            return root;
        }
    }
}
