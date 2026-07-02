using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;
using Sunshine;
using TMPro;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// One slot of the equipment doll (hat, jacket, held left, ...), wrapping its live
    /// <see cref="UIDragDock"/>. Reads the slot's caption from the game's own label and the docked item's
    /// name live (never cached), announcing "caption, item" or "caption, empty". On focus it makes the dock
    /// the game's selection so the doll highlights. Enter unequips or uses the slot (the game's submit);
    /// Backspace runs the item's interact action when the game offers one.
    /// </summary>
    internal sealed class EquipmentSlotCell : UIElement
    {
        private readonly UIDragDock _dock;
        private readonly TMP_Text _caption; // a live game label, read at announce time; null -> authored fallback
        private readonly IModHost _host;

        public EquipmentSlotCell(UIDragDock dock, TMP_Text caption, IModHost host)
        {
            _dock = dock;
            _caption = caption;
            _host = host;
        }

        public override bool CanFocus => _dock != null && _dock.gameObject.activeInHierarchy;

        public override string GetFocusText()
        {
            string caption = _caption != null ? TextFilter.Clean(_caption.text) : null;
            if (string.IsNullOrEmpty(caption)) caption = Strings.EquipmentSlotName(_dock.name);
            var item = InventoryAdapter.ItemInDock(_dock);
            // A filled slot reads the item and what it does (effects, description); an empty slot just says so.
            string contents = item != null
                ? InventoryItemAnnouncer.Compose(InventoryAdapter.ReadItem(item))
                : Strings.InventorySlotEmpty;
            return caption + ", " + contents;
        }

        public override void OnFocused() => GameCursor.Follow(InventoryAdapter.Selectable(_dock));

        public override IEnumerable<ElementAction> GetActions()
        {
            // Only when the slot holds something: an empty slot has nothing to unequip or interact with.
            if (InventoryAdapter.ItemInDock(_dock) == null) yield break;
            yield return new ElementAction(ActionIds.Activate, () => InventoryCommit.Primary(_dock));
            if (InventoryCommit.HasSecondary())
                yield return new ElementAction(ActionIds.Secondary, () => InventoryCommit.Secondary(_dock, _host));
        }
    }
}
