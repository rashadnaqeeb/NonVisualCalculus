using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A game button in a UI that runs its own navigation group separate from NavigationManager and sets
    /// no EventSystem selection (the confirmation popup's Confirm/Cancel, the loot panel's Take all/Close).
    /// Activation invokes the button's own onClick (the handler the game wired) directly, rather than going
    /// through NavigationManager.Submit, which targets the menu group these buttons are not part of. Label
    /// read live from the button's caption, never cached.
    /// </summary>
    internal sealed class ClickButton : UIElement
    {
        private readonly Button _button;

        public ClickButton(Button button) => _button = button;

        // Focusable only while shown and enabled: a popup with no Cancel hides that button, so it drops out.
        public override bool CanFocus => _button != null && _button.isActiveAndEnabled && _button.interactable;
        public override string Label => FocusReader.Read(_button);
        public override string Role => Strings.RoleButton;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, () => _button.onClick.Invoke());
        }
    }
}
