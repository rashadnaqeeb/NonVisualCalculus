using System;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A menu of the mod's own (the F12 settings menu, the Ctrl+B bookmarks menu): it floats above the
    /// game and maps to no game <see cref="Sunshine.Views.ViewType"/>. The <see cref="ScreenManager"/>
    /// drives the active one ahead of the view system and the popup overlay, owns the keyboard while it
    /// stands, and closes it on Escape or its own toggle key; the screen underneath stays attached and
    /// resumes on close. Built fresh on each open from live state.
    /// </summary>
    public abstract class ModOverlay
    {
        /// <summary>Spoken when the overlay opens; the landing control then queues behind.</summary>
        public abstract string Title { get; }

        /// <summary>Build the navigable tree from live state. <paramref name="onClose"/> closes the
        /// overlay (wire it to the root's Back action - see <see cref="OverlayRoot"/>).</summary>
        public abstract Container BuildRoot(IModHost host, Action onClose);

        /// <summary>Called every frame while the overlay stands, after the first build. An overlay with
        /// dynamic content overrides it to rebuild in place and re-home focus, returning whether focus
        /// was re-homed so the ScreenManager announces the landing once - the same contract as
        /// <see cref="Screen.OnUpdate"/>, announcing included. The default does nothing.</summary>
        public virtual bool OnUpdate(IModHost host, TraditionalNavigator nav) => false;
    }
}
