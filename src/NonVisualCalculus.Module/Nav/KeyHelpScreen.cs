using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The mod's key-help screen (Shift+F1): one line per key live in the context the player pressed it
    /// in, as a vertical list to arrow through (type-ahead works, so typing "time" lands on the time
    /// readout's line). The lines are composed ONCE, at the keypress (see UiModule.ToggleKeyHelp):
    /// opening this overlay replaces the live key set with its own, so the moment of the press is the
    /// only time the context being asked about can be read - a deliberate exception to reading live.
    /// Closed on Escape or Shift+F1 again.
    /// </summary>
    internal sealed class KeyHelpScreen : ModOverlay
    {
        private readonly IReadOnlyList<string> _lines;

        public KeyHelpScreen(IReadOnlyList<string> lines) => _lines = lines;

        public override string Title => Strings.ScreenKeyHelp;

        public override Container BuildRoot(IModHost host, Action onClose)
        {
            var root = new OverlayRoot(onClose);
            var list = new Container(ContainerShape.VerticalList);
            foreach (string line in _lines)
                list.Add(new ReadonlyTextCell(() => line));
            root.Add(list);
            return root;
        }
    }
}
