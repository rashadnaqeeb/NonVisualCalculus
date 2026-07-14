using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The popup's body text as a focusable, read-only line, so the prompt is reachable and re-readable on
    /// its own rather than only spoken once on entry. Reads the live message (the same natural-case text the
    /// popup announces), never cached.
    /// </summary>
    public sealed class PopupMessage : UIElement
    {
        public override bool CanFocus => PopupOverlay.IsShowing();
        public override string Label => PopupOverlay.Message();
    }
}
