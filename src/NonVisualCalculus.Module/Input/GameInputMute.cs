namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// The keyboard-ownership lever: disabling every registered InControl <c>PlayerActionSet</c>. The game
    /// has exactly one (<c>MyCharacterActions</c>) and ALL its action input - keyboard, controller, and
    /// InControl-bound mouse alike - flows through it; a disabled set's action getters read released
    /// (<c>OneAxisInputControl</c> gates every read on <c>ownerEnabled</c>, resynced from the set each
    /// update), so the game's handlers see nothing. InControl itself keeps running: devices keep polling
    /// and <c>InputManager.ActiveDevice</c> stays fresh for the mod's own controller bindings, which the
    /// former wholesale <c>InputManager.Enabled</c> mute froze along with the game.
    ///
    /// Taken (reasserted) each owning frame: the game couples the set's Enabled to its own input locks
    /// (<c>CrossPlatformInputManager.ActivateInputActions</c> toggles it with <c>InputManager.Enabled</c>
    /// on the boot logo and the save-delete flows), so a game-side re-enable mid-ownership is expected
    /// and won by the reassert. Released exactly once when ownership ends - never on frames we did not
    /// own - so we don't fight a lock the game set for itself.
    /// </summary>
    internal static class GameInputMute
    {
        /// <summary>Mute the game's action input for this frame (call every owning frame).</summary>
        public static void Take()
        {
            var sets = InControl.InputManager.playerActionSets;
            for (int i = 0; i < sets.Count; i++) sets[i].Enabled = false;
        }

        /// <summary>Hand the game its action input back (call once, on the frame ownership ends).</summary>
        public static void Release()
        {
            var sets = InControl.InputManager.playerActionSets;
            for (int i = 0; i < sets.Count; i++) sets[i].Enabled = true;
        }
    }
}
