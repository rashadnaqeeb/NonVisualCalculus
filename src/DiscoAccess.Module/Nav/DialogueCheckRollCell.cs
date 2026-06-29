using System.Collections.Generic;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Metric; // CheckResult, CheckModifier

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The silent roll line for a resolved skill check, placed in the transcript just above the game's own
    /// outcome line. That outcome line already speaks the skill, difficulty and success/failure live, so
    /// this carries only what it omits, the dice and the modifiers, read on demand when the player walks
    /// the scrollback with Up. Never spoken on delivery (it is not the current line). Holds the live
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

        // Extract the live roll into Unity-free data: the two dice, the skill value and name, the base
        // (pre-modifier) target, and the target modifiers (raw bonus; the announcer negates to the effect
        // on the player's check). Null when the check has not actually rolled (a forced or unresolved node).
        private static CheckRollState BuildState(CheckResult c)
        {
            if (c == null || !c.HasRoll())
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
            return new CheckRollState(c.die1, c.die2, c.SkillValue(), c.SkillName(), c.baseTarget, mods);
        }
    }
}
