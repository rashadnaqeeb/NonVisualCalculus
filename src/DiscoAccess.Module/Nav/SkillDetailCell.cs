using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using TMPro;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A read-only detail region for the skill currently selected in the grid, mirroring the game's
    /// character-sheet info panel as one Tab-stop: first the bonus breakdown (Intellect base, learned
    /// skill, signature, items, thoughts), then the long encyclopedic description. The grid's skill cells
    /// drive the panel's selection on focus, so this region always reflects the focused skill; both texts
    /// are kept populated by the game regardless of which tab is visible, so they are read without flipping
    /// the on-screen tab. Each line reads as its own sentence (the cleaner turns the game's line breaks into
    /// pauses). Read live each focus; advertises no actions.
    ///
    /// <see cref="Subject"/> is the skill the grid cells set on focus (null while an attribute is focused -
    /// the panel cannot show an attribute, and would otherwise read the last skill). When it is null this
    /// region drops out of the Tab order, so Tab from an attribute does nothing rather than reading a stale
    /// skill.
    /// </summary>
    internal sealed class SkillDetailCell : UIElement
    {
        public SkillPortraitPanel Subject { get; set; }

        public override bool CanFocus => Subject != null && CharacterSheetInfoPanel.Singleton != null;

        public override string GetFocusText()
        {
            var p = CharacterSheetInfoPanel.Singleton;
            if (p == null)
                return string.Empty;

            // The breakdown then the description, each on its own line in the source; cleaning together
            // turns every line break (within the breakdown and between it and the description) into a
            // sentence pause.
            string raw = Raw(p.extraText) + "\n" + Raw(p.infoText);
            return TextFilter.Clean(raw);
        }

        private static string Raw(TMP_Text t) => t != null ? GameLocalization.Spoken(t) : string.Empty;
    }
}
