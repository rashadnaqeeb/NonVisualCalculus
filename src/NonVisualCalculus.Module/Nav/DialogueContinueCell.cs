using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The navigable Continue affordance shown below the current line when a conversation has no response
    /// choices but can be advanced (an NPC line or chain the player steps through). DE's own continue is an
    /// image-only control, so this is the mod's authored button: the player presses Down from the current
    /// line to reach it and Enter to advance, the same shape as choosing a response. Focusable only while a
    /// continue is actually available (read live), so it drops out the instant the conversation moves on.
    /// </summary>
    internal sealed class DialogueContinueCell : UIElement
    {
        private readonly Func<bool> _available;
        private readonly Action _continue;

        public DialogueContinueCell(Func<bool> available, Action continueAction)
        {
            _available = available;
            _continue = continueAction;
        }

        public override bool CanFocus => _available();
        public override string Label => Strings.DialogueContinue;
        public override string Role => Strings.RoleButton;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, _continue);
        }
    }
}
