namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Unity-free snapshot of a focused skill on the signature skill screen (the grid where the player
    /// picks one skill as their signature), extracted by the module adapter and composed into speech by
    /// <see cref="SkillAnnouncer"/>. The name and description are the game's own localized strings (the
    /// description is the skill actor's short tagline, read from the dialogue database rather than the
    /// on-screen detail panel, which never follows controller focus); the value is the skill's displayed
    /// total. <see cref="IsSignature"/> is true when this skill is the one currently set as the
    /// signature. The description is optional (a lookup miss leaves it null).
    ///
    /// <see cref="CanRaise"/> is set on the in-game character sheet, where a skill point can be spent to
    /// raise the skill: true when the skill is upgradeable and points remain. The signature-skill screen
    /// leaves it false (no leveling there), so the announcer adds the "can raise" marker only on the sheet.
    /// </summary>
    public sealed class SkillState
    {
        public string Name { get; }
        public int Value { get; }
        public string? Description { get; }
        public bool IsSignature { get; }
        public bool CanRaise { get; }

        public SkillState(string name, int value, string? description, bool isSignature, bool canRaise = false)
        {
            Name = name;
            Value = value;
            Description = description;
            IsSignature = isSignature;
            CanRaise = canRaise;
        }
    }
}
