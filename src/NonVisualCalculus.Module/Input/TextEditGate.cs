using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// The single source of truth for "a game text field currently owns the keyboard" - today the save-name
    /// rename field, and any future in-game text entry. Every input path that would otherwise consume keys
    /// must defer to <see cref="Active"/> so the keystrokes reach the field instead.
    ///
    /// We do NOT hand the keyboard back to the game during editing: the field types and commits through
    /// Unity's StandaloneInputModule (OnUpdateSelected reads the keyboard via IMGUI), which runs regardless
    /// of who owns the input lever, so keeping our lever held costs nothing and avoids waking the game's
    /// menu input - which on DE re-handles the commit Enter as a submit that re-opens edit mode. So the
    /// navigator keeps owning the keyboard and merely gates itself off via <see cref="Active"/>.
    ///
    /// This matters most for the type-ahead search to come: it reads raw physical keys and so bypasses the
    /// action system entirely, the very mechanism the navigator uses to stand down. It will not inherit that
    /// protection for free - its key handling MUST check <see cref="Active"/> first, or a letter would both
    /// type into the field and jump a list at once. Gate type-ahead on <see cref="Active"/> (grace-inclusive,
    /// see below), not raw focus, and clear its buffer on <see cref="JustBegan"/>.
    ///
    /// <see cref="Active"/> is held one frame past the edit actually ending (the grace frame) so the commit
    /// Enter or cancel Escape that ends editing cannot leak into a fresh activation or type-ahead the same
    /// frame. Detection keys on a live focused InputField rather than the game's IsEditingInputField static,
    /// which can stick true after a screen closes and would then wedge every input path off forever.
    /// </summary>
    public sealed class TextEditGate
    {
        private bool _editingLast;

        /// <summary>Whether a text field owns the keyboard this frame (grace-inclusive). The one flag every
        /// input path defers to before consuming a key.</summary>
        public bool Active { get; private set; }

        /// <summary>This frame editing just engaged: speak "edit mode" and reset transient input state such
        /// as a type-ahead buffer.</summary>
        public bool JustBegan { get; private set; }

        /// <summary>This frame editing just ended: re-read the focused control so the player hears the
        /// committed value and where focus now rests.</summary>
        public bool JustEnded { get; private set; }

        /// <summary>Recompute from the live focused field. Call once per frame, before input is polled and
        /// before any path that gates on <see cref="Active"/>.</summary>
        public void Update()
        {
            bool editingNow = FieldFocused();
            JustBegan = editingNow && !_editingLast;
            JustEnded = !editingNow && _editingLast;
            Active = editingNow || _editingLast; // hold the gate one frame past the edit ending
            _editingLast = editingNow;
        }

        // A focused, actively-editing InputField is the EventSystem's selected object. Our navigation never
        // selects an InputField, so this is true only during real text entry.
        private static bool FieldFocused()
        {
            EventSystem es = EventSystem.current;
            GameObject go = es != null ? es.currentSelectedGameObject : null;
            if (go == null)
                return false;
            var legacy = go.GetComponent<InputField>();
            if (legacy != null && legacy.isFocused)
                return true;
            var tmp = go.GetComponent<TMP_InputField>();
            return tmp != null && tmp.isFocused;
        }
    }
}
