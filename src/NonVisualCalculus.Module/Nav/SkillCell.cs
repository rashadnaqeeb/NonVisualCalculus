using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A cell in the signature skill grid wrapping one live <see cref="SkillPortraitPanel"/>. Reads the
    /// skill's name, value, signature marker, and flavour description live at announce time (never cached)
    /// through <see cref="SkillAdapter"/> and the Core <see cref="SkillAnnouncer"/>. On focus it makes the
    /// skill the game's selection so its portrait highlights and the shared Signature Skill button targets
    /// it; on activate it sets this skill as the signature - registering the skill (its select button's own
    /// click) then running the shared Signature Skill button - and the navigator re-announces the gained
    /// signature marker.
    /// </summary>
    internal sealed class SkillCell : UIElement
    {
        private readonly SkillPortraitPanel _panel;
        private readonly Button _signatureButton;

        public SkillCell(SkillPortraitPanel panel, Button signatureButton)
        {
            _panel = panel;
            _signatureButton = signatureButton;
        }

        // Focusable while the skill's select button is shown and interactable.
        public override bool CanFocus
            => _panel != null && _panel.selectButton != null
               && _panel.selectButton.isActiveAndEnabled && _panel.selectButton.interactable;

        // The full spoken line: skill name, value, signature marker, description - composed by Core from a
        // Unity-free snapshot. Overrides the default label/role/value join, whose order does not fit.
        public override string GetFocusText() => SkillAnnouncer.Compose(SkillAdapter.Read(_panel));

        // Just the signature marker, for the navigator's re-announce after Enter sets this skill as the
        // signature: "signature" when it now is, nothing otherwise.
        public override string Value
            => _panel.skill == SkillPortraitPanel.signatureSkill ? Strings.StatusSignature : null;

        public override bool ReannounceOnActivate => true;

        // As focus lands, make this skill the game's selection so its portrait highlights and the shared
        // Signature Skill button targets it.
        public override void OnFocused() => GameCursor.Follow(_panel.selectButton);

        public override IEnumerable<ElementAction> GetActions()
        {
            // No shared signature button (a build that could not find it, already logged) means Enter has
            // nothing to run, so do not advertise an activate that would no-op.
            if (_signatureButton == null) yield break;
            yield return new ElementAction(ActionIds.Activate, SetSignature);
        }

        // Set this skill as the signature: register it as the chosen skill (its select button's own click,
        // which the game tracks as the current selection) then run the shared Signature Skill button. Uses
        // the buttons' click handlers directly (not a nav submit) so the game's selection stays on the
        // skill, like the save action cells.
        private void SetSignature()
        {
            GameCursor.Follow(_panel.selectButton);
            _panel.selectButton.onClick.Invoke();
            _signatureButton.onClick.Invoke();
        }
    }
}
