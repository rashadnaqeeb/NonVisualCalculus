using System;
using System.Collections.Generic;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// The live registry of everything in the current area - one stable <see cref="IWorldItem"/> proxy per
    /// thing present, regardless of fog or reachability (consumers apply <see cref="IWorldItem.IsVisible"/>
    /// and <see cref="IWorldItem.IsAccessible"/> themselves). The sonar, scanner, and cursor read
    /// <see cref="Items"/>; the Module backs it with a poll-and-diff over the game's entity pools, so the
    /// proxy identities stay stable across frames - which is what lets a consumer attach to a thing (e.g. a
    /// looping sound) and follow it via <see cref="Added"/>/<see cref="Removed"/> rather than re-scanning.
    /// </summary>
    public interface IWorldModel
    {
        IReadOnlyCollection<IWorldItem> Items { get; }

        /// <summary>Raised when a thing enters the area (a new proxy is created).</summary>
        event Action<IWorldItem> Added;

        /// <summary>Raised when a thing leaves (despawned, or the area changed).</summary>
        event Action<IWorldItem> Removed;
    }
}
