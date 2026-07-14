using System.Collections.Generic;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A screen's root Panel that advertises Back so Escape closes the screen the game's own way: it finds
    /// the live <see cref="View"/> matching the current view type and runs its <c>CloseOnEscapeKey</c>, the
    /// same handler the Escape key drives for that view. Shared by every rich screen whose root is just a
    /// closeable container.
    /// </summary>
    public class ScreenRoot : Container
    {
        public ScreenRoot() : base(ContainerShape.Panel) { }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Close);
        }

        private static void Close()
        {
            ViewType current = ViewsPagesBridge.Current;
            foreach (View v in Object.FindObjectsOfType<View>())
                if (v.GetViewType() == current)
                {
                    v.CloseOnEscapeKey();
                    return;
                }
        }
    }
}
