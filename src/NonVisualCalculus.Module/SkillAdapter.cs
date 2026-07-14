using NonVisualCalculus.Core.UI;
using PixelCrushers.DialogueSystem;
using Sunshine.Metric;
using TMPro;
using UnityEngine.UI;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Adapter: turns a focused skill on the signature skill screen into a Unity-free
    /// <see cref="SkillState"/> for Core to compose. The focused control is the skill portrait's select
    /// button; its <see cref="SkillPortraitPanel"/> parent is the clean source. The name is the skill's
    /// localized name; the value is the portrait's displayed total (a plain label, no flip clock here);
    /// the signature marker compares the skill against the statically tracked chosen signature. The
    /// description is the skill actor's short tagline from the dialogue database (skills are dialogue
    /// actors), read directly rather than from the on-screen detail panel, which only follows the mouse
    /// and never the controller focus. A focus that is not a skill portrait returns null and falls
    /// through to the next reader. Extraction only; no caching past the live read.
    /// </summary>
    public static class SkillAdapter
    {
        public static SkillState TryRead(Selectable selectable)
        {
            var panel = selectable.GetComponentInParent<SkillPortraitPanel>();
            return panel != null ? Read(panel) : null;
        }

        /// <summary>Read a skill portrait directly into a <see cref="SkillState"/>, for the grid cell that
        /// already holds the panel (no Selectable lookup needed).</summary>
        public static SkillState Read(SkillPortraitPanel panel)
        {
            string name = Skill.SkillTypeToLocalizedName(panel.skill, false);
            bool isSignature = panel.skill == SkillPortraitPanel.signatureSkill;
            return new SkillState(name, Value(panel), Description(panel.skill), isSignature);
        }

        /// <summary>Read a skill portrait on the in-game character sheet for its grid line: name, displayed
        /// total, signature marker, a "can raise" marker set when the skill is upgradeable and the player
        /// has a skill point to spend (<paramref name="pointsAvailable"/>, computed by the caller from the
        /// leveling controller), and the info-panel detail folded on (the bonus breakdown then the long
        /// description). The value is the displayed <c>SkillNumber</c> label, the settled total (the skill
        /// model's own <c>value</c> field is not the live total). The description is the skill's short
        /// tagline, the same one the signature-skill screen reads. The detail is read from the live skill
        /// model, not the shared info panel, so it reflects this skill with no cursor move or frame lag.</summary>
        public static SkillState ReadLeveling(SkillPortraitPanel panel, bool pointsAvailable)
        {
            string name = Skill.SkillTypeToLocalizedName(panel.skill, false);
            // In-game the skill model on the panel is dormant (the static signatureSkill is char-creation
            // only, and currentSkill is not the live total), so the live signature signal is the portrait's
            // signature frame being shown.
            bool isSignature = panel.signatureFrame != null && panel.signatureFrame.gameObject.activeInHierarchy;
            bool canRaise = panel.isUpgradeable && pointsAvailable;
            // The live skill (a Modifiable) on the character, the source the info panel itself reads: its
            // bonus breakdown and long description, composed the game's own way but off the model so the
            // read needs no selection. Null when the character is not up, leaving the detail unspoken.
            var skill = CharsheetView.Singleton?.character?.GetSkill(panel.skill);
            string bonuses = skill != null ? CharacterSheetInfoPanel.GatherModifiableData(skill) : null;
            return new SkillState(name, Value(panel), Description(panel.skill), isSignature, canRaise,
                bonuses, LongDescription(panel.skill));
        }

        // The displayed total sits in the portrait's SkillNumber label. It is a plain TextMeshPro here
        // (the leveling flip clocks are not used on this screen), so its text is the settled value.
        private static int Value(SkillPortraitPanel panel)
        {
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true))
                if (label.gameObject.name == "SkillNumber" && int.TryParse(label.text, out int value))
                    return value;
            return 0;
        }

        // Each skill is a dialogue actor; its short tagline ("Wield raw intellectual power...") is the
        // actor's short_description field, and its long encyclopedic entry the LongDescription field (the
        // same one the info panel reads). DE localizes actor fields through its own custom system, the way
        // its skill panel does; the dialogue database keeps these fields only in English, so Pixel Crushers'
        // LookupLocalizedValue would speak the English dev string in every language. Null when the actor or
        // database is not found, leaving that text unspoken rather than failing the whole read.
        private static string Description(SkillType skill) => ActorField(skill, "short_description");

        private static string LongDescription(SkillType skill) => ActorField(skill, "LongDescription");

        private static string ActorField(SkillType skill, string field)
        {
            DialogueDatabase database = DialogueManager.MasterDatabase;
            Actor actor = database != null ? database.GetActor(Skill.GetActorSkillName(skill)) : null;
            return actor != null
                ? LocalizationCustomSystem.LocalizationUtils.GetActorLocalizedField(actor, field)
                : null;
        }
    }
}
