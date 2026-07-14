namespace NonVisualCalculus.Core.UI
{
    /// <summary>The interactive kind of an options-screen control, which decides its value wording.</summary>
    public enum OptionControlKind { Slider, Toggle, Dropdown }

    /// <summary>
    /// Identifies a discrete (whole-number) slider so the composer can supply authored step words
    /// (Menu Size and Dialogue Text Size read "small/medium/large" rather than a bare number). The
    /// module adapter resolves the id structurally (by node name), keeping authored words in Core.
    /// </summary>
    public enum SteppedSliderId { Unknown, MenuSize, DialogueTextSize }

    /// <summary>
    /// Unity-free snapshot of a focused options control, extracted by the module adapter and composed
    /// into speech by <see cref="OptionAnnouncer"/>. Holds only plain data (no live component), so the
    /// composition is unit-tested off-engine. The label is the setting name (game text or, for the few
    /// label-less controls, an authored override); a dropdown's caption is the game's own localized
    /// value text and is spoken verbatim.
    /// </summary>
    public sealed class OptionState
    {
        public string Label { get; }
        public OptionControlKind Kind { get; }

        // The game's tooltip body for this setting, already localized; null when it has none. Spoken
        // at the end of the full readout, not on a value-only re-announce.
        public string? Description { get; }

        // Toggle.
        public bool ToggleOn { get; }

        // Dropdown: the game's already-localized selected-value caption.
        public string? DropdownCaption { get; }

        // Slider.
        public bool SliderStepped { get; }
        public float SliderFraction { get; }   // continuous slider: position along its travel, 0..1
        public SteppedSliderId SteppedId { get; }
        public int StepIndex { get; }           // stepped slider: 0-based position
        public int StepCount { get; }           // stepped slider: number of steps

        private OptionState(string label, OptionControlKind kind, bool toggleOn, string? dropdownCaption,
            bool sliderStepped, float sliderFraction, SteppedSliderId steppedId, int stepIndex, int stepCount,
            string? description)
        {
            Label = label;
            Kind = kind;
            ToggleOn = toggleOn;
            DropdownCaption = dropdownCaption;
            SliderStepped = sliderStepped;
            SliderFraction = sliderFraction;
            SteppedId = steppedId;
            StepIndex = stepIndex;
            StepCount = stepCount;
            Description = description;
        }

        public static OptionState Toggle(string label, bool on, string? description = null) =>
            new OptionState(label, OptionControlKind.Toggle, on, null, false, 0f, SteppedSliderId.Unknown, 0, 0, description);

        public static OptionState Dropdown(string label, string? caption, string? description = null) =>
            new OptionState(label, OptionControlKind.Dropdown, false, caption, false, 0f, SteppedSliderId.Unknown, 0, 0, description);

        public static OptionState ContinuousSlider(string label, float fraction, string? description = null) =>
            new OptionState(label, OptionControlKind.Slider, false, null, false, fraction, SteppedSliderId.Unknown, 0, 0, description);

        public static OptionState SteppedSlider(string label, SteppedSliderId id, int stepIndex, int stepCount, string? description = null) =>
            new OptionState(label, OptionControlKind.Slider, false, null, true, 0f, id, stepIndex, stepCount, description);
    }
}
