namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// A mod-owned text-entry session (a bookmark name): what <see cref="TextEditGate"/> is for the
    /// game's own fields, for a field the MOD draws. The editing cell activates a session by setting
    /// <see cref="ModTextEntry.Active"/>; while one is active the UI dispatch routes Enter to
    /// <see cref="Commit"/> and Escape to <see cref="Cancel"/> and freezes navigation under the typing,
    /// and the per-frame reader feeds typed characters in. The session ends itself (clearing Active) on
    /// commit or cancel; the <see cref="Nav.ScreenManager"/> clears it defensively whenever the overlay
    /// hosting it goes away, so a swapped or closed menu never leaves keys routed into a dead cell.
    /// </summary>
    internal interface IModTextSession
    {
        void Type(char c);
        void Backspace();
        void Commit();
        void Cancel();
    }

    internal static class ModTextEntry
    {
        private static IModTextSession _active;

        /// <summary>The active session, null when none. Set by the editing cell, cleared by the session
        /// ending (commit/cancel) or its host overlay closing. The setter also owns the OS IME: Unity's
        /// default Auto mode enables composition only for the engine's own input fields, and this field
        /// is mod-drawn, so CJK typing composes only while a session explicitly turns the IME on.</summary>
        internal static IModTextSession Active
        {
            get => _active;
            set
            {
                _active = value;
                UnityEngine.Input.imeCompositionMode = value != null
                    ? UnityEngine.IMECompositionMode.On
                    : UnityEngine.IMECompositionMode.Auto;
            }
        }

        /// <summary>Whether the IME holds an uncommitted composition. While it does, Enter and Escape
        /// belong to the IME (accept or cancel the composition), not the edit; the committed characters
        /// arrive through <c>Input.inputString</c> like any typed text.</summary>
        internal static bool Composing => !string.IsNullOrEmpty(UnityEngine.Input.compositionString);

        /// <summary>Feed this frame's OS-typed characters (<c>Input.inputString</c>, which honors
        /// keyboard layout and key repeat) into the active session. Ctrl/Alt chords are hotkeys, never
        /// text; Enter is not read here (the bound Activate action commits); '\b' deletes - the raw
        /// counterpart of the type-ahead reader, gated the same way.</summary>
        internal static void Tick()
        {
            IModTextSession session = Active;
            if (session == null) return;
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt))
                return;

            string typed = UnityEngine.Input.inputString;
            if (string.IsNullOrEmpty(typed)) return;
            foreach (char c in typed)
            {
                if (c == '\b') session.Backspace();
                else if (!char.IsControl(c)) session.Type(c);
            }
        }
    }
}
