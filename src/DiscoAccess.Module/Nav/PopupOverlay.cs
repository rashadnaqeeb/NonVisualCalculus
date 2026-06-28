using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// DE's shared confirmation/error/quit popup (<see cref="ConfirmationController"/>) as a navigable
    /// overlay. It floats over whatever view is up rather than matching a <see cref="Sunshine.Views.ViewType"/>,
    /// so the <see cref="ScreenManager"/> resolves it by visibility ahead of the view's own screen and drives
    /// it with our navigator. The tree is the body text as its own reachable read-only line then the Confirm
    /// and (when shown) Cancel buttons; focus lands on Confirm so Enter confirms, the body is reachable by
    /// arrowing to it, and Escape cancels. Built fresh on each show and read live, never cached.
    /// </summary>
    public static class PopupOverlay
    {
        /// <summary>Whether the popup is up this frame.</summary>
        public static bool IsShowing()
        {
            var inst = ConfirmationController.Singleton;
            return inst != null && inst.IsVisible;
        }

        /// <summary>The popup's live body text - the same natural-case message via <see cref="ConfirmDialogAdapter"/>.</summary>
        public static string Message() => ConfirmDialogAdapter.TryRead();

        /// <summary>Build the navigable tree: body line, Confirm, Cancel, landing on Confirm.</summary>
        public static Container BuildRoot()
        {
            var inst = ConfirmationController.Singleton;
            var root = new PopupRoot(inst);
            root.Add(new PopupMessage());
            var confirm = new PopupButton(inst.Confirm);
            root.Add(confirm);
            root.Add(new PopupButton(inst.Cancel));
            // Land on the default action so Enter confirms at once; the body line above stays reachable.
            root.SetFocusedChild(confirm);
            return root;
        }
    }
}
