using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A mod-overlay root Panel that advertises Back so Escape closes the overlay through the supplied
    /// callback. Unlike <see cref="ScreenRoot"/>, it does not run a game view's CloseOnEscapeKey - a mod
    /// overlay (the settings menu) maps to no game view, so closing is the mod's own affair.
    /// </summary>
    internal sealed class OverlayRoot : Container
    {
        private readonly Action _close;

        public OverlayRoot(Action close) : base(ContainerShape.Panel) => _close = close;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, _close);
        }
    }
}
