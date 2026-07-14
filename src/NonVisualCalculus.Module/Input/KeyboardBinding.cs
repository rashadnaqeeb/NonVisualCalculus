using NonVisualCalculus.Core.Input;
using UnityEngine;

namespace NonVisualCalculus.Module.Input
{
    /// <summary>
    /// A keyboard combo polled against Unity's legacy Input. Modifiers must match exactly so Ctrl+A does
    /// not also fire a bare-A binding. The concrete <see cref="InputBinding"/> behind Core's registry.
    /// </summary>
    public sealed class KeyboardBinding : InputBinding
    {
        public KeyCode Key { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }

        public KeyboardBinding(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        private static bool CtrlHeld =>
            UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        private static bool ShiftHeld =>
            UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        private static bool AltHeld =>
            UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);

        private bool ModifiersMatch() => Ctrl == CtrlHeld && Shift == ShiftHeld && Alt == AltHeld;

        public override bool JustPressed() => ModifiersMatch() && UnityEngine.Input.GetKeyDown(Key);
        public override bool Held() => ModifiersMatch() && UnityEngine.Input.GetKey(Key);
        public override bool Released() => ModifiersMatch() && UnityEngine.Input.GetKeyUp(Key);

        public override string DisplayName
        {
            get
            {
                var s = "";
                if (Ctrl) s += "Ctrl+";
                if (Shift) s += "Shift+";
                if (Alt) s += "Alt+";
                return s + Key;
            }
        }

        public override string Type => KeyboardType;

        // "A|ctrl,shift" - the KeyCode, then a comma-list of held modifiers (omitted if none).
        public override string Serialize()
        {
            var mods = new System.Collections.Generic.List<string>();
            if (Ctrl) mods.Add("ctrl");
            if (Shift) mods.Add("shift");
            if (Alt) mods.Add("alt");
            return mods.Count == 0 ? Key.ToString() : Key + "|" + string.Join(",", mods);
        }
    }
}
