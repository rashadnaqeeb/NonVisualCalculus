using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.Text;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// One category tab (TOOLS, CLOTHES, ITEMS, INTERACT), wrapping the game's live
    /// <see cref="InventoryTabButton"/>. Reads its label from the game's own button text and marks the
    /// current tab "active". Enter switches the storage grid to this category by clicking the game's own tab
    /// button (its full path: background tween, scroll resize), after which the screen refills the item list
    /// tab-stop. The active mark is re-announced on activation so the switch is confirmed.
    /// </summary>
    internal sealed class InventoryTabCell : UIElement
    {
        private readonly ItemTabGroup _group;
        private readonly InventoryTabButton _button;

        public InventoryTabCell(ItemTabGroup group, InventoryTabButton button)
        {
            _group = group;
            _button = button;
        }

        public override bool CanFocus
            => _button != null && _button.TabButton != null && _button.TabButton.isActiveAndEnabled;

        public override string Label
            => _button.ButtonText != null ? TextFilter.Clean(GameLocalization.Spoken(_button.ButtonText)) : _group.ToString();

        public override string Role => Strings.RoleTab;

        // The current tab reads "selected" (the mod-standard status word); the others have no value. Re-read
        // after activation (below) to confirm the switch.
        public override string Value
            => InventoryTabPanel.Singleton != null && InventoryTabPanel.Singleton.CurrentItemTabGroup == _group
                ? Strings.StatusSelected : null;

        public override bool ReannounceOnActivate => true;

        public override void OnFocused() => GameCursor.Follow(_button.TabButton);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, () => _button.TabButton.onClick.Invoke());
        }
    }
}
