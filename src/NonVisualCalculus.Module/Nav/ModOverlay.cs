using System;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
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

        /// <summary>Called once when the overlay leaves, on every close path - Escape, its toggle key, a
        /// swap to another overlay, module teardown. An overlay holding something live (a sounding
        /// preview) releases it here, so nothing keeps running with its menu gone. The default does
        /// nothing.</summary>
        public virtual void OnClosed() { }
    }
}
