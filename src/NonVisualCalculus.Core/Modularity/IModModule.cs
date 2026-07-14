using System;

namespace NonVisualCalculus.Core.Modularity
{
    /// <summary>
    /// The contract every reloadable feature module implements. The host loads the module assembly
    /// into a collectible load context, instantiates the one implementor, calls <see cref="Load"/>
    /// once, drives <see cref="Tick"/> from its permanent per-frame pump, and calls
    /// <see cref="IDisposable.Dispose"/> before dropping the context on reload. The module injects no
    /// IL2CPP types and owns no native handles, so it can be torn down and rebuilt without a restart.
    /// </summary>
    public interface IModModule : IDisposable
    {
        /// <summary>Capture the host (logging + speech) and wire up any Harmony patches. Called once.</summary>
        void Load(IModHost host);

        /// <summary>Run the module's per-frame work. Called once per frame by the host pump.</summary>
        void Tick();
    }
}
