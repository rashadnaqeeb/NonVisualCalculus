using System;
using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A mod-authored menu button: a label (resolved at speak time, so a language switch lands) and an
    /// activate action, spoken with the button role like the game buttons it sits among.
    /// </summary>
    internal sealed class ModActionButton : UIElement
    {
        private readonly Func<string> _label;
        private readonly Action _activate;

        public ModActionButton(Func<string> label, Action activate)
        {
            _label = label;
            _activate = activate;
        }

        public override string Label => _label();
        public override string Role => Strings.RoleButton;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, _activate);
        }
    }
}
