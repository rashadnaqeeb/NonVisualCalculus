using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Text;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// One line of the conversation transcript, read live from a rendered <c>LogEntry</c> (never cached): a
    /// spoken line reads "speaker: line" from the game's own <c>FinalEntry</c> in natural case (with any
    /// trailing comment), and a check/annotation row falls back to the rendered paragraph text. A line that
    /// resolved a skill check folds in the game's own result tag ("Medium: Success") - the difficulty tier
    /// and outcome a sighted player sees, never the raw target number. Cleaned for speech through the shared
    /// text filter (rich-text colour stripped). The current line (the last entry) is given a continue action
    /// so Enter advances the conversation when a continue is the only way forward; earlier lines advertise
    /// nothing and are pure scrollback the player walks with Up.
    /// </summary>
    internal sealed class DialogueLineCell : UIElement
    {
        private readonly LogEntry _entry;
        private readonly Func<bool> _continueAvailable;
        private readonly Action _continue;

        public DialogueLineCell(LogEntry entry, Func<bool> continueAvailable = null, Action continueAction = null)
        {
            _entry = entry;
            _continueAvailable = continueAvailable;
            _continue = continueAction;
        }

        // Focusable only when there is something to say after cleaning, never when the raw text is just
        // rich-text tags or whitespace (a styled spacer or an empty annotation), so navigation never lands
        // on a line that would read as blank.
        public override bool CanFocus => !string.IsNullOrEmpty(GetFocusText());

        public override string GetFocusText() => TextFilter.Clean(Raw(_entry));

        /// <summary>The line's raw spoken form (speaker, any check tag, line, comment), shared so delivery
        /// and Up-review read a line identically. Static so the screen can compose the current line the same
        /// way it focuses it.</summary>
        internal static string Raw(LogEntry entry)
        {
            if (entry == null)
                return null;
            FinalEntry fe = entry.Entry;
            if (fe != null && !entry.isAnnotation && !string.IsNullOrEmpty(fe.spokenLine))
            {
                // The header is the speaker, plus the game's own check result tag when the line resolved a
                // check ("Composure Medium: Success") - the tag's brackets dropped, its tier:outcome colon
                // kept. A check header runs into the line with ". " so that colon does not collide with a
                // "speaker:" one.
                string head = fe.speakerName;
                bool hasCheck = fe.HasCheck;
                if (hasCheck)
                {
                    string check = fe.checkText;
                    if (!string.IsNullOrEmpty(check))
                        head = string.IsNullOrEmpty(head) ? check : head + " " + check;
                }
                string body = string.IsNullOrEmpty(head)
                    ? fe.spokenLine
                    : head + (hasCheck ? ". " : ": ") + fe.spokenLine;
                if (fe.HasComment)
                {
                    string comment = fe.GetCommentString();
                    if (!string.IsNullOrEmpty(comment))
                        body += ". " + comment;
                }
                return body;
            }
            // A check result or other annotation row: read what the game rendered (the filter strips colour).
            var lt = entry.logText;
            return lt != null ? lt.text : entry.OverrideText;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            if (_continue != null && _continueAvailable != null && _continueAvailable())
                yield return new ElementAction(ActionIds.Activate, _continue);
        }
    }
}
