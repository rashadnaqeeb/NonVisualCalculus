using System;
using System.Collections.Generic;
using DiscoAccess.Core.World;
using FortressOccident;
using UnityEngine;
using Object = UnityEngine.Object; // the registry keys are Unity objects; disambiguate from System.Object

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The live registry of everything in the current area: one stable proxy per entity and orb, rebuilt by
    /// a poll-and-diff each frame against the game's pools (<see cref="BasicEntity.sceneEntitySet"/> and the
    /// active <see cref="SenseOrb"/>s). One proxy instance per game object is kept across frames so a consumer
    /// can hold a proxy and keep reading it. Keyed by <see cref="Object.GetInstanceID"/>, not the Unity object
    /// itself: <c>FindObjectsOfType</c> hands back a fresh managed wrapper each call, and those wrappers do not
    /// compare equal in a dictionary keyed by the object (unlike the persistent <c>sceneEntitySet</c> ones), so
    /// keying by the object would rebuild every orb's proxy each poll - churning the reference the cursor cue
    /// compares by and machine-gunning its enter click. The instance id is stable per native object. Not
    /// fog-filtered; consumers apply IsVisible/IsAccessible.
    /// </summary>
    internal sealed class WorldModel : IWorldModel
    {
        // The registry refreshes at this cadence, not every frame: the per-frame cost is dominated by
        // FindObjectsOfType (a full active-scene scan), and membership need not be frame-fresh - proxies
        // read their own live state on demand, and the consumers (sonar sweeps, the scanner) act on the
        // order of seconds. A tenth of a second of membership lag is imperceptible and cuts the scan rate ~6x.
        private const float PollInterval = 0.1f;

        private readonly Dictionary<int, IWorldItem> _items = new Dictionary<int, IWorldItem>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly Action<string> _log; // handed to each entity proxy for its self-diagnostics
        private float _sincePoll = PollInterval; // poll on the first tick

        public WorldModel(Action<string> log) { _log = log; }

        public IReadOnlyCollection<IWorldItem> Items => _items.Values;

        public event Action<IWorldItem> Added;
        public event Action<IWorldItem> Removed;

        /// <summary>Poll the game's pools and diff against the held set (throttled to <see cref="PollInterval"/>),
        /// building a proxy only for a genuinely new object and dropping any that despawned or left when the
        /// area changed.</summary>
        public void Tick(float dt)
        {
            _sincePoll += dt;
            if (_sincePoll < PollInterval) return;
            _sincePoll = 0f;

            _present.Clear();

            // sceneEntitySet is an Il2Cpp list; index it rather than relying on a BCL enumerator.
            var entities = BasicEntity.sceneEntitySet;
            if (entities != null)
                for (int i = 0; i < entities.Count; i++)
                {
                    BasicEntity e = entities[i];
                    if (e == null) continue;
                    // An OrbUiElement is the canvas element the game draws for an active SenseOrb, and it
                    // registers in sceneEntitySet like any BasicEntity. The orb itself is already tracked
                    // below through its OrbProxy (named, gated, and cleared on trigger), so its UI twin
                    // would only duplicate it as a junk-named interactable.
                    if (e.TryCast<OrbUiElement>() != null) continue;
                    Track(e, () => new EntityProxy(e, _log));
                }

            foreach (SenseOrb orb in Object.FindObjectsOfType<SenseOrb>())
            {
                if (orb == null) continue;
                Track(orb, () => new OrbProxy(orb));
            }

            _gone.Clear();
            foreach (int key in _items.Keys) if (!_present.Contains(key)) _gone.Add(key);
            for (int i = 0; i < _gone.Count; i++)
            {
                int key = _gone[i];
                IWorldItem item = _items[key];
                _items.Remove(key);
                Removed?.Invoke(item);
            }
        }

        // Mark a game object present, building (and announcing) a proxy only the first time it is seen. Keyed
        // by the native instance id (stable across the fresh wrappers FindObjectsOfType returns each poll).
        private void Track(Object key, Func<IWorldItem> make)
        {
            int id = key.GetInstanceID();
            if (!_items.ContainsKey(id))
            {
                IWorldItem item = make();
                _items[id] = item;
                Added?.Invoke(item);
            }
            _present.Add(id);
        }
    }
}
