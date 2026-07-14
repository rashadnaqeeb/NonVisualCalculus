namespace NonVisualCalculus.Core.UI
{
    /// <summary>
    /// Unity-free snapshot of a focused ability on the Adjust Abilities (Create Your Own) screen,
    /// extracted by the module adapter and composed into speech by <see cref="AbilityAnnouncer"/>. The
    /// name and description are the game's own localized strings; the value is the ability's settled
    /// score (read from its grade flip clock's target, right even mid-animation), which is also the
    /// index of the qualitative grade word ("Good", "Great"). The grade is optional because the game's
    /// grade table has no word for a zero score.
    /// </summary>
    public sealed class AbilityState
    {
        public string Name { get; }
        public int Value { get; }
        public string? Grade { get; }
        public string? Description { get; }

        public AbilityState(string name, int value, string? grade, string? description)
        {
            Name = name;
            Value = value;
            Grade = grade;
            Description = description;
        }
    }
}
