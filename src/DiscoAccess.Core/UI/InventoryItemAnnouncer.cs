using System.Collections.Generic;
using DiscoAccess.Core.Strings;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken line for an inventory item from its <see cref="InventoryItemState"/>: the name
    /// first (the distinguishing part), then the markers that apply - "new" for a fresh item, the use count
    /// for a consumable, the pawn value when positive (spoken as the shop's "Pawn for" offer when the state
    /// carries the pawnshop label) - then what the item does (its effects) and its description. The effects and description ride on the item itself rather than a separate detail stop,
    /// so the player hears what a piece of gear does without a second keypress and type-ahead can match on a
    /// specific bonus.
    /// </summary>
    public static class InventoryItemAnnouncer
    {
        public static string Compose(InventoryItemState s)
        {
            var parts = new List<string>(6);
            if (!string.IsNullOrEmpty(s.Name)) parts.Add(s.Name);
            if (s.IsFresh) parts.Add(Strings.Strings.InventoryFresh);
            if (s.Uses.HasValue) parts.Add(Strings.Strings.ItemUses(s.Uses.Value));
            if (s.Value > 0)
                parts.Add(string.IsNullOrEmpty(s.PawnLabel)
                    ? Strings.Strings.ItemValue(s.Value)
                    : s.PawnLabel + " " + Strings.Strings.WorldMoney(s.Value));
            if (!string.IsNullOrEmpty(s.Effects)) parts.Add(s.Effects!);
            if (!string.IsNullOrEmpty(s.Description)) parts.Add(s.Description!);
            return string.Join(", ", parts);
        }
    }
}
