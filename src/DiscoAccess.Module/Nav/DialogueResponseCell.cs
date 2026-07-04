using System;
using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI;          // InventoryItemAnnouncer
using DiscoAccess.Core.UI.Nav;
using PixelCrushers.DialogueSystem; // Response
using Sunshine.Metric;              // CheckResult, CheckModifier, CheckType, InventoryItem, PlayerCharacter
using ConversationLogger = Sunshine.ConversationLogger;
// WhiteCheckNode / RedCheckNode / FakeCheckNode / CostOptionNode live in the global namespace.

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// One player response in the conversation, read live (never cached). The label is the game's own
    /// formatted answer text - which folds in the skill-check tag and odds when the response carries a check,
    /// a "[Locked]" prefix when a check is not yet open (gated by the same dialogue settings the on-screen
    /// button uses), or the formatted cost and afford/paid word on a money option - cleaned for speech. A
    /// check response then reads its breakdown (see <see cref="CheckBreakdown"/>) and a money option its
    /// wallet-and-item breakdown (see <see cref="CostBreakdown"/>), each composed to add only what the label
    /// omits. A disabled response stays focusable so it is announced (DE shows locked checks and other
    /// unavailable options to a sighted player, and a locked check is a hint worth surfacing), but only an
    /// enabled one advertises the activate action; activation goes through the game's selection path, which
    /// advances the conversation and plays its own sound, so the next line announces itself.
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
            // The game's answer text can arrive display-shaped (RTL); restore logical order before the
            // breakdowns compose around it and compare against it (the skill-tag and cost checks below
            // match logical game strings).
            label = RtlText.Unfix(label);
            // A check or money response reads its breakdown after the option text (the two node types are
            // disjoint, so at most one applies). Each breakdown is built against the label so it can drop
            // what the label already states (see CheckBreakdown / CostBreakdown). Join with a sentence
            // break, but not a second period when the label already ends one (DE's answer text is a full
            // sentence), so it does not read "clipboard.. white check".
            string breakdown = CheckBreakdown(label) ?? CostBreakdown(label);
            if (!string.IsNullOrEmpty(breakdown))
                label = string.IsNullOrEmpty(label) ? breakdown : label + SentenceJoin(label) + breakdown;
            // An option the player has already picked before is dimmed on screen; speak that last, after
            // any breakdown, so the distinguishing option text leads and the status trails. Only a chosen
            // response (its node displayed) earns it - one merely offered before reads plain, as DE draws it.
            DialogueEntry entry = _response != null ? _response.destinationEntry : null;
            if (entry != null && SunshineNode.IsSeen(entry))
                label = string.IsNullOrEmpty(label)
                    ? Strings.DialogueAlreadyChosen
                    : label + SentenceJoin(label) + Strings.DialogueAlreadyChosen;
            return TextFilter.Clean(label);
        }

        // The skill-check breakdown for this response, read live from the game's own check computation, or
        // null when the response carries no check. Reads "<colour> check, <odds>%, skill level <n>" then
        // "modifiers: <condition> <signed bonus>, ..." - the odds, the player's skill level (the character
        // sheet's total: ability plus learned points plus item and thought bonuses), and the conditions
        // that raise or lower the check. The skill level and modifiers are raw inputs, spoken unfolded;
        // the odds carry the combined arithmetic. DE's answer label already opens with a check tag
        // naming the skill, difficulty tier, and target number (e.g. "[Interfacing - Medium 10] ..."); when
        // that tag is present we read only what it omits so nothing is spoken twice. When the tag is absent
        // (its display is gated by dialogue settings) the skill, then the difficulty with its target
        // number, lead the breakdown instead. A fake check (the game's scripted atmosphere check) is
        // tooltipped and styled as a red check, so it reads the same way.
        private string CheckBreakdown(string label)
        {
            DialogueEntry entry = _response != null ? _response.destinationEntry : null;
            if (entry == null)
                return null;
            CheckResult check = WhiteCheckNode.IsWhiteCheckNode(entry) ? WhiteCheckNode.GetCheck(entry)
                : RedCheckNode.IsRedCheckNode(entry) ? RedCheckNode.GetCheck(entry)
                : FakeCheckNode.IsFakeCheckNode(entry) ? FakeCheckNode.TransformCheck(entry)
                : null;
            if (check == null)
                return null;

            string colour = check.checkType == CheckType.RED ? Strings.CheckRed : Strings.CheckWhite;
            string skill = RtlText.Unfix(check.SkillName());
            bool tagInLabel = !string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(skill) && label.Contains(skill);
            string head = tagInLabel ? colour : skill + " " + colour;
            if (!tagInLabel)
            {
                string difficulty = RtlText.Unfix(check.difficulty);
                if (!string.IsNullOrEmpty(difficulty))
                    head += ", " + difficulty + " " + check.baseTarget;
            }
            head += ", " + (int)Math.Round(check.Probability() * 100) + "%";

            // Only modifiers currently in effect, the ones the player has earned. Potential modifiers tied to
            // conditions not yet met (in allTargetModifiers but not these) are not shown - the game hides
            // unearned modifiers, so a locked check with nothing met reads no modifier list at all.
            var mods = check.applicableTargetModifiers;

            head += ", " + Strings.CheckSkillLevelOf(check.SkillValue());

            var parts = new List<string> { head };
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
                    explanation = RtlText.Unfix(explanation).TrimEnd().TrimEnd('.', ',', ';', ':');
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

        // The money breakdown for a cost option (a purchase, a bribe, the room), or null when the response
        // carries no cost. The game's label already folds in its own formatted cost and an
        // afford/locked/paid status word, so the amount is spoken here only when its digits are missing
        // from the label; what the label never carries is added: the wallet ("you have <money>", so
        // affordability is audible) and, for a purchase, the item's detail (name, effects, description),
        // read from the same data the button's hover tooltip shows a sighted player. The cost and you-have
        // words are the game's own tooltip terms so they localize.
        private string CostBreakdown(string label)
        {
            DialogueEntry entry = _response != null ? _response.destinationEntry : null;
            if (entry == null || !CostOptionNode.IsCostOptionNode(entry))
                return null;

            var parts = new List<string>(3);
            int cost = CostOptionNode.GetCost(entry);
            if (!ContainsMoney(label, cost))
                parts.Add(GameLocalization.Term("TOOLTIP_COST", Strings.CostWord) + " " + Strings.WorldMoney(cost));
            parts.Add(GameLocalization.Term("TOOLTIP_YOU_HAVE", Strings.CostYouHave) + " "
                + Strings.WorldMoney(PlayerCharacter.Singleton.Money));

            CostTooltipData data = DialogueAdapter.CostData(_response);
            InventoryItem item = data != null ? data.item : null;
            if (item != null)
                parts.Add(InventoryItemAnnouncer.Compose(InventoryAdapter.ReadLoot(item)));
            return string.Join(". ", parts);
        }

        // Whether the label already carries the cost amount (the game renders it as a currency glyph plus
        // the centims over 100, "0.90"), checked against both decimal separators its culture formatter
        // produces, so the amount is never read twice.
        private static bool ContainsMoney(string label, int centims)
        {
            if (string.IsNullOrEmpty(label))
                return false;
            string amount = (centims / 100) + "." + (centims % 100).ToString("D2");
            return label.Contains(amount) || label.Contains(amount.Replace('.', ','));
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
