using DiscoAccess.Core.World;
using FortressOccident;
using Sunshine;
using Vector3 = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The <see cref="IWorldItem"/> over a live <see cref="BasicEntity"/> (NPC, door, exit, container,
    /// prop). Reads everything live and classifies by the game's own <see cref="Interactable"/> subclass
    /// tree via <c>TryCast</c> (not GetType, which the interop boxes to BasicEntity).
    /// </summary>
    internal sealed class EntityProxy : IWorldItem
    {
        private readonly BasicEntity _e;

        public EntityProxy(BasicEntity e) { _e = e; }

        // The spoken name, composed by Core from the raw fields: a clean GameObject.name, else a
        // spoiler-filtered conversation title for a non-character slug, else a generic type word. A Character
        // is treated as a named thing so its conversation title is never spoken in its place.
        public string Name => EntityNaming.Resolve(_e.name, _e.conversation, _e.TryCast<Character>() != null, Category);
        public Vector3 Position => WorldConvert.ToSnv(_e.transform.position);
        public ScanBounds Bounds => ScanBounds.Point(Position);
        public string Category => Classify(_e);
        public bool IsAccessible => _e.IsAccessible;
        public bool IsVisible => true; // present in the scene; fog/streaming refinement comes later

        // The interaction stand-point and the reachability oracle, both approach-relative (computed from the
        // querying position). GameEntity, which BasicEntity derives from, supplies both; the from-position
        // becomes the Formation.Location the game measures the approach from.
        public Vector3 InteractionPoint(Vector3 from)
            => WorldConvert.ToSnv(_e.GetInteractionLocation(LocationAt(from)).position);

        public bool IsActionable(Vector3 from) => _e.CheckIfCanCreatePathToHavePath(LocationAt(from));

        // Extra facts the Enter walk-then-interact verb needs beyond the sensing contract: the stand-point's
        // facing (so the character ends up looking the right way) and the game's own arrival-range test. Kept
        // here so the game-call and Unity<->Numerics conversion stay inside the proxy boundary.
        internal Vector3 Approach(Vector3 from, out float heading)
        {
            Formation.Location loc = _e.GetInteractionLocation(LocationAt(from));
            heading = loc.heading;
            return WorldConvert.ToSnv(loc.position);
        }

        internal bool WithinInteractionRadius(Vector3 playerPos)
            => _e.IsWithinInteractionRadius(WorldConvert.ToUnity(playerPos));

        private static Formation.Location LocationAt(Vector3 from)
            => new Formation.Location(WorldConvert.ToUnity(from), 0f);

        public bool Interact() => _e.Interact(new Interactable.ClickEventData());

        // Map the entity's runtime type onto a taxonomy category. Order matters where types nest (Curtains
        // derives from Door). TravelDestination/Teleporter exits derive from NavMeshClickHandler, not
        // BasicEntity, so they are not in sceneEntitySet and not seen here; TransitionEntity is.
        private static string Classify(BasicEntity e)
        {
            if (e.TryCast<Character>() != null) return WorldTaxonomy.Npc;
            if (e.TryCast<Door>() != null) return WorldTaxonomy.Door;
            if (e.TryCast<TransitionEntity>() != null) return WorldTaxonomy.Exit;
            if (e.TryCast<ContainerSource>() != null) return WorldTaxonomy.Container;
            return WorldTaxonomy.Other;
        }
    }
}
