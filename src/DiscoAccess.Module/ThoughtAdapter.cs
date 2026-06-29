using System;
using System.Linq;
using DiscoAccess.Core.UI;
using Sunshine;
using Sunshine.Metric;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Adapter: turns a live thought cabinet slot or master-list entry into a Unity-free
    /// <see cref="ThoughtSnapshot"/> for Core to compose. A slot maps its occupancy state directly; a slot
    /// holding a thought, and every list entry, is read from the <see cref="ThoughtCabinetProject"/> model -
    /// its localized name, the effects that apply in its current stage (research bonuses while cooking,
    /// completion bonuses once fixed) via <see cref="CharacterEffect.EffectName"/>, and the matching
    /// description (the completion text once researched). Read straight from the model, so it never depends
    /// on the selection-driven tooltip being primed; extraction only, nothing cached past the live read.
    /// </summary>
    public static class ThoughtAdapter
    {
        public static ThoughtSnapshot ReadSlot(ThoughtSlot slot)
        {
            switch (slot.State)
            {
                case ThoughtSlot.SlotState.OPEN:
                    return new ThoughtSnapshot(null, ThoughtStatusKind.Empty);
                case ThoughtSlot.SlotState.BUYABLE:
                    return new ThoughtSnapshot(null, ThoughtStatusKind.Unlockable);
                case ThoughtSlot.SlotState.LOCKED:
                    return new ThoughtSnapshot(null, ThoughtStatusKind.Locked);
                default: // FILLED, FIXTURE - a thought occupies the slot
                    return ReadProject(slot.Project);
            }
        }

        public static ThoughtSnapshot ReadListItem(ThoughtOnList item) => ReadProject(item.Project);

        // Map a thought's lifecycle stage onto the snapshot, reading the effects and description that apply
        // in that stage. An undiscovered thought hides its name and details (it is a mystery to the player).
        private static ThoughtSnapshot ReadProject(ThoughtCabinetProject p)
        {
            if (p == null || p.state == ThoughtState.UNKNOWN)
                return new ThoughtSnapshot(null, ThoughtStatusKind.Unknown);

            string name = p.GetDisplayName();
            switch (p.state)
            {
                case ThoughtState.COOKING:
                    return new ThoughtSnapshot(name, ThoughtStatusKind.Researching, Percent(p),
                        Effects(p.researchEffects), p.description,
                        researchMinutesLeft: Math.Max(0, p.ResearchTimeLeft));
                case ThoughtState.DISCOVERED:
                    // Cooking just finished; the result is not yet fixed. Read as research complete.
                    return new ThoughtSnapshot(name, ThoughtStatusKind.Researching, 100,
                        Effects(p.researchEffects), p.description);
                case ThoughtState.FIXED:
                    return new ThoughtSnapshot(name, ThoughtStatusKind.Researched, 0,
                        Effects(p.completionEffects), p.completionDescription);
                case ThoughtState.FORGOTTEN:
                    // The game shows no detail tooltip for a forgotten thought (only its name in the
                    // forgotten group), so we reveal neither its effects nor its description either.
                    return new ThoughtSnapshot(name, ThoughtStatusKind.Forgotten);
                default: // KNOWN - gathered but not placed
                    return new ThoughtSnapshot(name, ThoughtStatusKind.Available, 0,
                        Effects(p.researchEffects), p.description,
                        researchMinutesTotal: Math.Max(0, p.ResearchTime));
            }
        }

        // Research progress as a 0-100 percent COMPLETE, the same number the game shows sighted players on
        // the list entry. ResearchProgress is the model's fraction REMAINING (researchTimeLeft / researchTime,
        // counting down to zero), so the completed share is one minus that. Truncated, matching the game's
        // own display (a thought at 79.8 percent reads "79%"), and clamped (the model can read slightly out
        // of range on a fixed thought, where the percent is not announced anyway).
        private static int Percent(ThoughtCabinetProject p)
        {
            int v = (int)((1f - p.ResearchProgress) * 100f);
            return Math.Max(0, Math.Min(100, v));
        }

        // The bonuses an effect set grants, joined into one phrase. EffectName is the game's own localized,
        // color-free rendering ("Learning cap for Logic raised to 4"); any residual markup is stripped by
        // the Core text filter at speech time. Null when the thought has no effects in this stage.
        private static string Effects(CharacterEffect[] effects)
        {
            if (effects == null || effects.Length == 0)
                return null;
            string joined = string.Join(", ", effects
                .Select(e => e.EffectName(false, false, false, true))
                .Where(s => !string.IsNullOrEmpty(s)));
            return string.IsNullOrEmpty(joined) ? null : joined;
        }
    }
}
