using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The main menu as a single vertical list of its sidebar buttons (Continue, New Game, Load Game,
    /// Collage, Options, Quit). The buttons are the active, interactable Selectable children of the
    /// menu's content container, in sibling (visual) order; hidden entries (Quick Save, Save Game while
    /// at the menu) are inactive and skipped. The container is located from the live selection, which the
    /// game still holds at the menu when focus mode engages.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.MAINMENU;
        public override string ScreenName => Strings.ScreenMainMenu;

        public override Container BuildRoot(IModHost host)
        {
            // The list itself is the root; its label would just echo the screen name, so it is unlabeled
            // and the spoken screen name carries the context.
            var list = new Container(ContainerShape.VerticalList);

            Selectable start = CurrentSelectable();
            if (start == null)
            {
                host.LogWarning("MainMenuScreen: no live selection to locate the menu; list is empty.");
                return list;
            }

            var parent = start.transform.parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var selectable = child.GetComponent<Selectable>();
                if (selectable == null || !selectable.interactable) continue;
                list.Add(new SelectableButton(selectable));
            }
            return list;
        }

        // The game's current selection: NavigationManager (it keeps the menu selection even after we
        // disable it), falling back to the EventSystem ground truth.
        private static Selectable CurrentSelectable()
        {
            var nav = NavigationManager.Singleton;
            Selectable sel = nav != null ? nav.GetCurrentSelectedSelectable() : null;
            if (sel != null) return sel;
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            return go != null ? go.GetComponent<Selectable>() : null;
        }
    }
}
