using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The title main menu as a single vertical list of its sidebar buttons (Continue, New Game, Load
    /// Game, Collage, Options, Quit). The buttons are the active, interactable Selectable children of the
    /// menu's content container, in sibling (visual) order; hidden entries (Quick Save, Save Game, the
    /// return-to-title Main Menu button) are inactive and skipped. The container is located by scanning
    /// for the menu's own buttons (their shared parent), not the game's live selection: the selection is
    /// absent when the menu opens mid-save (the pause view captures a save thumbnail as it opens, and an
    /// Escape landing in that hitch opens the menu before the game assigns a selection), and keying off it
    /// there left us reading an empty list while owning the keyboard, a soft-lock. The game reuses this
    /// view for the in-game pause menu too; <see cref="PauseMenuScreen"/> handles that case, so this is
    /// the fallback for the title screen and is not sealed.
    /// </summary>
    public class MainMenuScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.MAINMENU;
        public override string ScreenName => Strings.ScreenMainMenu;

        // The title menu's root is the bare list with NO Back: at the title screen Escape does nothing in
        // vanilla, so we must not wire it to the view's CloseOnEscapeKey (which collapses the menu). An
        // Escape here is left unconsumed; the pump then hands the keyboard back so the game's own (no-op at
        // the title) Escape runs. The pause menu, which DOES resume on Escape, adds its own closeable root.
        public override Container BuildRoot(IModHost host) => BuildList(host);

        /// <summary>The vertical list of the menu's active, interactable buttons in visual order. Shared
        /// with <see cref="PauseMenuScreen"/>, which wraps it in a closeable root and slots its own
        /// <paramref name="beforeOptions"/> entry in just above the Options button (appended last, with a
        /// warning, should the game's menu ever stop carrying one).</summary>
        protected static Container BuildList(IModHost host, UIElement beforeOptions = null)
        {
            var list = new Container(ContainerShape.VerticalList);

            Transform parent = MenuContent();
            if (parent == null)
            {
                host.LogWarning("MainMenuScreen: no live selection to locate the menu; list is empty.");
                return list;
            }

            bool inserted = false;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var selectable = child.GetComponent<Selectable>();
                if (selectable == null || !selectable.interactable) continue;
                // Collage opens DE's screenshot composition canvas, a visual screen with no accessible
                // path, so it is the one button we navigate to but refuse to open. A menu button knows the
                // view it opens via RelatedViewType; the Collage entry's is COLLAGEMODE.
                var menuButton = child.GetComponent<MainMenuButton>();
                if (!inserted && beforeOptions != null
                    && menuButton != null && menuButton.RelatedViewType == ViewType.OPTIONS)
                {
                    list.Add(beforeOptions);
                    inserted = true;
                }
                if (menuButton != null && menuButton.RelatedViewType == ViewType.COLLAGEMODE)
                    list.Add(new BlockedButton(selectable, host, Strings.CollageInaccessible));
                else
                    list.Add(new SelectableButton(selectable));
            }
            if (beforeOptions != null && !inserted)
            {
                host.LogWarning("MainMenuScreen: no Options button to anchor the extra entry; appending it last.");
                list.Add(beforeOptions);
            }
            return list;
        }

        /// <summary>The menu's button container, located by scanning for the menu's own buttons and
        /// taking their shared parent, or null when the menu is not present. Independent of the game's
        /// live selection, which is absent when the menu opens mid-save.</summary>
        protected static Transform MenuContent()
        {
            foreach (MainMenuButton button in Resources.FindObjectsOfTypeAll<MainMenuButton>())
                if (button.gameObject.activeInHierarchy)
                    return button.transform.parent;
            return null;
        }
    }
}
