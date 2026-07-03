using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using TMPro;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A read-only readout of the character sheet's leveling status: the experience progress and the pool
    /// of unspent skill points. The experience is the game's own localized label (read live from the XP
    /// panel's text, which DE shows as "EXPERIENCE: 51/100"); the skill-point count is the filled segments
    /// of the skill-point pip indicator (which the game keeps in step with each raise), spoken with an
    /// authored "skill points" word since DE shows it only as those pips, no number. Read live each focus;
    /// advertises no actions.
    /// </summary>
    internal sealed class CharStatusCell : UIElement
    {
        public override bool CanFocus => CharsheetView.Singleton != null;

        public override string GetFocusText()
        {
            var parts = new System.Collections.Generic.List<string>(2);

            string xp = ExperienceText();
            if (!string.IsNullOrEmpty(xp))
                parts.Add(xp);

            parts.Add(Strings.SkillPoints(SkillPointsAvailable()));

            return string.Join(", ", parts);
        }

        // The experience line from the XP panel's own label, cleaned of markup. Null when the panel or its
        // label is not up, so the readout falls back to just the skill-point count.
        private static string ExperienceText()
        {
            var panel = CharsheetView.Singleton?.xpPanel;
            if (panel == null)
                return null;
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true))
                if (label.gameObject.name == "Xp Text")
                    return TextFilter.Clean(GameLocalization.Spoken(label));
            return null;
        }

        // The unspent skill-point count, read from the skill-point field's pip indicator (its filled
        // segments), which the game keeps in step with each raise. The leveling controller's own
        // usedSkillPoints/MaxSkillPoints are a character-creation tally and do not track in-game raises.
        private static int SkillPointsAvailable()
        {
            var field = CharsheetView.Singleton?.skillPointField;
            var seg = field != null ? field.GetComponentInChildren<SegmentIndicator>(true) : null;
            return seg != null ? seg.Current : 0;
        }
    }
}
