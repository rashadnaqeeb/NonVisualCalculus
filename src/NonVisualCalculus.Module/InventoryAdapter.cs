using System.Collections.Generic;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.Text;
using NonVisualCalculus.Core.UI;
using Sunshine;
using Sunshine.Metric;
using Sunshine.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Reads the live inventory the game's way and hands back plain data (no Unity types past the boundary,
    /// per the adapter/composition split). The equipped doll and the item storage are both
    /// <see cref="UIDragDock"/>s distinguished by <see cref="SlotNature"/>; a filled dock parents a
    /// <see cref="UIDraggable"/> carrying the <see cref="InventoryItem"/>. Nothing here formats speech - that
    /// is <see cref="InventoryItemAnnouncer"/> and the cells - and nothing is cached (held Unity component
    /// references aside, which is the allowed kind).
    /// </summary>
    internal static class InventoryAdapter
    {
        public static InventoryView View() => Object.FindObjectOfType<InventoryView>();

        public static InventoryTooltip Tooltip() => Object.FindObjectOfType<InventoryTooltip>();

        // The active category tab's display name, read from its live button text, so the item list can be
        // labeled with whatever the filter is actually set to (Tools, Clothes, ...) rather than a fixed
        // "items" that collides with the Items tab. Falls back to the authored label when the tab panel or
        // its button text is unavailable.
        public static string CurrentTabName()
        {
            InventoryTabPanel tp = InventoryTabPanel.Singleton;
            if (tp == null) return Strings.InventoryItemsLabel;
            ItemTabGroup g = tp.CurrentItemTabGroup;
            InventoryTabButton button = null;
            if (tp.inventoryTabButtons != null && tp.inventoryTabButtons.TryGetValue(g, out button)
                && button != null && button.ButtonText != null)
                return TextFilter.Clean(GameLocalization.Spoken(button.ButtonText));
            return Strings.InventoryItemsLabel;
        }

        // Every dock of a given nature, in scene order. Equipment and held docks are stable across a screen
        // (the doll), so they are gathered once at build; inventory docks belong to the active tab and are
        // gathered fresh each rebuild.
        public static List<UIDragDock> Docks(SlotNature nature, bool activeOnly)
        {
            var list = new List<UIDragDock>();
            foreach (UIDragDock d in Object.FindObjectsOfType<UIDragDock>())
                if (d.slotNature == nature && (!activeOnly || d.gameObject.activeInHierarchy))
                    list.Add(d);
            return list;
        }

        // The item docked in a slot, or null when the slot is empty. A filled dock parents the item's
        // UIDraggable; an empty dock parents none.
        public static InventoryItem ItemInDock(UIDragDock dock)
        {
            UIDraggable dr = dock.GetComponentInChildren<UIDraggable>(false);
            return dr != null ? dr.item : null;
        }

        // The slot's caption label, read live from the game's own "<dockName>Tag" object (e.g. "hatTag" ->
        // "HAT"), so the wording and localization are the game's. Null when none is found; the cell then
        // falls back to the authored slot name.
        public static TMP_Text FindCaption(UIDragDock dock)
        {
            string tag = dock.name + "Tag";
            foreach (TMP_Text t in Object.FindObjectsOfType<TMP_Text>(true))
                if (t.transform.parent != null && t.transform.parent.name == tag)
                    return t;
            return null;
        }

        public static InventoryItemState ReadItem(InventoryItem item)
        {
            bool counted = item.consumable || item.substance;
            int uses = counted ? InventoryItemExtension.ItemsUses(item) : 0;
            return new InventoryItemState
            {
                Name = item.GetDisplayName(),
                IsFresh = item.IsFresh(),
                Uses = counted && uses > 0 ? uses : (int?)null,
                Value = item.itemValue,
                Effects = Effects(item),
                Description = string.IsNullOrEmpty(item.description) ? null : TextFilter.Clean(item.description),
            };
        }

        // An item read as a pawnable (the pawnshop sell view): the regular readout plus the shop's own
        // "Pawn for" label, so the value is spoken as the offer the priced PAWN button shows.
        public static InventoryItemState ReadPawnable(InventoryItem item)
        {
            InventoryItemState s = ReadItem(item);
            s.PawnLabel = GameLocalization.Term("INVENTORY_TOOLTIP_PAWN_FOR", Strings.ItemPawnFor);
            return s;
        }

        // An item read as loot (the world's container panel): the library prototype looked up by internal
        // name, not an owned instance, so the ownership markers are left out - the prototype's "fresh" flag
        // and use count describe a thing the player does not hold yet and would read as noise. Name, pawn
        // value, effects, and description still tell the player what they are about to take.
        public static InventoryItemState ReadLoot(InventoryItem item)
        {
            return new InventoryItemState
            {
                Name = item.GetDisplayName(),
                Value = item.itemValue,
                Effects = Effects(item),
                Description = string.IsNullOrEmpty(item.description) ? null : TextFilter.Clean(item.description),
            };
        }

        // What the item does, read straight off its own model so it is correct for the item being
        // announced (the shared tooltip lags a frame behind focus): the equip effects, then for a substance
        // the effects-when-used riding its buffs, labelled with the game's own hover header so a drug's
        // payload is not mistaken for a wear bonus. Each effect's own full name already carries the sign,
        // the affected skill, and the flavour line ("+1 Half Light: Head as battering ram"); we only strip
        // its colour markup and join them.
        private static string Effects(InventoryItem item)
        {
            var parts = new List<string>();
            AddEffects(item.equipEffects, parts);
            if (item.substance && item.substanceBuffs != null)
            {
                var used = new List<string>();
                foreach (CharacterBuff b in item.substanceBuffs)
                    if (b != null)
                        AddEffects(b.effects, used);
                if (used.Count > 0)
                {
                    // The game's header is a format string ("Substance use effects: {0}"); the authored
                    // fallback is a bare label, so it takes the list after a space.
                    string header = GameLocalization.Term("TOOLTIP_HOVER_SUBSTANCE_USED_EFFECTS", Strings.ItemUseEffects);
                    string list = string.Join(", ", used);
                    parts.Add(header.Contains("{0}") ? string.Format(header, list) : header + " " + list);
                }
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static void AddEffects(CharacterEffect[] effects, List<string> parts)
        {
            if (effects == null) return;
            foreach (CharacterEffect e in effects)
            {
                if (e == null) continue;
                // Skip effects with no stat target (e.g. a tool's TOOLTIP effect reads "+0 None"): keep only
                // those that boost a skill or an attribute.
                if (e.skillType == SkillType.NONE && e.abilityType == AbilityType.Error) continue;
                string full = e.EffectFullName();
                if (!string.IsNullOrEmpty(full)) parts.Add(TextFilter.Clean(full));
            }
        }

        // The selectable the game navigates a dock by (its button), for syncing the game cursor and running
        // its submit path.
        public static Selectable Selectable(UIDragDock dock)
            => dock.MyButton != null ? dock.MyButton : dock.GetComponent<Selectable>();

        // ---- Left stats panel: read live from the game's own labels (the values are plain TMP texts here,
        // no flip clocks). ----

        // The four attributes as "Intellect 5, Psyche 1, Physique 2, Motorics 4". The panels live nested
        // under the stats column (a scroll viewport), found by a recursive search; the value is read from the
        // panel's own label, and the full ability name from the game's localization (the panel shows only the
        // abbreviation). The panel object name and the ability term differ for physique (PHQ vs FYS).
        private static readonly (string key, string ability)[] AttributeKeys =
            { ("INT", "INT"), ("PSY", "PSY"), ("PHQ", "FYS"), ("MOT", "MOT") };

        public static string Attributes(InventoryView iv)
        {
            if (iv == null || iv.statsColumn == null) return string.Empty;
            var parts = new List<string>(4);
            foreach ((string key, string ability) in AttributeKeys)
            {
                Transform t = FindDescendant(iv.statsColumn, key);
                if (t == null) continue;
                string value = Leaf(t, "Value");
                if (string.IsNullOrEmpty(value)) continue;
                string name = GameLocalization.Translate("Abilities/ABILITY_NAME_" + ability);
                if (string.IsNullOrEmpty(name)) name = Leaf(t, "Name"); // fall back to the abbreviation
                parts.Add(name + " " + value);
            }
            return string.Join(", ", parts);
        }

        // Health and morale, whose values are pip indicators (current of max), read as "HEALTH: 4 / 4".
        public static string Vitals(InventoryView iv)
        {
            if (iv == null || iv.statsColumn == null) return string.Empty;
            var parts = new List<string>(2);
            foreach (string key in new[] { "Health", "Morale" })
            {
                Transform t = FindDescendant(iv.statsColumn, key);
                if (t == null) continue;
                string label = Leaf(t, "Name");
                SegmentIndicator seg = t.GetComponentInChildren<SegmentIndicator>(true);
                if (seg == null) continue;
                string value = seg.Current + " / " + seg.Max;
                parts.Add(string.IsNullOrEmpty(label) ? value : label + " " + value);
            }
            return string.Join(", ", parts);
        }

        // The bonuses-from-items rows under the Modifiers panel: each row is a transform with a "Text" (the
        // skill) and "Number" (the signed amount) child, so "Drama +1". Only active rows count (an unequipped
        // loadout shows none); the header and the inactive template placeholder ("Suggestion:") are skipped.
        public static string Bonuses(InventoryView iv)
        {
            if (iv == null || iv.statsColumn == null) return string.Empty;
            Transform mods = FindDescendant(iv.statsColumn, "Modifiers");
            if (mods == null) return string.Empty;
            var parts = new List<string>();
            foreach (Transform row in mods.GetComponentsInChildren<Transform>(false))
            {
                if (row.Find("Text") == null || row.Find("Number") == null) continue;
                string text = Leaf(row, "Text");
                string number = Leaf(row, "Number");
                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(number)) continue;
                if (text.EndsWith(":")) continue; // a template placeholder ("Suggestion:")
                parts.Add(text + " " + number);
            }
            if (parts.Count == 0) return string.Empty;
            // Prepend the game's own "BONUSES FROM ITEMS:" header so the line announces what it is.
            Transform headerT = mods.Find("Helpful Text");
            TMP_Text header = headerT != null ? headerT.GetComponent<TMP_Text>() : null;
            string label = header != null ? GameLocalization.Cased(header) : null;
            string body = string.Join(", ", parts);
            return string.IsNullOrEmpty(label) ? body : label + " " + body;
        }

        // The keys / bullets display slots' content, from their own tooltip data: the keys slot lists the
        // keys held (one per line, read as sentences), the bullets slot the ammo count ("You've got 1
        // bullet"). Both carry a localized title that doubles as the label. An empty slot (no keys, no
        // bullets) yields null here - either the slot is hidden (FindObjectOfType returns only an active one)
        // or it is shown but its tooltip has no description - and its cell drops out of navigation.
        public static string KeysText()
        {
            Sunshine.KeysSlot slot = Object.FindObjectOfType<Sunshine.KeysSlot>();
            return slot != null ? TooltipReadout(slot.GetTooltipData(), Strings.InventoryKeys) : null;
        }

        public static string BulletsText()
        {
            Sunshine.BulletsSlot slot = Object.FindObjectOfType<Sunshine.BulletsSlot>();
            return slot != null ? TooltipReadout(slot.GetTooltipData(), Strings.InventoryBullets) : null;
        }

        // Compose a generic tooltip's title and description into one line. The description carries what the
        // slot holds (the keys list, the bullet count), so an empty description means an empty slot: return
        // null to drop the cell rather than announce a bare label for nothing.
        private static string TooltipReadout(GenericTooltipData data, string fallbackLabel)
        {
            string desc = data != null ? TextFilter.Clean(data.Description) : null;
            if (string.IsNullOrEmpty(desc)) return null;
            string title = string.IsNullOrEmpty(data.Title) ? fallbackLabel : TextFilter.Clean(data.Title);
            return title + ", " + desc;
        }

        private static string Leaf(Transform parent, string child)
        {
            Transform t = parent.Find(child);
            TMP_Text tmp = t != null ? t.GetComponent<TMP_Text>() : null;
            return tmp != null ? TextFilter.Clean(GameLocalization.Spoken(tmp)) : null;
        }

        // First descendant with the given name (the stats values are nested under a scroll viewport, beyond a
        // direct-child Find).
        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
