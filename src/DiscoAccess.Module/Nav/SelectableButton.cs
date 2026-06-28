using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A navigable leaf wrapping a live game <see cref="Selectable"/> (a menu button). Holds the Unity
    /// reference and reads its label and interactable state at announce time (never cached), per the
    /// house rule. Activation runs the game's own submit path so sounds and view transitions happen:
    /// select it in NavigationManager, then Submit.
    /// </summary>
    public sealed class SelectableButton : UIElement
    {
        private readonly Selectable _selectable;

        public SelectableButton(Selectable selectable) => _selectable = selectable;

        // Focusable only while shown and enabled; a button greyed out mid-screen drops out of nav.
        public override bool CanFocus => _selectable != null && _selectable.isActiveAndEnabled && _selectable.interactable;

        public override string Label => FocusReader.Read(_selectable);
        public override string Role => Strings.RoleButton;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Activate);
        }

        private void Activate()
        {
            // Make this the game's current selection, then run its submit handler (the Enter-key path):
            // it resolves the selection itself and runs DE's full activation (sound, button state, view
            // transition) that a hand-rolled uGUI submit would bypass. NavigationManager is never null
            // while a navigable screen is up, so it is not defensively checked - a null would surface as
            // a logged crash in the pump rather than a silently dropped activation.
            var nav = NavigationManager.Singleton;
            nav.Select(_selectable);
            nav.Submit();
        }
    }
}
