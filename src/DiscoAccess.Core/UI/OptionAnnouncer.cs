using System;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused options control from its <see cref="OptionState"/>.
    /// The order follows the house style and the reference mods: distinguishing word first (the setting
    /// name), then the control type ("slider"/"toggle"/"dropdown") so the user knows the interaction
    /// model, then the current value. <see cref="ComposeValue"/> yields the value alone, for
    /// re-announcing an in-place change (adjusting a slider or toggling) where the name was already
    /// spoken on focus. All mod-authored words come from the Strings table; the dropdown caption and
    /// the setting label are game text, spoken verbatim.
    /// </summary>
    public static class OptionAnnouncer
    {
        public static string Compose(OptionState s)
        {
            return Text.SpokenLine.Join(s.Label, TypeWord(s.Kind), ValueWord(s), s.Description);
        }

        public static string ComposeValue(OptionState s)
        {
            return ValueWord(s);
        }

        private static string TypeWord(OptionControlKind kind)
        {
            switch (kind)
            {
                case OptionControlKind.Slider: return ControlSlider;
                case OptionControlKind.Toggle: return ControlToggle;
                case OptionControlKind.Dropdown: return ControlDropdown;
                default: return string.Empty;
            }
        }

        private static string ValueWord(OptionState s)
        {
            switch (s.Kind)
            {
                case OptionControlKind.Toggle:
                    return s.ToggleOn ? StatusOn : StatusOff;
                case OptionControlKind.Dropdown:
                    return s.DropdownCaption ?? string.Empty;
                case OptionControlKind.Slider:
                    if (s.SliderStepped)
                        return StepWord(s);
                    return Percent((int)Math.Round(s.SliderFraction * 100f));
                default:
                    return string.Empty;
            }
        }

        // Menu Size and Dialogue Text Size are both a small/medium/large size scale; a stepped slider
        // with no authored words falls back to a generic position ("step 2 of 3").
        private static readonly string[] SizeWords = { StepSmall, StepMedium, StepLarge };

        private static string StepWord(OptionState s)
        {
            string[]? words = null;
            switch (s.SteppedId)
            {
                case SteppedSliderId.MenuSize:
                case SteppedSliderId.DialogueTextSize:
                    words = SizeWords;
                    break;
            }

            if (words != null && s.StepIndex >= 0 && s.StepIndex < words.Length)
                return words[s.StepIndex];
            return Step(s.StepIndex + 1, s.StepCount);
        }
    }
}
