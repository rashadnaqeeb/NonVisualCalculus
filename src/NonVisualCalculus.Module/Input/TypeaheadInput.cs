using NonVisualCalculus.Module.Nav;

namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// The engine-coupled half of type-ahead: each frame it reads the OS-typed characters
    /// (<c>UnityEngine.Input.inputString</c>, which honors keyboard layout, Shift, and dead keys) and
    /// feeds letters (and Space, once a buffer exists) into the navigator's search. The matching, focus
    /// landing, and announcement all live in Core's <see cref="NonVisualCalculus.Core.UI.Nav.TraditionalNavigator"/>;
    /// this side only decodes the keys, mirroring the adapter/composition split.
    ///
    /// Result-stepping (Up/Down/Home/End) and clearing (Escape) ride the normal bound UI actions through
    /// the navigator's Handle, so they are NOT read here - only the unbound typed text is.
    ///
    /// Letters bypass the action system entirely, so they do NOT inherit the navigator's stand-down when a
    /// game text field is editing: this MUST gate on the text-edit state (grace-inclusive) itself, or a
    /// letter would both type into the save-name field and jump the list at once. See <see cref="TextEditGate"/>.
    /// </summary>
    public sealed class TypeaheadInput
    {
        // Whether the navigator owned the keyboard (un-gated) last frame, so the frame we GAIN ownership is
        // swallowed: the very key that opened a screen can still sit in inputString while the new screen
        // auto-focuses, and would otherwise type the opening letter into a fresh search.
        private bool _ownedLastFrame;

        /// <summary>Read this frame's typed characters into the search. <paramref name="editActive"/> is the
        /// grace-inclusive text-edit gate; while it (or the navigator not owning the keyboard) holds, the
        /// search is cleared and no keys are read.</summary>
        public void Tick(ScreenManager screens, bool editActive)
        {
            bool own = screens.OwnsKeyboard && !editActive && screens.TypeAheadEnabled;
            if (!own)
            {
                if (_ownedLastFrame) screens.ClearSearch();
                _ownedLastFrame = false;
                return;
            }

            // Swallow the changeover frame (see _ownedLastFrame): drop any carried-over typed key.
            if (!_ownedLastFrame)
            {
                _ownedLastFrame = true;
                return;
            }

            // Ctrl/Alt chords are hotkeys, never search input.
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt))
                return;

            string typed = UnityEngine.Input.inputString;
            if (string.IsNullOrEmpty(typed)) return;

            foreach (char c in typed)
            {
                if (char.IsLetter(c)) screens.TypeSearch(c);
                else if (c == ' ' && screens.SearchHasBuffer) screens.TypeSearch(' '); // multi-word; a lone Space is not search
                else if (c == '\b') screens.BackspaceSearch(); // inputString carries Backspace as '\b'
            }
        }
    }
}
