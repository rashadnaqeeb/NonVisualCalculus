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
            // A check response reads its breakdown after the option text. The breakdown is built against the
            // label so it can drop what the label's own check tag already states (see CheckBreakdown). Join
            // with a sentence break, but not a second period when the label already ends one (DE's answer
            // text is a full sentence), so it does not read "clipboard.. white check".
            string breakdown = CheckBreakdown(label);
            if (!string.IsNullOrEmpty(breakdown))
                label = string.IsNullOrEmpty(label) ? breakdown : label + SentenceJoin(label) + breakdown;
            return TextFilter.Clean(label);
        }

        // The skill-check breakdown for this response, read live from the game's own check computation, or
        // null when the response carries no check. Reads "<colour> check, <odds>%" then "modifiers:
        // <condition> <signed bonus>, ..." - the conditions that raise or lower the check, the same ones DE
        // shows in its check tooltip. DE's answer label already opens with a check tag naming the skill and
        // difficulty tier (e.g. "[Interfacing - Medium 10] ..."); when that tag is present we read only what
        // it omits (colour, odds, modifiers) so the skill and difficulty are not spoken twice. When the tag
        // is absent (its display is gated by dialogue settings) the skill and difficulty lead the breakdown.
        private string CheckBreakdown(string label)
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
            string skill = check.SkillName();
            bool tagInLabel = !string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(skill) && label.Contains(skill);
            string head = tagInLabel ? colour : skill + " " + colour;
            if (!tagInLabel)
            {
                string difficulty = check.difficulty;
                if (!string.IsNullOrEmpty(difficulty))
                    head += ", " + difficulty;
            }
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
                    // The game's explanation is a full phrase ending in a period; dropping it keeps the
                    // period from interrupting before the bonus, so the modifier reads "<condition> +N".
                    explanation = explanation.TrimEnd().TrimEnd('.', ',', ';', ':');
                    // These modify the target, so a positive bonus raises the bar (a hindrance) and a
                    // negative one lowers it (a help). Speak the effect on the player's check, not the raw
                    // target delta: negate so a help reads "+N" and a hindrance "-N", as DE colours them.
                    int effect = -m.bonus;
                    lines.Add(explanation + " " + (effect >= 0 ? "+" : "") + effect);
                }
                if (lines.Count > 0)
                    parts.Add(Strings.CheckModifiers + ": " + string.Join(", ", lines));
            }
            return string.Join(". ", parts);
        }

        // The separator between DE's answer label and our check breakdown: a bare space when the label
        // already ends a sentence (its own period supplies the pause), else a period to break them apart.
        private static string SentenceJoin(string label)
        {
            string t = label.TrimEnd();
            char last = t.Length > 0 ? t[t.Length - 1] : '\0';
            return last == '.' || last == '!' || last == '?' || last == ':' ? " " : ". ";
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            if (_response != null && _response.enabled)
                yield return new ElementAction(ActionIds.Activate, _select);
        }
    }
}
