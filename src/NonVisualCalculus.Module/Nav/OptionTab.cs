using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A navigable tab in the options screen header (Settings / Controls). Reads its label live from the
    /// game's tab button and reports whether its content is the one currently shown. Activating it runs
    /// the game's own tab switch (<c>SettingsHeaderController.SelectSettingsView</c>/<c>SelectControlsView</c>),
    /// which swaps the visible subview; the screen's per-frame refresh then rebuilds the content panel to
    /// match. Focus stays on the tab, so the user hears the new tab is active and can Tab into its content.
    /// </summary>
    public sealed class OptionTab : UIElement
    {
        private readonly Selectable _button;
        private readonly Func<bool> _isActive;
        private readonly Action _activate;

        public OptionTab(Selectable button, Func<bool> isActive, Action activate)
        {
            _button = button;
            _isActive = isActive;
            _activate = activate;
        }

        public override bool CanFocus => _button != null && _button.isActiveAndEnabled;

        public override string Label => FocusReader.Read(_button);
        public override string Role => Strings.RoleTab;
        public override string Value => _isActive() ? Strings.StatusSelected : null;

        // Switching the tab leaves focus on it, so re-announce the value (now "selected") as confirmation.
        public override bool ReannounceOnActivate => true;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, _activate);
        }

        // Move the game's cursor to this tab as our focus lands (its selection follows ours). Select only
        // highlights; the tab switch is the separate activate.
        public override void OnFocused() => GameCursor.Follow(_button);
    }
}
