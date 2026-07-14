using System.Collections.Generic;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A table cell that acts on one save through a shared button: the row is the save, the column is an
    /// action. The game has a single Load/Save button and a single Delete button that act on whichever
    /// save is selected, so on focus the cell makes its save the game's selection (and the buttons follow),
    /// and on activate it re-asserts that selection and runs the button's own click handler. The Load and
    /// Save columns are this base directly with their respective button; Delete adds a deletable gate.
    /// </summary>
    internal class SaveActionCell : TableCell
    {
        protected readonly SaveGameListEntry Entry;
        protected readonly Selectable Row;
        private readonly Button _button;

        public SaveActionCell(SaveGameListEntry entry, Selectable row, Button button)
        {
            Entry = entry;
            Row = row;
            _button = button;
        }

        // Focusable while the save row is shown and interactable; a destroyed entry (deleted mid-screen)
        // drops out until the screen rebuilds.
        public override bool CanFocus => Row != null && Row.isActiveAndEnabled && Row.interactable;

        // The column action's caption, read live from the game's own Load/Save/Delete button (its
        // localized, natural-case label), so it stays correct across language and game updates.
        public override string ColumnHeader => _button != null ? FocusReader.Read(_button) : null;

        // The save line (name, date, time, and a new-save marker), read live; null when the entry is gone
        // so a stale announce during a rebuild speaks nothing rather than crashing.
        public override string RowText
        {
            get
            {
                if (Row == null) return null;
                SaveEntryState s = SaveEntryAdapter.TryRead(Row);
                return s != null ? SaveEntryAnnouncer.Compose(s) : null;
            }
        }

        // As focus lands on any cell of this save's row, select the save in the game so its info shows and
        // the shared buttons act on it.
        public override void OnFocused() => GameCursor.Follow(Row);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Activate);
        }

        // Re-assert the save as the game's selection, then run the shared button's own click handler (the
        // game's Load / Save / Delete path). onClick fires the controller directly without moving the
        // game's selection off the save, so the button keeps acting on the save we just selected. Virtual so
        // the Rename cell, which has no shared button, can supply its own activation.
        protected virtual void Activate()
        {
            GameCursor.Follow(Row);
            _button.onClick.Invoke();
        }
    }

    /// <summary>The Delete cell of a save's row. Only focusable when the save is deletable (auto/quick
    /// saves and read-only saves are not, and neither is the save menu's create-new slot).</summary>
    internal sealed class DeleteCell : SaveActionCell
    {
        public DeleteCell(SaveGameListEntry entry, Selectable row, Button deleteButton)
            : base(entry, row, deleteButton) { }

        public override bool CanFocus => base.CanFocus && Entry != null && Entry.IsDeletable;
    }
}
