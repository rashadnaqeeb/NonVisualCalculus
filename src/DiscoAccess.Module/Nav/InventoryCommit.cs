using DiscoAccess.Core.Modularity;
using Sunshine;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// Runs the inventory's actions the game's own way. The primary action is the controller submit on a
    /// dock - equip, unequip, or use, whichever the game does for that slot and item - reached by making the
    /// dock the game's selection and running its submit handler (the path <see cref="SelectableButton"/>
    /// uses). The secondary action is the item's contextual interact button on the shared
    /// <see cref="InventoryTooltip"/> (READ, EAT, and the like), primed by selecting the dock first, the way
    /// <see cref="ThoughtCommit"/> drives the thought-cabinet tooltip. We drive the game's handlers rather
    /// than poke the model so its sound, animation, and list refresh all run.
    /// </summary>
    internal static class InventoryCommit
    {
        // Equip / unequip / use: the game's submit on the dock.
        public static void Primary(UIDragDock dock)
        {
            Selectable sel = InventoryAdapter.Selectable(dock);
            NavigationManager nav = NavigationManager.Singleton;
            nav.Select(sel);
            nav.Submit();
        }

        // Whether the focused item has a contextual interact action right now. The tooltip reflects the
        // focused (primed) item, so this is read for the dock the player is on.
        public static bool HasSecondary()
        {
            Button ib = InventoryAdapter.Tooltip()?.interactButton;
            return ib != null && ib.gameObject.activeInHierarchy && ib.interactable;
        }

        // The item's contextual interact: prime the tooltip on the dock, then click its interact button.
        public static void Secondary(UIDragDock dock, IModHost host)
        {
            NavigationManager.Singleton.Select(InventoryAdapter.Selectable(dock));
            Button ib = InventoryAdapter.Tooltip()?.interactButton;
            if (ib != null && ib.gameObject.activeInHierarchy && ib.interactable)
                ib.onClick.Invoke();
            else
                host.LogWarning($"InventoryCommit: no active interact button for dock '{dock.name}'.");
        }

        // Sell a pawnable: the tooltip's priced PAWN button, whose click listener sells whatever item the
        // tooltip was last PRIMED to - not the selection. After a sale the game's delayed re-show primes
        // the first grid item regardless of selection, so selecting the dock is not enough; re-prime the
        // tooltip to this dock's item through the game's own Show (synchronous - it rewires the click
        // listener in the same call) before clicking, so Enter always sells the item the player heard.
        public static void Sell(UIDragDock dock, IModHost host)
        {
            var item = InventoryAdapter.ItemInDock(dock);
            if (item == null)
            {
                host.LogWarning($"InventoryCommit: sell on empty dock '{dock.name}'.");
                return;
            }
            NavigationManager.Singleton.Select(InventoryAdapter.Selectable(dock));
            // The internal list name, not the display name - Show hides the tooltip for a name the
            // gained-items ledger does not know.
            InventoryTooltip.Show(item.listName);
            Button ib = InventoryAdapter.Tooltip()?.interactButton;
            if (ib != null && ib.gameObject.activeInHierarchy && ib.interactable)
                ib.onClick.Invoke();
            else
                host.LogWarning($"InventoryCommit: no active PAWN button for dock '{dock.name}'.");
        }
    }
}
