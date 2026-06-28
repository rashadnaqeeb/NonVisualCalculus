using System;
using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using Sunshine.Views;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Adapter: reports the screen the player is on as an authored name, read from the live Unity
    /// <see cref="ViewType"/> on <see cref="ViewsPagesBridge"/>. DE exposes screens only as that enum
    /// with no localized title, so the names live in the Core strings table and this maps the enum to
    /// one. Every player-facing view is named; <see cref="Silenced"/> lists the views deliberately not
    /// announced, and <see cref="UnmappedScreens"/> reports any view that is in neither table so a
    /// missed (or game-update-added) screen surfaces in the log instead of going silently unannounced.
    /// </summary>
    public static class ScreenAdapter
    {
        private static readonly Dictionary<ViewType, string> Names = new Dictionary<ViewType, string>
        {
            { ViewType.LOBBY, Strings.ScreenWorld },
            { ViewType.INVENTORY, Strings.ScreenInventory },
            { ViewType.INVENTORY_PAWN, Strings.ScreenClothing },
            { ViewType.THOUGHTCABINET, Strings.ScreenThoughtCabinet },
            { ViewType.JOURNAL, Strings.ScreenJournal },
            { ViewType.CHARACTERSHEET, Strings.ScreenCharacterSheet },
            { ViewType.ARCHETYPE_SELECTION, Strings.ScreenArchetypeSelection },
            { ViewType.CHARACTER_CREATION_ADJUST_ABILITIES, Strings.ScreenAdjustAbilities },
            { ViewType.CHARACTER_CREATION_SET_SKILL, Strings.ScreenSignatureSkill },
            { ViewType.OPTIONS, Strings.ScreenOptions },
            { ViewType.SAVE, Strings.ScreenSave },
            { ViewType.LOAD, Strings.ScreenLoad },
            { ViewType.MAINMENU, Strings.ScreenMainMenu },
            { ViewType.HELPOVERLAY, Strings.ScreenHelp },
            { ViewType.THOUGHTSPLASHSCREEN, Strings.ScreenThought },
            { ViewType.COLLAGEMODE, Strings.ScreenCollage },
        };

        // Views deliberately left silent: DIALOGUE has its own reader, CUTSCENE is passive playback,
        // and TEST/CLEAR/SPECIAL are internal transition states with no screen a player lands on
        // (announcing any of them would be noise).
        private static readonly HashSet<ViewType> Silenced = new HashSet<ViewType>
        {
            ViewType.DIALOGUE, ViewType.CUTSCENE, ViewType.TEST, ViewType.CLEAR, ViewType.SPECIAL,
        };

        /// <summary>
        /// Views that are neither named nor silenced, so they would slip through unannounced. Empty
        /// in a correct build; the host logs any at load (a game update adding a ViewType lands here).
        /// </summary>
        public static IEnumerable<ViewType> UnmappedScreens()
        {
            foreach (ViewType view in Enum.GetValues(typeof(ViewType)))
                if (!Names.ContainsKey(view) && !Silenced.Contains(view))
                    yield return view;
        }
    }
}
