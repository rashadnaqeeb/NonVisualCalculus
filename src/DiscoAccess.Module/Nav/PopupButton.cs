using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A popup's Confirm or Cancel button. The popup runs its own navigation group separate from
    /// NavigationManager and sets no EventSystem selection, so activation invokes the button's own onClick
    /// (the handler ConfirmationController wired: run the action, then close) directly, rather than going
    /// through NavigationManager.Submit, which targets the menu group the popup is not part of. Label read
    /// live from the button's caption, never cached.
    /// </summary>
    public sealed class PopupButton : UIElement
    {
        private readonly Button _button;

        public PopupButton(Button button) => _button = button;

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
