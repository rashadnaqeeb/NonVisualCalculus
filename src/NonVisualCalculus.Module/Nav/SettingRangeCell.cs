using System.Collections.Generic;
using NonVisualCalculus.Core.Settings;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A navigable slider for one numeric mod setting. Reads the live value at announce time in the
    /// setting's own unit (a percentage, a millisecond duration) and steps it on Left/Right
    /// (decrease/increase); the navigator re-announces the new value, or names the bound
    /// ("minimum"/"maximum") when a step at the end moved nothing.
    /// </summary>
    public sealed class SettingRangeCell : UIElement
    {
        private readonly RangeSetting _setting;

        public SettingRangeCell(RangeSetting setting) => _setting = setting;

        public override string Label => _setting.Label;
        public override string Role => Strings.ControlSlider;
        public override string Value => _setting.Unit == RangeUnit.Milliseconds
            ? Strings.Milliseconds(_setting.Value)
            : Strings.Percent(_setting.Value);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Decrease, () => _setting.Decrease());
            yield return new ElementAction(ActionIds.Increase, () => _setting.Increase());
        }
    }
}
