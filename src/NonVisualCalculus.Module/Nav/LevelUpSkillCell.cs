using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A skill cell on the in-game character sheet, wrapping one live <see cref="SkillPortraitPanel"/>.
    /// Reads the skill's name, value, signature marker, a "can raise" marker, flavour description, and the
    /// info panel's full detail (bonus breakdown then long description) folded onto the one line, live at
    /// announce time (never cached) through <see cref="SkillAdapter"/> and the Core
    /// <see cref="SkillAnnouncer"/>. On focus it makes the skill the game's selection so its portrait
    /// highlights; on activate, when the skill is upgradeable and a skill
    /// point is available, it spends one through the game's own level-up button and the navigator
    /// re-announces the raised value. When the raise cannot happen, it instead re-announces why - the game's
    /// own reason for a capped skill, or that no points remain.
    /// </summary>
    internal sealed class LevelUpSkillCell : UIElement
    {
        private readonly SkillPortraitPanel _panel;
        private readonly IModHost _host;
        // Set by Raise when an Enter could not spend a point, read once by the next re-announce: the reason
        // to speak instead of a value (a capped skill's game reason, or that no points remain). Null after a
        // successful raise, so the re-announce reads the new value.
        private string _failureReason;

        public LevelUpSkillCell(SkillPortraitPanel panel, IModHost host)
        {
            _panel = panel;
            _host = host;
        }

        // Focusable while the skill's select button is shown and interactable.
        public override bool CanFocus
            => _panel != null && _panel.selectButton != null
               && _panel.selectButton.isActiveAndEnabled && _panel.selectButton.interactable;

        // The full spoken line: skill name, value, signature, "can raise", description - composed by Core
        // from a Unity-free snapshot. Overrides the default label/role/value join, whose order does not fit.
        public override string GetFocusText()
            => SkillAnnouncer.Compose(SkillAdapter.ReadLeveling(_panel, PointsAvailable()));

        // The navigator's re-announce after Enter: a failure reason if the raise could not happen (consumed
        // once), else the raised value plus its markers.
        public override string Value
        {
            get
            {
                if (_failureReason != null)
                {
                    string reason = _failureReason;
                    _failureReason = null;
                    return reason;
                }
                return SkillAnnouncer.ComposeLeveled(SkillAdapter.ReadLeveling(_panel, PointsAvailable()));
            }
        }

        public override bool ReannounceOnActivate => true;

        // As focus lands, highlight the portrait: make this skill the game's selection so its portrait
        // frame lights up and the info panel tracks it (the detail folded into the focus line is read from
        // the model, not this selection, but keeping the game's selection in step lets the level-up button
        // act on the right skill). The panel follows the mouse, not the controller, on its own, so we set
        // its selection explicitly.
        public override void OnFocused()
        {
            GameCursor.Follow(_panel.selectButton);
            var info = CharacterSheetInfoPanel.Singleton;
            if (info != null)
                info.SetSelected(_panel.TryCast<ICharsheetSelecteble>(), true);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Raise);
        }

        // Spend a skill point on this skill through the game's own level-up button. When the raise cannot
        // happen, record why so the re-announce speaks it: a capped skill gets the game's own reason (read
        // from the live skill model, since the panel's is dormant), and an otherwise-raisable skill with no
        // points gets the no-points message. The info panel's level-up button acts on the panel's selected
        // skill, so we select this skill first; running its own click handler (not a model call) does the
        // upgrade the game's way, so its level-up sound and visuals play.
        private void Raise()
        {
            if (!_panel.isUpgradeable)
            {
                _failureReason = WhyCantRaise();
                return;
            }
            if (!PointsAvailable())
            {
                _failureReason = Strings.AbilityNoPointsLeft;
                return;
            }
            var info = CharacterSheetInfoPanel.Singleton;
            if (info == null || info.levelUpButton == null)
            {
                _host.LogWarning("LevelUpSkillCell: no info panel level-up button; cannot raise skill.");
                return;
            }
            GameCursor.Follow(_panel.selectButton);
            info.SetSelected(_panel.TryCast<ICharsheetSelecteble>(), true);
            info.levelUpButton.onClick.Invoke();
        }

        // The game's localized reason a skill cannot be raised (e.g. "Skill cap reached"), read from the
        // live skill model on the character (the panel's own currentSkill is dormant in play). Null when the
        // model is unavailable, leaving the re-announce to fall back to the value.
        private string WhyCantRaise()
        {
            var skill = CharsheetView.Singleton?.character?.GetSkill(_panel.skill);
            return skill != null ? LevelingUtils.GetWhySkillCantBeUpgraded(skill) : null;
        }

        // Whether the player has a skill point that can be spent on some skill right now (the game's own
        // check, which accounts for both the point pool and at least one upgradeable skill).
        private static bool PointsAvailable() => LevelingUtils.CanBuySomeSkillWithSkillPoints();
    }
}
