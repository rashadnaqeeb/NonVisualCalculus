using System;
using System.Collections.Generic;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// A pure sensing provider attached to the <see cref="Overlay"/>: it queries the world relative to the
    /// cursor and either contributes <see cref="OverlayAnnouncement"/>s or makes sound in <see cref="Tick"/>.
    /// It never moves the cursor and never reads input — that is the cursor's job — which is what lets
    /// several systems coexist on one cursor. Exactly one instance of each concrete type per overlay, so a
    /// sibling can be looked up by type.
    ///
    /// The system's play mode is read live through a provider the host binds to a setting
    /// (<see cref="BindMode"/>), so flipping it in the menu takes effect immediately; a held hotkey can force
    /// it on for a frame (<see cref="ForceHeld"/>).
    /// </summary>
    public abstract class OverlaySystem
    {
        /// <summary>Authored display name (settings menu, mode announcements).</summary>
        public abstract string Name { get; }

        /// <summary>Stable settings-path segment, e.g. "sonar" (never spoken).</summary>
        public abstract string Key { get; }

        private Func<PlayMode> _mode = () => PlayMode.Off;

        /// <summary>Bind the live play-mode source (the host wires it to a setting). Until bound, the system
        /// reads <see cref="PlayMode.Off"/>.</summary>
        public void BindMode(Func<PlayMode> provider)
        {
            if (provider != null) _mode = provider;
        }

        /// <summary>The current play mode, read live.</summary>
        public PlayMode Mode => _mode();

        /// <summary>The play modes this system offers (its mode cycle / menu choices). Default: all three;
        /// a pure readout with no movement-timed playback overrides to Off/Continuous.</summary>
        public virtual IReadOnlyList<PlayMode> SupportedModes => AllModes;

        private static readonly PlayMode[] AllModes = { PlayMode.Off, PlayMode.WhenMoving, PlayMode.Continuous };

        /// <summary>Transient "force on while a hotkey is held": plays as if Continuous, never persisted.
        /// Set each frame by the input layer.</summary>
        public bool ForceHeld { get; set; }

        /// <summary>Active at all — for readout/announce systems with no movement-timed playback.</summary>
        public bool Enabled => Mode != PlayMode.Off || ForceHeld;

        /// <summary>The audio play gate: Continuous always, WhenMoving only while the cursor moved recently,
        /// Off never; a held hotkey forces it on.</summary>
        public bool ShouldPlay(Overlay overlay)
        {
            if (ForceHeld) return true;
            switch (Mode)
            {
                case PlayMode.Continuous: return true;
                case PlayMode.WhenMoving: return overlay != null && overlay.CursorMovingRecently;
                default: return false;
            }
        }

        /// <summary>Called when the overlay engages / disengages — for systems that hold audio voices.</summary>
        public virtual void OnEnter(Overlay overlay) { }
        public virtual void OnExit(Overlay overlay) { }

        /// <summary>Per-frame work while the overlay is engaged (continuous sound). Self-gates on
        /// <see cref="ShouldPlay"/>.</summary>
        public virtual void Tick(float dt, Overlay overlay) { }

        /// <summary>The announcements this system contributes for the request, each tagged with its context;
        /// empty if disabled or it doesn't serve the requested context.</summary>
        public virtual IEnumerable<OverlayAnnouncement> Announce(OverlayContext ctx)
        {
            yield break;
        }
    }
}
