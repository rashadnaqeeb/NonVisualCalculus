using System.Collections.Generic;
using DiscoAccess.Core.UI.Nav;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The popup's root list. Advertises Back so Escape dismisses the popup the game's own way: it invokes
    /// the Cancel button when one is shown (the popup's own Escape closes via Cancel), falling back to
    /// Confirm for a popup with no Cancel (an error with a single OK). A vertical list so Up/Down move
    /// between the body line and the buttons.
    /// </summary>
    public sealed class PopupRoot : Container
    {
        private readonly ConfirmationController _popup;

        public PopupRoot(ConfirmationController popup) : base(ContainerShape.VerticalList) => _popup = popup;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Close);
        }

        private void Close()
        {
            Button cancel = _popup.Cancel;
            Button button = cancel != null && cancel.isActiveAndEnabled && cancel.interactable ? cancel : _popup.Confirm;
            button.onClick.Invoke();
        }
    }
}
