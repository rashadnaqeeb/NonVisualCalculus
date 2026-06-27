using System.Collections.Generic;
using DiscoAccess.Core.UI;
using I2.Loc;
using UnityEngine.UI;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Adapter: turns a focused character-creation archetype button into a Unity-free
    /// <see cref="ArchetypeState"/> for Core to compose. An archetype button carries an
    /// <see cref="ArchetypeSelectButton"/>, whose structured fields are the clean source: name,
    /// description, and signature-skill <c>Localize</c> labels, and four <see cref="ArchetypeAbility"/>
    /// holders each wrapping a flip-clock number. Reading those directly avoids the raw TMP sweep, which
    /// picks up every flip-clock animation layer (four stacked copies of each digit) printed before its
    /// abbreviated label. The value is the flip clock's <c>targetValue</c>, the settled number, so it is
    /// right even mid-animation. The custom-character button is an <see cref="ArchetypeSelectButton"/>
    /// too but leaves all those fields null (it has no stats), so it returns null here and falls through
    /// to the generic reader, whose plain "Create your own character" label has no flip clocks to mangle.
    /// Extraction only; no word choice and no caching past the live read.
    /// </summary>
    public static class ArchetypeAdapter
    {
        public static ArchetypeState TryRead(Selectable selectable)
        {
            var button = selectable.gameObject.GetComponent<ArchetypeSelectButton>();
            if (button == null || button.IsCustomCharacterButton)
                return null;

            var attributes = new List<ArchetypeAttribute>();
            AddAttribute(attributes, button.Int, "INT");
            AddAttribute(attributes, button.Psy, "PSY");
            AddAttribute(attributes, button.Phq, "FYS");
            AddAttribute(attributes, button.Mot, "MOT");

            return new ArchetypeState(
                LabelText(button.NameLocalization),
                attributes,
                SignatureSkill(button.SignatureSkillLocalization),
                LabelText(button.DescriptionLocalization));
        }

        // The signature skill is drawn "+SKILL" to mark the archetype's bonus; the term resolves to just
        // the skill name, so re-add the game's leading "+" (read as "plus", conveying the bonus).
        private static string SignatureSkill(Localize localize)
        {
            string name = LabelText(localize);
            return string.IsNullOrEmpty(name) ? name : "+" + name;
        }

        // Append an attribute as its localized full name (Intellect/Psyche/Physique/Motorics, clearer
        // for speech than the on-screen INT/PSY/FYS/MOT) paired with the flip clock's settled value.
        private static void AddAttribute(List<ArchetypeAttribute> list, ArchetypeAbility ability, string key)
        {
            if (ability == null || ability.FlipClockNumber == null)
                return;
            string name = LocalizationManager.GetTranslation(
                "Abilities/ABILITY_NAME_" + key, false, 0, true, false, null, null, true);
            list.Add(new ArchetypeAttribute(name, ability.FlipClockNumber.targetValue));
        }

        // Resolve a Localize's term to natural-case text. The label's own .text is uppercased for display
        // (THINKER, +ENCYCLOPEDIA), which reads oddly; the I2 source term is naturally cased.
        private static string LabelText(Localize localize)
        {
            return localize != null ? GameLocalization.Translate(localize.Term) : null;
        }
    }
}
