using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using HarmonyLib;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Repairs the game's keyring persistence before its loader can silently delete keys. The game
    /// saves each keyring key as its display name resolved in English but passed through the RTL
    /// fixer whenever the current language is right-to-left (in Arabic, "Key to Room #1" is stored
    /// as "#1 Key to Room"), and restores keys by exact string match against the same call at load
    /// time - so a save written under one language state and loaded under another drops every key
    /// whose name the fixer reorders, permanently and without a word (the room key has no second
    /// source in the whole game). A prefix on the loader rewrites each saved entry to the exact
    /// string the matcher is about to compute for that item, so the stock code then restores every
    /// key the entry can still be traced to. An entry tracing to no item in any known form is left
    /// as the game would see it, logged, and spoken - a lost key must never be silent.
    /// </summary>
    internal sealed class KeyringPersistenceGuard : IDisposable
    {
        // The live guard while patched, for the static Harmony feeder; cleared on dispose (the
        // module reloads into a collectible context, so this static dies with it).
        private static KeyringPersistenceGuard _active;

        private readonly IModHost _host;

        public KeyringPersistenceGuard(IModHost host) { _host = host; }

        /// <summary>Patch the loader through the module's own Harmony instance, so a reload's
        /// <c>UnpatchSelf</c> removes it.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(InventoryViewPersister), nameof(InventoryViewPersister.Deserialize)),
                prefix: new HarmonyMethod(typeof(KeyringPersistenceGuard), nameof(OnDeserialize)));
        }

        public void Dispose() => _active = null;

        // The Harmony feeder: runs on the game's load path, so every failure is caught and logged -
        // a throw here would abort the game's own inventory restore.
        private static void OnDeserialize(InventoryViewPersister.InventoryViewState inventoryViewState)
        {
            KeyringPersistenceGuard self = _active;
            if (self == null || inventoryViewState == null) return;
            try
            {
                self.RepairKeys(inventoryViewState);
            }
            catch (Exception e)
            {
                self._host.LogError("KeyringPersistenceGuard: repairing saved keyring names failed: " + e);
            }
        }

        private void RepairKeys(InventoryViewPersister.InventoryViewState state)
        {
            var saved = state.keys;
            if (saved == null || saved.Count == 0) return;

            InventoryItemList library = UnityEngine.Object.FindObjectOfType<InventoryItemList>();
            if (library == null)
            {
                _host.LogError("KeyringPersistenceGuard: no InventoryItemList at load time; saved keyring names not repaired.");
                return;
            }

            // Every form a keyring entry can have been saved under, each mapped to its item's term.
            // The game's serializer resolves the display term in English and RTL-fixes the result
            // when the running language is RTL, so an entry is the raw English name, the fixed
            // English name, or (defensively) the item's dev name. Keys are compared Unicode-
            // normalized: the fixer's output for accented letters has not round-tripped stably.
            var forms = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (InventoryItemListComponent comp in library.items)
            {
                var item = comp.item;
                if (item == null) continue;
                string term = item.displayNameTerm;
                if (string.IsNullOrEmpty(term)) continue;
                AddForm(forms, item.name, term);
                string rawEnglish = I2.Loc.LocalizationManager.GetTranslation(
                    term, false, 0, true, false, null, "English", true);
                if (!string.IsNullOrEmpty(rawEnglish))
                {
                    AddForm(forms, rawEnglish, term);
                    AddForm(forms, I2.Loc.LocalizationManager.ApplyRTLfix(rawEnglish), term);
                }
            }

            for (int i = 0; i < saved.Count; i++)
            {
                string entry = saved[i];
                if (string.IsNullOrEmpty(entry)) continue;
                if (!forms.TryGetValue(Normalize(entry), out string term))
                {
                    // Untraceable: the stock matcher is about to drop it. Leave the entry so a
                    // future, smarter load can still try, and tell the player - the game says nothing.
                    _host.LogError("KeyringPersistenceGuard: saved keyring entry '" + entry
                        + "' matches no item; the game will not restore it.");
                    _host.Speech.Speak(Strings.KeyringItemNotRestored(entry), interrupt: false);
                    continue;
                }
                // The exact string the stock matcher computes for this item, at this instant, in
                // this language state - by the same call its loader makes - so it must match.
                string expected = LocalizationCustomSystem.LocalizationManager.GetLocalizedTerm(term, "English");
                if (string.IsNullOrEmpty(expected) || entry == expected) continue;
                _host.LogWarning("KeyringPersistenceGuard: repaired saved keyring entry '" + entry
                    + "' to '" + expected + "' (" + term + ").");
                saved[i] = expected;
            }
        }

        // First writer wins: when two items share a display name (the union card variants), the
        // stock matcher restores the first, so the repaired string must trace the same way.
        private static void AddForm(Dictionary<string, string> forms, string form, string term)
        {
            if (string.IsNullOrEmpty(form)) return;
            string key = Normalize(form);
            if (!forms.ContainsKey(key)) forms[key] = term;
        }

        private static string Normalize(string s) => s.Normalize(System.Text.NormalizationForm.FormC);
    }
}
