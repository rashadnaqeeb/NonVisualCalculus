using System.Collections.Generic;
using NonVisualCalculus.Core.Text;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Metric; // CheckResult, CheckModifier

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The silent roll line for a resolved skill check, placed in the transcript just above the game's own
    /// outcome line. That outcome line already speaks the skill, difficulty and success/failure live, so
    /// this carries only what it omits - the dice, the modifiers, and the game's critical word on double
    /// six or double ones; a passive check (no dice) reads just its bare total against the target, the
    /// same numbers the game's hover tooltip shows - read on demand when the player walks the scrollback
    /// with Up. Never spoken on delivery (it is not the current line). Holds the live
    /// <see cref="CheckResult"/> and reads it at speech time (never cached); the Unity-free composition is
    /// done by <see cref="CheckRollAnnouncer"/> in Core. Advertises no actions.
    /// </summary>
    internal sealed class DialogueCheckRollCell : UIElement
    {
        private readonly CheckResult _check;

        public DialogueCheckRollCell(CheckResult check)
        {
            _check = check;
        }

        public override bool CanFocus => !string.IsNullOrEmpty(GetFocusText());

        public override string GetFocusText()
        {
            CheckRollState state = BuildState(_check);
            return state == null ? string.Empty : TextFilter.Clean(CheckRollAnnouncer.Compose(state));
        }

        // Extract the live check into Unity-free data: the two dice, the skill value and name, the base
        // (pre-modifier) target, the target modifiers (raw bonus; the announcer negates to the effect on
        // the player's check), and the game's own critical word when the dice came up double six or double
        // ones - a critical overrides the arithmetic, so without it a numeric miss that succeeded reads as
        // a contradiction. A passive check carries no dice, so it passes as passive and composes to just
        // its total against the target. Null when the check neither rolled nor resolved passively (a
        // forced or unresolved node).
        private static CheckRollState BuildState(CheckResult c)
        {
            if (c == null || !(c.HasRoll() || c.checkType == CheckType.PASSIVE))
                return null;
            var mods = new List<CheckRollModifier>();
            var src = c.applicableTargetModifiers;
            if (src != null)
                for (int i = 0; i < src.Count; i++)
                {
                    CheckModifier m = src[i];
                    string name = m != null ? m.explanation : null;
                    if (string.IsNullOrEmpty(name))
                        continue;
                    // Drop the explanation's trailing sentence punctuation so it does not collide with the
                    // chain's commas (the same trim the pre-roll breakdown applies).
                    name = name.TrimEnd().TrimEnd('.', ',', ';', ':');
                    mods.Add(new CheckRollModifier(name, m.bonus));
                }
            string critical = c.SixSix() ? Sunshine.ConstTooltip.critical_success
                : c.Snakeyes() ? Sunshine.ConstTooltip.critical_failure
                : null;
            return new CheckRollState(c.die1, c.die2, c.SkillValue(), c.SkillName(), c.baseTarget, mods,
                critical, c.checkType == CheckType.PASSIVE);
        }
    }
}
