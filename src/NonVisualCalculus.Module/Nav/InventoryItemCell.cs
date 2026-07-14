using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// One item in the active category's storage grid, wrapping its live <see cref="UIDragDock"/>. Reads the
    /// item's name and brief status (new, uses, value) live through <see cref="InventoryAdapter"/> and the
    /// Core <see cref="InventoryItemAnnouncer"/>; the long description and equip effects are read on the
    /// detail tab-stop, not here. On focus it makes the dock the game's selection so the grid highlights and
    /// the tooltip primes to this item. Enter equips or uses it (the game's submit); Backspace runs its
    /// interact action when the game offers one.
    /// </summary>
    internal sealed class InventoryItemCell : UIElement
    {
        private readonly UIDragDock _dock;
        private readonly IModHost _host;

        public InventoryItemCell(UIDragDock dock, IModHost host)
        {
            _dock = dock;
            _host = host;
        }

        // Only filled docks are listed (the screen builds a cell per docked item), but the dock can empty
        // under us when its item is equipped away; drop out of nav then so focus re-homes.
        public override bool CanFocus
            => _dock != null && _dock.gameObject.activeInHierarchy && InventoryAdapter.ItemInDock(_dock) != null;

        public override string GetFocusText()
        {
            var item = InventoryAdapter.ItemInDock(_dock);
            if (item == null) return string.Empty;
            return InventoryItemAnnouncer.Compose(InventoryAdapter.ReadItem(item));
        }

        public override void OnFocused() => GameCursor.Follow(InventoryAdapter.Selectable(_dock));

        public override IEnumerable<ElementAction> GetActions()
        {
            if (InventoryAdapter.ItemInDock(_dock) == null) yield break;
            yield return new ElementAction(ActionIds.Activate, () => InventoryCommit.Primary(_dock));
            if (InventoryCommit.HasSecondary())
                yield return new ElementAction(ActionIds.Secondary, () => InventoryCommit.Secondary(_dock, _host));
        }
    }
}
