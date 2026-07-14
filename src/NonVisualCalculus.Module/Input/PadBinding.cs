using NonVisualCalculus.Core.Input;
using InControl;

namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// A controller control polled against InControl's active device, the pad sibling of
    /// <see cref="KeyboardBinding"/> behind Core's registry. Reads <c>InputManager.ActiveDevice</c> live
    /// on every query (never cached), so it rides the game's own per-brand mapping profiles and deadzones;
    /// the keyboard mute (<see cref="GameInputMute"/>) leaves InControl's device polling running precisely
    /// so these reads stay fresh while the game's own actions are muted. With no controller attached the
    /// active device is InControl's null device, whose controls all read released, so an unbound pad is
    /// simply inert. Stick directions (e.g. <c>LeftStickUp</c>) are one-axis controls with InControl's
    /// press threshold, so a held stick reads as a held direction like a held key.
    /// </summary>
    public sealed class PadBinding : InputBinding
    {
        public InputControlType Control { get; }

        public PadBinding(InputControlType control) { Control = control; }

        private InputControl Read() => InControl.InputManager.ActiveDevice.GetControl(Control);

        public override bool JustPressed() => Read().WasPressed;
        public override bool Held() => Read().IsPressed;
        public override bool Released() => Read().WasReleased;

        public override string DisplayName => Control.ToString();

        public override string Type => "pad";

        public override string Serialize() => Control.ToString();
    }
}
