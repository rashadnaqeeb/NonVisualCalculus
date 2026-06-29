using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A non-focusable placeholder that pads a shorter column of a ragged <see cref="Grid"/> so every
    /// category keeps its fixed column index. The navigator skips it: it is never a landing target and a
    /// move scans past it.
    /// </summary>
    internal sealed class GapCell : UIElement
    {
        public override bool CanFocus => false;
    }
}
