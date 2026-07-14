using System.Collections.Generic;

namespace NonVisualCalculus.Core.UI
{
    /// <summary>
    /// Unity-free snapshot of a resolved skill check, extracted by the module adapter from the game's
    /// <c>CheckResult</c> and composed into the silent transcript line by
    /// <see cref="CheckRollAnnouncer"/>. Holds the two dice, the skill value and name that fed the roll,
    /// the base (pre-modifier) target, the target modifiers in effect, the game's own critical word when
    /// the dice came up double six or double ones (a critical overrides the arithmetic, so a roll that
    /// misses its target numerically can still succeed), and whether the check was passive (no dice; the
    /// game displays its roll as the skill value plus a flat base). Each modifier's
    /// <see cref="CheckRollModifier.Bonus"/> is the raw amount it adds to the target, so a positive bonus
    /// raises the bar (a hindrance); the announcer negates it to the effect on the player's check.
    /// </summary>
    public sealed class CheckRollState
    {
        public int Die1 { get; }
        public int Die2 { get; }
        public int SkillValue { get; }
        public string SkillName { get; }
        public int BaseTarget { get; }
        public IReadOnlyList<CheckRollModifier> Modifiers { get; }
        public string? Critical { get; }
        public bool Passive { get; }

        public CheckRollState(int die1, int die2, int skillValue, string skillName, int baseTarget,
            IReadOnlyList<CheckRollModifier> modifiers, string? critical = null, bool passive = false)
        {
            Die1 = die1;
            Die2 = die2;
            SkillValue = skillValue;
            SkillName = skillName;
            BaseTarget = baseTarget;
            Modifiers = modifiers;
            Critical = critical;
            Passive = passive;
        }
    }

    /// <summary>One target modifier in effect on a check: its game-text name and the raw bonus it adds to
    /// the target (positive raises the bar, a hindrance; negative lowers it, a help).</summary>
    public sealed class CheckRollModifier
    {
        public string Name { get; }
        public int Bonus { get; }

        public CheckRollModifier(string name, int bonus)
        {
            Name = name;
            Bonus = bonus;
        }
    }
}
