using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;
using Sunshine;
using Sunshine.Metric;
using Sunshine.Views;
using Container = DiscoAccess.Core.UI.Nav.Container;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The pawnshop sell view (INVENTORY_PAWN), opened from the pawnbroker's dialogue: the game re-dresses
    /// the inventory window as one grid of the player's pawnable items, with the equipment doll, tabs, and
    /// stats column hidden. The tree is the item list, a wallet readout, and the game's own close button;
    /// Back closes through that button, dropping back into the dialogue (the game re-selects the first
    /// response itself). Enter on an item sells it through the shared tooltip's priced PAWN button - the
    /// game's submit on a pawn dock does nothing, so the button IS the primary action here - and the
    /// money-gained announcement then arrives through the NotificationReader. A sale deletes the item and
    /// the game repacks the grid, so the item list rebuilds in place and re-homes focus, like the
    /// inventory after an equip.
    /// </summary>
    internal sealed class PawnshopScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.INVENTORY_PAWN;
        public override string ScreenName => Strings.ScreenPawnshop;

        private IModHost _host;
        private Container _itemsList; // rebuilt when a sale changes the item set
        private int _builtCount;

        public override Container BuildRoot(IModHost host)
        {
            _host = host;
            var root = new PawnshopRoot();

            _itemsList = new Container(ContainerShape.VerticalList, Strings.InventoryItemsLabel);
            root.Add(_itemsList);
            PopulateItems();

            root.Add(new ReadonlyTextCell(() => Strings.PawnshopMoney(PlayerCharacter.Singleton.Money)));

            InventoryTooltip tooltip = InventoryAdapter.Tooltip();
            if (tooltip != null && tooltip.closeButton != null)
                root.Add(new ClickButton(tooltip.closeButton));
            else
                _host.LogWarning("PawnshopScreen: no tooltip close button; closing is Escape only.");
            return root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (FilledDocks().Count == _builtCount)
                return false;

            // A sale went through: the game deleted the item and repacked the grid. Rebuild the list and,
            // if the cursor was inside it, land on the same position (clamped), announcing just that
            // landing - the player hears the next item to consider selling, not the top of the list. The
            // landing queues (no interrupt) so it does not cut off the money-gained line the sale just
            // spoke.
            int idx = FocusedItemIndex(nav);
            PopulateItems();
            if (idx >= 0)
            {
                UIElement target = NthFocusable(_itemsList, idx);
                if (target != null)
                {
                    nav.Focus(target, announce: true, interrupt: false);
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

        // The nth focusable child of a container, clamped to the last focusable when n overshoots (the
        // list shrank). Null when the container has no focusable child.
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

        // The filled pawn docks in grid order. While this view is up, the pawn grid's docks are the only
        // active INVENTORY-nature docks (the regular storage UI is inactive), so no further filter is
        // needed. Empty slots are skipped (the player browses items, not the grid's gaps).
        private static List<UIDragDock> FilledDocks()
        {
            var docks = InventoryAdapter.Docks(SlotNature.INVENTORY, activeOnly: true);
            docks.RemoveAll(d => InventoryAdapter.ItemInDock(d) == null);
            docks.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return docks;
        }

        private void PopulateItems()
        {
            _itemsList.Clear();
            List<UIDragDock> docks = FilledDocks();
            _builtCount = docks.Count;
            foreach (UIDragDock dock in docks)
                _itemsList.Add(new PawnItemCell(dock, _host));
            // Keep the item list a reachable Tab-stop even when everything is sold, announcing "no items"
            // rather than silently vanishing from the tab order.
            if (docks.Count == 0)
                _itemsList.Add(new ReadonlyTextCell(() => Strings.InventoryNoItems));
        }
    }

    /// <summary>The pawnshop's root. Back (Escape) closes through the game's own close button (the
    /// "Pawnshop Close Button"), so its wired teardown and the return to the dialogue match a sighted
    /// click; the generic <see cref="ScreenRoot"/> view close bypasses that path.</summary>
    internal sealed class PawnshopRoot : Container
    {
        public PawnshopRoot() : base(ContainerShape.Panel) { }

        public override IEnumerable<ElementAction> GetActions()
        {
            var close = InventoryAdapter.Tooltip()?.closeButton;
            if (close != null)
                yield return new ElementAction(ActionIds.Back, () => close.onClick.Invoke());
        }
    }

    /// <summary>
    /// One pawnable item, wrapping its live <see cref="UIDragDock"/>. Reads name, markers, the "Pawn for"
    /// price, effects, and description live off the item model. On focus it makes the dock the game's
    /// selection, which primes the shared tooltip's priced PAWN button to this item; Enter sells through
    /// <see cref="InventoryCommit.Sell"/>, which re-primes that button to this dock's item before
    /// clicking (the game's post-sale re-show primes the first grid item, off our focus).
    /// </summary>
    internal sealed class PawnItemCell : UIElement
    {
        private readonly UIDragDock _dock;
        private readonly IModHost _host;

        public PawnItemCell(UIDragDock dock, IModHost host)
        {
            _dock = dock;
            _host = host;
        }

        // The dock empties under us when its item is sold; drop out of nav then so focus re-homes.
        public override bool CanFocus
            => _dock != null && _dock.gameObject.activeInHierarchy && InventoryAdapter.ItemInDock(_dock) != null;

        public override string GetFocusText()
        {
            var item = InventoryAdapter.ItemInDock(_dock);
            if (item == null) return string.Empty;
            return InventoryItemAnnouncer.Compose(InventoryAdapter.ReadPawnable(item));
        }

        public override void OnFocused() => GameCursor.Follow(InventoryAdapter.Selectable(_dock));

        public override IEnumerable<ElementAction> GetActions()
        {
            if (InventoryAdapter.ItemInDock(_dock) == null) yield break;
            yield return new ElementAction(ActionIds.Activate, () => InventoryCommit.Sell(_dock, _host));
        }
    }
}
