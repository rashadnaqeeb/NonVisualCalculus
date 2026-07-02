namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// The plain data an inventory item cell speaks, extracted from the live game item by the module's
    /// adapter and composed into the spoken line by <see cref="InventoryItemAnnouncer"/>. No Unity types,
    /// so the composition stays unit-testable off-engine (the adapter/composition split).
    /// </summary>
    public sealed class InventoryItemState
    {
        /// <summary>The item's localized display name, the distinguishing part spoken first.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Newly acquired (the game's "fresh" marker), spoken as "new".</summary>
        public bool IsFresh { get; set; }

        /// <summary>Remaining uses for a consumable, or null when the item is not a counted consumable.</summary>
        public int? Uses { get; set; }

        /// <summary>Pawn value in centims, spoken only when positive (the pawnables tab).</summary>
        public int Value { get; set; }

        /// <summary>The game's label heading the pawn price in the pawnshop ("Pawn for"), or null outside
        /// it. When set, the value is spoken as this offer ("Pawn for 13.13 réal") instead of the plain
        /// "value" marker, matching the priced button on screen.</summary>
        public string? PawnLabel { get; set; }

        /// <summary>What the item does when equipped or used: its effects already formatted and joined by the
        /// adapter (e.g. "+1 Reaction Speed: Limitless grip, -1 Encyclopedia: Too fast for facts"), or null.</summary>
        public string? Effects { get; set; }

        /// <summary>The item's full description prose, or null.</summary>
        public string? Description { get; set; }
    }
}
