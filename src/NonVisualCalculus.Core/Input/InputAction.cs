using System;
using System.Collections.Generic;

namespace NonVisualCalculus.Core.Input
{
    /// <summary>
    /// A named mod command with one or more bindings. Exposes per-frame phase state and fires
    /// <see cref="Performed"/> when the manager sees a live JustPressed that no dispatcher consumed
    /// (the "global hotkey" path). Distinct from <see cref="ElementAction"/>-style commands a control
    /// advertises; this is the keybinding side.
    /// </summary>
    public sealed class InputAction
    {
        public string Key { get; }
        public string Label { get; }

        /// <summary>The input layer this action lives in (decides when it is polled and how identical
        /// chords across categories resolve - see <see cref="InputCategory"/>).</summary>
        public InputCategory Category { get; internal set; } = InputCategory.Global;

        private readonly List<InputBinding> _bindings = new List<InputBinding>();
        public IReadOnlyList<InputBinding> Bindings => _bindings;

        /// <summary>Fired on a live JustPressed that no dispatcher consumed.</summary>
        public event Action? Performed;

        public InputAction(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public InputAction AddBinding(InputBinding binding)
        {
            _bindings.Add(binding);
            return this;
        }

        /// <summary>Whether this action auto-repeats while held (nav directions, Tab). Set via <see cref="Repeating"/>.</summary>
        public bool Repeats { get; private set; }
        internal double NextRepeatTime;

        public InputAction Repeating()
        {
            Repeats = true;
            return this;
        }

        internal void InvokePerformed() => Performed?.Invoke();
    }
}
