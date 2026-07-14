using Sunshine.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonVisualCalculus.Dev
{
    /// <summary>
    /// Injects logical UI navigation by driving DE's own focus system, NOT OS synthetic keys: we drive
    /// the game while its window is unfocused, where SendInput (needs foreground) and PostMessage (won't
    /// reach the game's raw input) don't work. Directional moves walk the focused Selectable's uGUI
    /// navigation links and hand the result to NavigationManager.Select, which is what the focus pump
    /// polls - so the move gets spoken. Confirm calls NavigationManager.Submit(), the game's own submit
    /// handler (what the Enter key triggers); it resolves the current selection itself and runs DE's
    /// full activation path (sounds, view transitions, button state), which a hand-rolled uGUI submit
    /// event would bypass. Back calls the active Sunshine View's CloseOnEscapeKey() (what the Escape
    /// key triggers for that view), so leaving a menu runs the game's own close path.
    ///
    /// Current selection is read from NavigationManager, falling back to the EventSystem ground truth -
    /// at a freshly loaded menu the EventSystem has a selection a beat before NavigationManager records
    /// one, and without the fallback input would no-op during that window.
    /// </summary>
    internal static class InputInjector
    {
        public static string Inject(string verb)
        {
            string v = (verb ?? "").Trim().ToLowerInvariant();
            var nav = NavigationManager.Singleton;
            if (nav == null)
                return "[no NavigationManager] not on a navigable screen\n";

            switch (v)
            {
                case "up":
                    return Move(nav, "up");
                case "down":
                    return Move(nav, "down");
                case "left":
                    return Move(nav, "left");
                case "right":
                    return Move(nav, "right");
                case "confirm":
                case "enter":
                case "ok":
                    return Confirm(nav);
                case "back":
                case "escape":
                case "cancel":
                    return Back();
                default:
                    return "[unknown verb] '" + verb + "' - up|down|left|right|confirm|back\n";
            }
        }

        private static string Move(NavigationManager nav, string dir)
        {
            Selectable current = CurrentSelectable(nav);
            if (current == null)
                return dir + ": nothing selected\n";

            Selectable target;
            switch (dir)
            {
                case "up":
                    target = current.FindSelectableOnUp();
                    break;
                case "down":
                    target = current.FindSelectableOnDown();
                    break;
                case "left":
                    target = current.FindSelectableOnLeft();
                    break;
                default:
                    target = current.FindSelectableOnRight();
                    break;
            }

            if (target == null)
                return dir + ": no selectable that way\n";

            nav.Select(target);
            return dir + " -> " + target.name + "\n";
        }

        private static string Confirm(NavigationManager nav)
        {
            GameObject go = CurrentGameObject(nav);
            nav.Submit();
            return "confirm -> Submit()" + (go != null ? " on " + go.name : " (no selection)") + "\n";
        }

        // Leave the current Sunshine View the way Escape does: each View registers its own
        // CloseOnEscapeKey handler. Covers menu/options screens; in-game Pages use a separate path.
        private static string Back()
        {
            ViewType current = ViewsPagesBridge.Current;
            foreach (View v in Object.FindObjectsOfType<View>())
            {
                if (v.GetViewType() == current)
                {
                    v.CloseOnEscapeKey();
                    return "back -> CloseOnEscapeKey on " + current + "\n";
                }
            }
            return "back: no active view for " + current + "\n";
        }

        private static Selectable CurrentSelectable(NavigationManager nav)
        {
            Selectable sel = nav.GetCurrentSelectedSelectable();
            if (sel != null)
                return sel;
            GameObject go = EventSystemSelection();
            return go != null ? go.GetComponent<Selectable>() : null;
        }

        private static GameObject CurrentGameObject(NavigationManager nav)
        {
            Selectable sel = nav.GetCurrentSelectedSelectable();
            if (sel != null)
                return sel.gameObject;
            return EventSystemSelection();
        }

        private static GameObject EventSystemSelection()
        {
            EventSystem es = EventSystem.current;
            return es != null ? es.currentSelectedGameObject : null;
        }
    }
}
