using System;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// An action a <see cref="UIElement"/> advertises (activate, back, increase, ...). Navigators discover
    /// actions by <see cref="Id"/> and invoke them; they never switch on element type. Distinct from
    /// <c>InputAction</c>, which is a keybinding.
    /// </summary>
    public sealed class ElementAction
    {
        public string Id { get; }
        private readonly Action _execute;

        public ElementAction(string id, Action execute)
        {
            Id = id;
            _execute = execute;
        }

        public void Execute() => _execute();
    }

    /// <summary>Standard action ids navigators map inputs to.</summary>
    public static class ActionIds
    {
        public const string Activate = "activate"; // primary action (Enter)
        public const string Back = "back";          // screen-level back/close (Escape)
        public const string Increase = "increase";  // arrow-right on a slider/stepper
        public const string Decrease = "decrease";  // arrow-left on a slider/stepper
    }
}
