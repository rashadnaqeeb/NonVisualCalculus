using System;
using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine;
using Sunshine.Metric;
using Sunshine.Views;
using TMPro;
using Container = DiscoAccess.Core.UI.Nav.Container;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The inventory screen, mirroring the game window as Tab-stops the player walks with our own navigator
    /// (nothing on a hotkey - it is all reachable by navigation). In order: the equipment doll (a slot list,
    /// ending in the read-only keys and bullets slots), the category tabs (a vertical list whose Enter
    /// switches the storage grid), the active category's item list, and the left stats panel (attributes,
    /// vitals, bonuses - read-only). Each item carries its own effects and description, so there is no
    /// separate detail stop. Everything is read live from the game's own components (no caching); the
    /// equipment, tabs, and stats are stable for a screen, so they build once, while the item list belongs to
    /// the active tab and rebuilds in place when the tab switches or the item set changes (an equip/use),
    /// re-homing focus to the same grid position.
    ///
    /// Equipment and storage slots are both <see cref="UIDragDock"/>s told apart by <see cref="SlotNature"/>;
    /// a filled dock parents the item. The primary action (Enter) is the game's submit on a dock - equip,
    /// unequip, or use; the secondary (Backslash) is the item's interact action where the game offers one.
    /// </summary>
    public sealed class InventoryScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.INVENTORY;
        public override string ScreenName => Strings.ScreenInventory;

        // The doll's slots in reading order; docks come from the scene unordered, so this fixes their order.
        private static readonly string[] EquipOrder =
            { "hat", "jacket", "shirt", "pants", "glasses", "neck", "gloves", "shoes", "heldLeft", "heldRight" };

        private IModHost _host;
        private ScreenRoot _root;
        private Container _itemsList;       // rebuilt on tab switch / item-set change
        private InventoryView _view;        // a live component, read at announce time
        private int _builtSig;

        public override Container BuildRoot(IModHost host)
        {
            _host = host;
            _root = new ScreenRoot();
            _view = InventoryAdapter.View();
            _builtSig = -1;

            BuildEquipment();
            BuildTabs();

            _itemsList = new LiveLabelContainer(ContainerShape.VerticalList, InventoryAdapter.CurrentTabName);
            _root.Add(_itemsList);
            PopulateItems();

            BuildStats();
            return _root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (ItemSignature() == _builtSig)
                return false;

            // The active tab or the item set changed (a tab switch settling, or an equip/use from the list).
            // If the cursor was inside the item list, restore it to the same grid position in the rebuilt
            // list (clamped) - after an equip that lands the player on the item that shifted up into the slot,
            // not back at the top - and announce just that landing. Focus elsewhere (a tab, the doll) is left
            // be unless its cell was removed.
            int idx = FocusedItemIndex(nav);
            PopulateItems();
            if (idx >= 0)
            {
                UIElement target = NthFocusable(_itemsList, idx);
                if (target != null)
                {
                    nav.Focus(target, announce: true);
                    return false; // we announced the landing ourselves; don't let the manager re-announce
                }
            }
            return nav.EnsureFocusValid();
        }

        // The cursor's index among the item list's focusable cells, or -1 when focus is not in the list.
        private int FocusedItemIndex(TraditionalNavigator nav)
        {
            UIElement cur = nav.Current;
            if (cur == null || cur.Parent != _itemsList) return -1;
            int idx = 0;
            foreach (UIElement c in _itemsList.Children)
            {
                if (c == cur) return idx;
                if (c.CanFocus) idx++;
            }
            return -1;
        }

        // The nth focusable child of a container, clamped to the last focusable when n overshoots (the list
        // shrank). Null when the container has no focusable child.
        private static UIElement NthFocusable(Container c, int n)
        {
            foreach (UIElement child in c.Children)
            {
                if (!child.CanFocus) continue;
                if (n == 0) return child;
                n--;
            }
            return c.LastFocusable();
        }

        private void BuildEquipment()
        {
            var docks = InventoryAdapter.Docks(SlotNature.EQUIPMENT, activeOnly: true);
            docks.AddRange(InventoryAdapter.Docks(SlotNature.HELD, activeOnly: true));
            docks.Sort((a, b) => OrderIndex(a.name).CompareTo(OrderIndex(b.name)));

            var list = new Container(ContainerShape.VerticalList, Strings.InventoryEquipmentLabel);
            foreach (UIDragDock dock in docks)
            {
                TMP_Text caption = InventoryAdapter.FindCaption(dock);
                list.Add(new EquipmentSlotCell(dock, caption, _host));
            }

            // The keys and bullets display slots are not equipment docks (read-only, no equip), so they are
            // added as plain read-outs of their game captions at the end of the doll.
            list.Add(new ReadonlyTextCell(InventoryAdapter.KeysText));
            list.Add(new ReadonlyTextCell(InventoryAdapter.BulletsText));

            if (list.Children.Count == 0)
                _host.LogWarning("InventoryScreen: no equipment docks found; the doll will be unreachable.");
            _root.Add(list);
        }

        private static int OrderIndex(string name)
        {
            int i = Array.IndexOf(EquipOrder, name);
            return i < 0 ? EquipOrder.Length : i;
        }

        private void BuildTabs()
        {
            InventoryTabPanel tp = InventoryTabPanel.Singleton;
            if (tp == null)
            {
                _host.LogWarning("InventoryScreen: InventoryTabPanel.Singleton is null; tabs unreachable.");
                return;
            }

            var list = new Container(ContainerShape.VerticalList, Strings.InventoryTabsLabel);
            foreach (ItemTabGroup g in Enum.GetValues(typeof(ItemTabGroup)))
            {
                InventoryTabButton button = null;
                if (tp.inventoryTabButtons != null && tp.inventoryTabButtons.TryGetValue(g, out button) && button != null)
                    list.Add(new InventoryTabCell(g, button));
            }
            if (list.Children.Count == 0)
                _host.LogWarning("InventoryScreen: no tab buttons found; categories unreachable.");
            _root.Add(list);
        }

        // The active tab's filled storage slots, in grid order. Empty slots are skipped (the player browses
        // items, not the grid's gaps).
        private void PopulateItems()
        {
            _itemsList.Clear();
            _builtSig = ItemSignature();

            var docks = InventoryAdapter.Docks(SlotNature.INVENTORY, activeOnly: true);
            docks.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            int added = 0;
            foreach (UIDragDock dock in docks)
                if (InventoryAdapter.ItemInDock(dock) != null)
                {
                    _itemsList.Add(new InventoryItemCell(dock, _host));
                    added++;
                }
            // Keep the item list a reachable Tab-stop even when the category is empty, announcing "no items"
            // rather than silently vanishing from the tab order.
            if (added == 0)
                _itemsList.Add(new ReadonlyTextCell(() => Strings.InventoryNoItems));
        }

        // The active tab plus the count of filled storage slots: a tab switch changes the tab, an equip/use
        // changes the count, either driving the item list's in-place rebuild.
        private int ItemSignature()
        {
            InventoryTabPanel tp = InventoryTabPanel.Singleton;
            int tab = tp != null ? (int)tp.CurrentItemTabGroup : -1;
            int count = 0;
            foreach (UIDragDock dock in InventoryAdapter.Docks(SlotNature.INVENTORY, activeOnly: true))
                if (InventoryAdapter.ItemInDock(dock) != null)
                    count++;
            return tab * 1000 + count;
        }

        private void BuildStats()
        {
            if (_view == null)
            {
                _host.LogWarning("InventoryScreen: InventoryView not found; stats panel unreachable.");
                return;
            }
            InventoryView iv = _view;
            var list = new Container(ContainerShape.VerticalList, Strings.InventoryStatsLabel);
            list.Add(new ReadonlyTextCell(() => InventoryAdapter.Attributes(iv)));
            list.Add(new ReadonlyTextCell(() => InventoryAdapter.Vitals(iv)));
            list.Add(new ReadonlyTextCell(() => InventoryAdapter.Bonuses(iv)));
            _root.Add(list);
        }
    }
}
