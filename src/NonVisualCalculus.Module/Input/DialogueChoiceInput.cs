namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// The engine-coupled read of the number row for the dialogue viewer's jump-to-choice: each frame it
    /// reports which bare digit (1-9) was pressed this frame, or 0 for none. The dialogue screen turns that
    /// into a cursor move to the matching choice. Kept here (not in the input registry) because it is scoped
    /// to one screen and never fires a game action - it only moves our own cursor, so it rides no bound key.
    ///
    /// Only the number row is read (not the keypad), and only a modifier-free press: Shift+digit is a
    /// symbol, and Ctrl/Alt+digit are hotkeys, none of which are a choice jump. The dialogue view runs the
    /// UI+Status categories only, so the world's Alpha1/Alpha2 hand-item binds are inert here and never
    /// compete; and we mute the game's action set while we own the screen, so DE's own number-to-select is muted -
    /// a number moves the cursor, never clicks the option.
    /// </summary>
    public static class DialogueChoiceInput
    {
        // The number-row digit pressed this frame with no modifier held, 1-9, or 0 for none. The keypad is
        // deliberately excluded (the request is the number row); a held Ctrl/Alt/Shift disqualifies the press.
        public static int PressedChoiceDigit()
        {
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                return 0;

            for (int n = 1; n <= 9; n++)
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha1 + (n - 1)))
                    return n;
            return 0;
        }
    }
}
