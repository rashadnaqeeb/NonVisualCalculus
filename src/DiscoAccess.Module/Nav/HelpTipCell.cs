using System.Collections.Generic;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// One tip card on the F1 help overlay: a category title followed by its description lines. The overlay
    /// is a single baked sprite with no live text, so the wording is read from the game's own localized
    /// help terms (the <c>SETTINGS_HELP_*</c> I2 terms that also back the settings-menu tips), resolved live
    /// each focus. Read-only; advertises no actions.
    /// </summary>
    internal sealed class HelpTipCell : UIElement
    {
        private readonly string _titleTerm;
        private readonly string[] _descTerms;

        public HelpTipCell(string titleTerm, params string[] descTerms)
        {
            _titleTerm = titleTerm;
            _descTerms = descTerms;
        }

        public override string Label => TextFilter.Clean(GameLocalization.Translate(_titleTerm));

        // The title as one clause, then each description line as its own, so the card reads as a short
        // sentence run. Composed here (not via Label/Value) because a card carries several body lines.
        public override string GetFocusText()
        {
            var parts = new List<string>(_descTerms.Length + 1);
            string title = Label;
            if (!string.IsNullOrEmpty(title))
                parts.Add(title);
            foreach (string term in _descTerms)
            {
                string line = TextFilter.Clean(GameLocalization.Translate(term));
                if (!string.IsNullOrEmpty(line))
                    parts.Add(line);
            }
            return string.Join(". ", parts);
        }
    }
}
