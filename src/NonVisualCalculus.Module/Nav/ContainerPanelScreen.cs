using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using PixelCrushers.DialogueSystem;
using Sunshine;
using Sunshine.Metric;
using Sunshine.Views;
using Voidforge;
// The nav tree's Container (the game's Sunshine.Container, the loot panel itself, stays fully qualified).
using Container = NonVisualCalculus.Core.UI.Nav.Container;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The world's loot panel (<see cref="Sunshine.Container"/>), the in-scene popup an unlocked container
    /// opens with one image button per item plus Take all and Close. It has no ViewType of its own - the
    /// view stays CLEAR while it floats over free-roam - so this screen registers for CLEAR and applies
    /// only while the panel is up; the ScreenManager then takes the keyboard from the world reader and
    /// hands it back when the panel closes (announced by <see cref="ContainerReader"/>, which also covers
    /// the game closing it on its own when the player walks out of range). The tree is a flat list: the
    /// items still inside, then the game's own Take all and Close buttons; Escape closes through the game.
    /// </summary>
    internal sealed class ContainerPanelScreen : Screen
    {
        public override ViewType ViewType => ViewType.CLEAR;
        public override bool AppliesNow() => IsShowing();
        public override string ScreenName => Strings.ScreenContainer;

        /// <summary>Whether the loot panel is up: the singleton exists, is active, and is bound to a
        /// source. Hide clears the source, so the pooled-but-idle panel never counts.</summary>
        public static bool IsShowing()
        {
            Sunshine.Container panel = Panel();
            return panel != null && panel.gameObject.activeInHierarchy && panel.Source != null;
        }

        private static Sunshine.Container Panel() => SingletonComponent<Sunshine.Container>.Singleton;

        public override Container BuildRoot(IModHost host)
        {
            Sunshine.Container panel = Panel();
            var root = new Container(ContainerShape.VerticalList);
            var items = panel.Source.containedItems;
            for (int i = 0; i < items.Count; i++)
                if (ContainerLootCell.IsShown(items[i]))
                    root.Add(new ContainerLootCell(panel, items[i]));
            root.Add(new ClickButton(panel.takeAllButton));
            root.Add(new ClickButton(panel.closeButton));
            return root;
        }

        // Taking an item removes it from the container under the focused cell, whose CanFocus then drops;
        // move to the nearest remaining sibling (the next item, or Take all when the list ran out) so the
        // player is never parked on a dead cell. The manager announces the landing (returns true). Taking
        // the LAST item never reaches here - the panel closes, this screen stops applying, and the world
        // reader resumes.
        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            UIElement cur = nav.Current;
            if (!(cur is ContainerLootCell) || cur.CanFocus) return false;
            Container root = cur.Parent;
            if (root == null) return false;
            UIElement next = root.GetNeighbor(cur, NavDirection.Down) ?? root.GetNeighbor(cur, NavDirection.Up);
            if (next == null) return false;
            nav.Focus(next, announce: false);
            return true;
        }
    }

    /// <summary>
    /// One item still inside the container. The game's buttons are icon-only (no caption to read), so the
    /// spoken line is composed from the item model instead: the money entry reads its amount, anything else
    /// reads like an inventory item (localized name, pawn value, effects, description) via the library
    /// prototype its internal name maps to. Activation runs the game's own take
    /// (<see cref="Sunshine.Container.TakeItemFromContainer"/>, the same call the button's click handler
    /// makes), so the received notification and the panel's own bookkeeping follow; the pickup announcement
    /// then arrives through the NotificationReader like any other received item.
    /// </summary>
    internal sealed class ContainerLootCell : UIElement
    {
        // The game's internal name for a container's money entry (Sunshine.Container's MONEY_NAME).
        private const string MoneyName = "money";

        private readonly Sunshine.Container _panel;
        private readonly ContainerItem _item;

        public ContainerLootCell(Sunshine.Container panel, ContainerItem item)
        {
            _panel = panel;
            _item = item;
        }

        /// <summary>The game's own visibility filter for a contained item (SetItems builds no button for
        /// an item already picked up - its Lua inInventory flag - or whose alterant condition fails), so
        /// the list matches what a sighted player sees.</summary>
        public static bool IsShown(ContainerItem item)
            => !DialogueLua.GetItemField(item.name, "inInventory").AsBool && Alterant.ItemConditionMet(item.name);

        // Drops out once taken (the game removes it from containedItems); compared by native pointer, as
        // the interop hands back a fresh wrapper per list read.
        public override bool CanFocus
        {
            get
            {
                ContainerSource source = _panel.Source;
                if (source == null) return false;
                var items = source.containedItems;
                if (items == null) return false;
                for (int i = 0; i < items.Count; i++)
                    if (items[i] != null && items[i].Pointer == _item.Pointer) return true;
                return false;
            }
        }

        public override string GetFocusText()
        {
            if (_item.name == MoneyName)
                return Strings.WorldMoney(MoneyAmount());
            InventoryItemList library = SingletonComponent<InventoryItemList>.Singleton;
            InventoryItem prototype = library != null ? library.GetByName(_item.name) : null;
            // No library entry: speak the internal name rather than nothing (never a silent cell).
            if (prototype == null) return _item.name;
            return InventoryItemAnnouncer.Compose(InventoryAdapter.ReadLoot(prototype));
        }

        // The amount in centims: the rolled value (SetItems fixed calculatedValue from value±deviation when
        // it built the panel) scaled by the game mode's multiplier, exactly the figure the panel displays
        // and OnPickupMoney grants.
        private int MoneyAmount()
            => (int)(GameModeController.ContainerMoneyMultiplayer * _item.calculatedValue);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Take);
        }

        // The game's own take call, with no button rect to hide: the panel's bookkeeping (model removal,
        // pickup grant, closing after the last item) all runs inside it, and Take all passes null the same
        // way. The stale image left visible until the panel's delayed refresh is invisible to speech.
        private void Take() => _panel.TakeItemFromContainer(_item, null);
    }
}
