using System;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A read-only navigable line whose text is produced live by a delegate (never cached); advertises no
    /// actions and is unfocusable while it has nothing to say. Used for the inventory's left stats panel -
    /// attributes, vitals, and the bonuses-from-items list - each a stop the player can land on but not act
    /// on.
    /// </summary>
    internal sealed class ReadonlyTextCell : UIElement
    {
        private readonly Func<string> _text;

        public ReadonlyTextCell(Func<string> text) => _text = text;

        public override bool CanFocus => !string.IsNullOrEmpty(_text());

        public override string GetFocusText() => _text() ?? string.Empty;
    }
}
