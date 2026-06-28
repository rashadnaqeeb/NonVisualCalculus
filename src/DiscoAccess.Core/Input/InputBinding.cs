namespace DiscoAccess.Core.Input
{
    /// <summary>
    /// One key/button combo. Phase queries (JustPressed/Held/Released) are polled each frame by
    /// <see cref="InputManager"/>; the concrete implementation lives in the module (it polls Unity).
    /// Controller bindings can join later as a sibling implementation.
    /// </summary>
    public abstract class InputBinding
    {
        /// <summary>Human-readable combo, e.g. "Ctrl+Shift+A".</summary>
        public abstract string DisplayName { get; }

        public abstract bool JustPressed();
        public abstract bool Held();
        public abstract bool Released();

        /// <summary>Stable kind tag (e.g. "keyboard"), paired with <see cref="Serialize"/> for the chord key.</summary>
        public abstract string Type { get; }

        /// <summary>Serialize this binding's data. A binding is immutable, so this is constant.</summary>
        public abstract string Serialize();

        // Stable identity used for chord shadowing (Type + serialized data). A binding is immutable - a
        // rebind makes a new binding - so this is computed once and cached: RebuildLive runs every frame
        // and rebuilding the string per binding would dominate the per-frame allocation.
        private string? _chord;
        public string Chord => _chord ??= Type + "\n" + Serialize();
    }
}
