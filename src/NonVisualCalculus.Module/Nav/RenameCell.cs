using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The Rename cell of a save row (save menu only): activating it puts the entry's save-name field into
    /// edit mode so the player can type a name. It is a <see cref="SaveActionCell"/> for the same save (so
    /// it reads the same save line and selects the same save on focus) but with no shared button: instead of
    /// clicking a controller button, it triggers the game's own per-entry edit (the entry's OnPointerClick,
    /// which flips the field interactable and focuses it). The module's pump then detects the focused field,
    /// gates our navigator off, and speaks "edit mode".
    ///
    /// Focusable ONLY for the create-new slot. Existing saves expose an editable name field too (it is the
    /// same prefab), but committing an edit on one does not persist - the game only applies the typed name
    /// when creating the new save, not as a standalone rename of an existing file (verified live: editing an
    /// existing save's name and committing leaves its name and file unchanged). Offering Rename on them would
    /// be a dead action, invisible failure to a blind player, so we do not.
    /// </summary>
    internal sealed class RenameCell : SaveActionCell
    {
        // An InputField waiting to be focused by the pump on a later frame than the Enter that requested it,
        // so the activating keypress is gone before the field is live and cannot immediately commit it.
        internal static InputField PendingActivation;

        // The entry's save-name field, resolved at build (a live component reference, read at edit time). Null
        // if the entry has no legacy InputField under a "Text" child - logged at build so the dead rename is
        // visible, and made non-focusable below so the player never lands on an action that cannot work.
        private readonly InputField _field;

        public RenameCell(IModHost host, SaveGameListEntry entry, Selectable row)
            : base(entry, row, null)
        {
            Transform text = row.transform.Find("Text");
            _field = text != null ? text.GetComponent<InputField>() : null;
            if (_field == null)
                host.LogWarning($"RenameCell: save '{entry.SaveName}' has no legacy InputField under a 'Text' child; rename disabled for it.");
        }

        // Only the create-new slot: that is the one save whose typed name the game actually applies (when it
        // creates the save). Requires a name field to type into, so a slot whose field we could not resolve
        // is not offered as a dead action.
        public override bool CanFocus => base.CanFocus && Entry != null && Entry.IsNewSave && _field != null;

        public override string ColumnHeader => Strings.ActionRename;

        // Enter the game's edit mode the way a click does (its pointer-click handler sets the field
        // interactable and wires the rename commit), then hand the field to the pump to focus on a LATER
        // frame. It must not be focused this frame: the Enter that activated this cell is still down, and a
        // field focused now would consume that same Enter and immediately commit, dropping the player back
        // out of edit mode. Once the field is focused the pump gates our navigator off so keys reach it.
        protected override void Activate()
        {
            GameCursor.Follow(Row);
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerClickHandler>(Row.gameObject, data, ExecuteEvents.pointerClickHandler);
            PendingActivation = _field;
        }
    }
}
