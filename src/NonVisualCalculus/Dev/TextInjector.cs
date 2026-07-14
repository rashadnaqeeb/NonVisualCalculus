using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonVisualCalculus.Dev
{
    /// <summary>
    /// Headless text entry for the dev /type endpoint and for Enter/Escape on a focused field, the
    /// counterpart to <see cref="InputInjector"/> for the game's text fields (e.g. the save-name field). A
    /// backgrounded window takes no real keys, so we drive the focused UnityEngine.UI.InputField directly:
    /// append typed text, or commit/cancel the edit the way Enter/Escape would. Host-side and self-contained
    /// (does not go through the feature module) so it works even when the module failed to load. Reads live
    /// state; caches nothing.
    /// </summary>
    internal static class TextInjector
    {
        // Append text to the focused field, like typing it. The field is already active (the rename cell
        // activated it), so setting the text and caret is enough.
        public static string Type(string text)
        {
            InputField f = Focused();
            if (f == null)
                return "[no field] no focused input field to type into\n";
            f.text += text ?? "";
            f.caretPosition = f.text.Length;
            return "typed into " + f.gameObject.name + ": \"" + f.text + "\"\n";
        }

        // Commit the focused field the way Enter does (fire onEndEdit, then deactivate). Returns null when no
        // field is focused, so /input falls through to navigation / the game injector.
        public static string TryCommit()
        {
            InputField f = Focused();
            if (f == null)
                return null;
            f.onEndEdit.Invoke(f.text);
            f.DeactivateInputField();
            return "committed " + f.gameObject.name + ": \"" + f.text + "\"";
        }

        // Cancel the focused field the way Escape does (deactivate, no commit). Returns null when no field is
        // focused.
        public static string TryCancel()
        {
            InputField f = Focused();
            if (f == null)
                return null;
            f.DeactivateInputField();
            return "cancelled " + f.gameObject.name;
        }

        private static InputField Focused()
        {
            EventSystem es = EventSystem.current;
            GameObject go = es != null ? es.currentSelectedGameObject : null;
            return go != null ? go.GetComponent<InputField>() : null;
        }
    }
}
