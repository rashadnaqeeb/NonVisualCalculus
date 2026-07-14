using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A navigable ability on the Adjust Abilities (Create Your Own) screen, wrapping the live
    /// <see cref="StatPanel"/>. It reads name, value, grade, and description through the structured
    /// <see cref="AbilityAdapter"/> and composes with the Core <see cref="AbilityAnnouncer"/>, then drives
    /// the score like a slider: Left/Right run the panel leveler's <c>DowngradeRank</c>/<c>UpgradeRank</c>
    /// (the game's own adjust path, exposing the plus/minus buttons the keyboard focus never landed on),
    /// each gated on the leveler's interactable flags - which encode the shared ability-point budget, so
    /// raising one ability needs a point freed by lowering another. After an adjust the navigator
    /// re-announces the new value and grade.
    /// </summary>
    public sealed class AbilityControl : UIElement
    {
        private readonly StatPanel _panel;

        public AbilityControl(StatPanel panel) => _panel = panel;

        // The StatPanel is itself the Selectable; focusable only while shown and interactable.
        public override bool CanFocus => _panel != null && _panel.isActiveAndEnabled && _panel.interactable;

        // The ability name alone, for the navigator's type-ahead and container-label dedup. Read live.
        public override string Label
        {
            get { AbilityState s = AbilityAdapter.TryRead(_panel); return s?.Name; }
        }

        // Just the value and grade, for re-announcing an in-place change after Left/Right.
        public override string Value
        {
            get { AbilityState s = AbilityAdapter.TryRead(_panel); return s != null ? AbilityAnnouncer.ComposeValue(s) : null; }
        }

        // The full focus readout: name, value, grade, description - the same composition the focus-follower
        // used, via the unit-tested Core composer.
        public override string GetFocusText()
        {
            AbilityState s = AbilityAdapter.TryRead(_panel);
            return s != null ? AbilityAnnouncer.Compose(s) : string.Empty;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Decrease, Decrease);
            yield return new ElementAction(ActionIds.Increase, Increase);
        }

        // A blocked raise is "maximum" by default, but here it can instead mean the shared point pool is
        // empty - the ability could still go higher if points were freed elsewhere - which is a distinct
        // cue. Everything else (a real move, or the minimum/maximum bound) uses the universal behavior.
        public override string GetAdjustText(string actionId, bool changed)
        {
            if (!changed && actionId == ActionIds.Increase && PoolEmpty())
                return Strings.AbilityNoPointsLeft;
            return base.GetAdjustText(actionId, changed);
        }

        // Move the game's cursor to this ability as our focus lands, so its selection follows ours.
        public override void OnFocused() => GameCursor.Follow(_panel);

        // The ability-point pool is empty when no ability can be raised: every creation StatPanel's
        // level-up is non-interactable. A panel blocked only at its own cap leaves others raisable, so a
        // single raisable panel means points still remain.
        private static bool PoolEmpty()
        {
            foreach (StatPanel p in Object.FindObjectsOfType<StatPanel>())
                if (p.abilityGradeFlipClock != null && p.leveler != null
                    && p.leveler.levelUp != null && p.leveler.levelUp.interactable)
                    return false;
            return true;
        }

        // Raise/lower the score through the game's own leveler, but only when the matching plus/minus is
        // interactable: that flag is the live ability-point budget (raising needs a freed point, lowering
        // needs to be above the floor), so honoring it keeps us to exactly the moves the game allows.
        private void Increase()
        {
            StatLeveler lev = _panel.leveler;
            if (lev != null && lev.levelUp != null && lev.levelUp.interactable)
                lev.UpgradeRank();
        }

        private void Decrease()
        {
            StatLeveler lev = _panel.leveler;
            if (lev != null && lev.levelDown != null && lev.levelDown.interactable)
                lev.DowngradeRank();
        }
    }
}
