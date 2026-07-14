using InControl;

namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// Forwards a mod hotkey to the game as a real InControl action press, so the game's own input handlers
    /// open (or refuse) the screen with all their gating - the mod adds none of its own. The press is
    /// written directly onto the action - a pressed <c>thisState</c> over a released <c>lastState</c> IS
    /// <c>WasPressed</c> true - with the action's <c>ownerEnabled</c> forced on, because the keyboard mute
    /// disables the owning set (<see cref="GameInputMute"/>) and every action read gates on that flag.
    /// InControl is frozen (<c>InputManager.Enabled</c> off) for the press's one-frame lifetime: left
    /// running, its update would recompute the state from the real (unpressed) bindings and resync
    /// <c>ownerEnabled</c> off before handlers ordered ahead of our pump could read the press. The release
    /// unfreezes it, and InControl's next update resyncs state and flag from truth. <see cref="Tick"/> runs
    /// from the pump right after input is polled: it writes the requested press this frame (for the game's
    /// handlers to read) and releases the previous one, so each is a clean one-frame edge.
    /// </summary>
    internal static class GameActionPress
    {
        private static PlayerAction _pending;   // requested this frame, written by the next Tick
        private static PlayerAction _injected;  // written last Tick, released this Tick

        /// <summary>Queue a game action to be pressed; the next <see cref="Tick"/> writes it onto the action.</summary>
        public static void Request(PlayerAction action) => _pending = action;

        /// <summary>Release the previous frame's press, then write this frame's, so the press lasts one frame.</summary>
        public static void Tick()
        {
            if (_injected != null) ReleaseInjected();
            if (_pending != null)
            {
                // Freeze BEFORE writing: the disable transition itself clears all input state
                // (InputManager.set_Enabled(false) calls ClearInputState), which would wipe the press.
                InControl.InputManager.Enabled = false;
                var pressed = new InputControlState();
                pressed.Set(true);
                _pending.lastState = new InputControlState();
                _pending.thisState = pressed;
                _pending.ownerEnabled = true;
                _injected = _pending;
                _pending = null;
            }
        }

        /// <summary>Drop any in-flight press and unfreeze InControl, for module teardown so a reload never
        /// leaves the game's input frozen mid-injection.</summary>
        public static void Reset()
        {
            _pending = null;
            if (_injected != null) ReleaseInjected();
        }

        private static void ReleaseInjected()
        {
            _injected.thisState = new InputControlState();
            _injected = null;
            InControl.InputManager.Enabled = true;
        }
    }
}
