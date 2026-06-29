using System;
using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using PixelCrushers.DialogueSystem; // Response
using Sunshine.Metric;              // CheckResult, CheckModifier, CheckType
using ConversationLogger = Sunshine.ConversationLogger;
// WhiteCheckNode / RedCheckNode live in the global namespace.

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// One player response in the conversation, read live (never cached). The label is the game's own
    /// formatted answer text - which folds in the skill-check tag and odds when the response carries a check,
    /// or a "[Locked]" prefix when a check is not yet open, gated by the same dialogue settings the on-screen
    /// button uses - cleaned for speech. A disabled response stays focusable so it is announced (DE shows
    /// locked checks and other unavailable options to a sighted player, and a locked check is a hint worth
    /// surfacing), but only an enabled one advertises the activate action; activation goes through the game's
    /// selection path, which advances the conversation and plays its own sound, so the next line announces
    /// itself.
    /// </summary>
    internal sealed class DialogueResponseCell : UIElement
    {
        private readonly ConversationLogger _logger;
        private readonly Response _response;
        private readonly int _number;
        private readonly Action _select;

        public DialogueResponseCell(ConversationLogger logger, Response response, int number, Action select)
        {
            _logger = logger;
            _response = response;
            _number = number;
            _select = select;
        }

        public override bool CanFocus => _response != null;

        public override string GetFocusText()
        {
            // The game's own answer formatter carries the numbered prefix and any locked tag; the counter
            // matches the on-screen ordering. Fall back to the raw response text if it is unavailable.
            string label = _logger != null
                ? _logger.FormatResponse(_number, _logger.ChooseResponseText(_response))
                : _response?.formattedText?.text;
            // A check response reads its breakdown after the option text: skill, colour, difficulty and odds
            // lead; the modifiers that feed the check come last.
            string breakdown = CheckBreakdown();
            if (!string.IsNullOrEmpty(breakdown))
                label = string.IsNullOrEmpty(label) ? breakdown : label + ". " + breakdown;
            return TextFilter.Clean(label);
        }

        // The skill-check breakdown for this response, read live from the game's own check computation, or
        // null when the response carries no check. Reads "<skill> <colour> check, <difficulty>, <odds>%" then
        // "modifiers: <condition> <signed bonus>, ..." - the conditions that raise or lower the check, the
        // same ones DE shows in its check tooltip. The raw target number is not read (a sighted player sees
        // the difficulty tier, not the number).
        private string CheckBreakdown()
        {
            DialogueEntry entry = _response != null ? _response.destinationEntry : null;
            if (entry == null)
                return null;
            CheckResult check = WhiteCheckNode.IsWhiteCheckNode(entry) ? WhiteCheckNode.GetCheck(entry)
                : RedCheckNode.IsRedCheckNode(entry) ? RedCheckNode.GetCheck(entry)
                : null;
            if (check == null)
                return null;

            string colour = check.checkType == CheckType.RED ? Strings.CheckRed : Strings.CheckWhite;
            string head = check.SkillName() + " " + colour;
            string difficulty = check.difficulty;
            if (!string.IsNullOrEmpty(difficulty))
                head += ", " + difficulty;
            head += ", " + (int)Math.Round(check.Probability() * 100) + "%";

            var parts = new List<string> { head };
            // Only modifiers currently in effect, the ones the player has earned. Potential modifiers tied to
            // conditions not yet met (in allTargetModifiers but not these) are not shown - the game hides
            // unearned modifiers, so a locked check with nothing met reads no modifier list at all.
            var mods = check.applicableTargetModifiers;
            if (mods != null && mods.Count > 0)
            {
                var lines = new List<string>();
                for (int i = 0; i < mods.Count; i++)
                {
                    CheckModifier m = mods[i];
                    string explanation = m != null ? m.explanation : null;
                    if (string.IsNullOrEmpty(explanation))
                        continue;
                    int bonus = m.bonus;
                    lines.Add(explanation + " " + (bonus >= 0 ? "+" : "") + bonus);
                }
                if (lines.Count > 0)
                    parts.Add(Strings.CheckModifiers + ": " + string.Join(", ", lines));
            }
            return string.Join(". ", parts);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            if (_response != null && _response.enabled)
                yield return new ElementAction(ActionIds.Activate, _select);
        }
    }
}
