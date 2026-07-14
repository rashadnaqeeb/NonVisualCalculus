using System.Collections.Generic;
using NonVisualCalculus.Core.Settings;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A navigable toggle for one boolean mod setting. Reads the live value at announce time and, on
    /// activate, flips and persists it; the navigator then re-announces the new state ("on"/"off").
    /// </summary>
    public sealed class SettingToggleCell : UIElement
    {
        private readonly ToggleSetting _setting;

        public SettingToggleCell(ToggleSetting setting) => _setting = setting;

        public override string Label => _setting.Label;
        public override string Role => Strings.ControlToggle;
        public override string Value => _setting.Value ? Strings.StatusOn : Strings.StatusOff;
        public override bool ReannounceOnActivate => true;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, () => _setting.Toggle());
        }
    }
}
