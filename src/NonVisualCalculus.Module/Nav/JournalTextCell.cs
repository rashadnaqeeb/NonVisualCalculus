using NonVisualCalculus.Core.UI.Nav;
using TMPro;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A read-only line that speaks one or two live game TMP texts joined with a comma, read live at announce
    /// time. Used for the officer profile's pieces, all of them the game's own text: the name and title
    /// header, each "label value" copotype stat row, and (when the clipboard is undiscovered) the
    /// profile-incomplete notice. Focusable only while its primary text is shown and non-empty, so a blank
    /// spacer row in the stat console drops out of navigation.
    /// </summary>
    internal sealed class JournalTextCell : UIElement
    {
        private readonly TMP_Text _primary;
        private readonly TMP_Text _secondary;

        public JournalTextCell(TMP_Text primary, TMP_Text secondary = null)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public override bool CanFocus
            => _primary != null && _primary.gameObject.activeInHierarchy && !string.IsNullOrEmpty(_primary.text);

        public override string GetFocusText()
            => NonVisualCalculus.Core.Text.SpokenLine.Join(
                _primary != null ? GameLocalization.Spoken(_primary) : null,
                _secondary != null ? GameLocalization.Spoken(_secondary) : null);
    }
}
