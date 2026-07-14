using System.Linq;
using NonVisualCalculus.Core.Modularity;
using Sunshine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// Runs the thought cabinet's commit actions the game's own way. Selecting a slot or list entry primes
    /// the shared <see cref="ThoughtCabinetTooltip"/> synchronously (confirmed live), wiring its single
    /// contextual action button to the right operation: Internalize for a gathered thought (the game places
    /// it in the first free slot and starts it cooking), Forget for one already placed. Unlocking a buyable
    /// slot runs that slot's own unlock button, which raises the game's spend-a-skill-point confirmation
    /// (read by the popup overlay). We invoke the button's click rather than poke the model directly so the
    /// game's full path - sound, confirmation, slot animation, list refresh - all runs.
    /// </summary>
    internal static class ThoughtCommit
    {
        // Internalize or forget the selected entry, whichever the game wires onto the action button for it.
        public static void Interact(Selectable target, IModHost host)
        {
            NavigationManager.Singleton.Select(target);
            Button button = ActionButton();
            if (button != null)
                button.onClick.Invoke();
            else
                host.LogWarning($"ThoughtCommit: no active thought action button for '{target.name}'.");
        }

        // Unlock a buyable slot: its own unlock button if present, else the tooltip's action button (the
        // unlock prompt the tooltip shows for a buyable slot). Either path raises the game's confirmation.
        public static void Unlock(ThoughtSlot slot, IModHost host)
        {
            NavigationManager.Singleton.Select(slot);
            Button unlock = slot.interactButtonUnlock;
            if (unlock != null && unlock.isActiveAndEnabled && unlock.interactable)
            {
                unlock.onClick.Invoke();
                return;
            }
            Button button = ActionButton();
            if (button != null)
                button.onClick.Invoke();
            else
                host.LogWarning($"ThoughtCommit: no unlock button for slot {slot.SlotIndex}.");
        }

        // The tooltip's single contextual action button: the one active, interactable button that is not a
        // Problem/Solution description toggle. Null when the tooltip is not up or the action is unavailable
        // (e.g. internalize with no free slot leaves the button non-interactable).
        private static Button ActionButton()
        {
            ThoughtCabinetTooltip tooltip = ThoughtCabinetTooltip.Singleton;
            if (tooltip == null)
                return null;
            return tooltip.gameObject.GetComponentsInChildren<Button>(true).FirstOrDefault(b =>
                b.name != "ButtonProblem" && b.name != "ButtonSolution"
                && b.gameObject.activeInHierarchy && b.interactable);
        }
    }
}
