using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// One slot in the thought cabinet's slot grid, wrapping a live <see cref="ThoughtSlot"/>. Reads its
    /// occupancy and, when filled, the thought's stage, name, effects and description live at announce time
    /// (never cached) through <see cref="ThoughtAdapter"/> and the Core <see cref="ThoughtAnnouncer"/>. On
    /// focus it makes the slot the game's selection so the cabinet highlights it. On Enter it forgets a
    /// filled slot's thought or unlocks a buyable slot (both the game's own way, see
    /// <see cref="ThoughtCommit"/>); an empty or not-yet-unlockable slot has no Enter action. Placing a
    /// thought is done from the list, not here. After a forget (immediate) the new "empty slot" status is
    /// re-announced; an unlock instead raises the game's confirmation, which announces itself, so it is not
    /// re-announced here.
    /// </summary>
    internal sealed class ThoughtSlotCell : UIElement
    {
        private readonly ThoughtSlot _slot;
        private readonly IModHost _host;

        public ThoughtSlotCell(ThoughtSlot slot, IModHost host)
        {
            _slot = slot;
            _host = host;
        }

        // Every slot is focusable while shown, including empty and locked ones, so the grid keeps a stable
        // shape and the player can reach a slot to unlock it or hear that it is empty.
        public override bool CanFocus => _slot != null && _slot.isActiveAndEnabled;

        public override string GetFocusText() => ThoughtAnnouncer.Compose(ThoughtAdapter.ReadSlot(_slot));

        // The re-announce after a successful Enter: the slot's now-current status (empty, after a forget).
        public override string Value => ThoughtAnnouncer.ComposeStatus(ThoughtAdapter.ReadSlot(_slot));

        // Re-announce after forget (the slot changes in place); not after unlock, whose confirmation popup
        // speaks for itself. Read after the action ran: a committed forget has left the slot OPEN, an unlock
        // that only opened a confirmation leaves it BUYABLE.
        public override bool ReannounceOnActivate => _slot.State != ThoughtSlot.SlotState.BUYABLE;

        public override void OnFocused() => GameCursor.Follow(_slot);

        public override IEnumerable<ElementAction> GetActions()
        {
            switch (_slot.State)
            {
                case ThoughtSlot.SlotState.FILLED:
                case ThoughtSlot.SlotState.FIXTURE:
                    yield return new ElementAction(ActionIds.Activate, () => ThoughtCommit.Interact(_slot, _host));
                    break;
                case ThoughtSlot.SlotState.BUYABLE:
                    yield return new ElementAction(ActionIds.Activate, () => ThoughtCommit.Unlock(_slot, _host));
                    break;
            }
        }
    }
}
