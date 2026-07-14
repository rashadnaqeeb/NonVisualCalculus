using NonVisualCalculus.Core.UI;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Adapter: turns a focused save/load list entry into a Unity-free <see cref="SaveEntryState"/> for
    /// Core to compose. A list entry carries a <see cref="SaveGameListEntry"/>, whose
    /// <see cref="SaveGameListEntry.ShortenSaveName"/> is the clean name source: natural case for the
    /// game's own quick/auto saves and verbatim for a player-typed name, where the on-screen name label
    /// is uppercased and the raw file name is a timestamped path. The date and time are the entry's
    /// "DateText" and "TimeText" labels, which the game draws with a leading "| " divider that is stripped
    /// for speech; the create-new slot has neither but reports <see cref="SaveGameListEntry.IsNewSave"/>.
    /// Extraction only; no word choice and no caching past the live read.
    /// </summary>
    public static class SaveEntryAdapter
    {
        public static SaveEntryState TryRead(Selectable selectable)
        {
            var entry = selectable.gameObject.GetComponent<SaveGameListEntry>();
            if (entry == null)
                return null;

            return new SaveEntryState(
                entry.ShortenSaveName,
                Divider(selectable.gameObject, "DateText"),
                Divider(selectable.gameObject, "TimeText"),
                entry.IsNewSave);
        }

        // Read a named child label and drop the leading "| " visual divider the game prepends.
        private static string Divider(GameObject entry, string child)
        {
            Transform node = entry.transform.Find(child);
            Text label = node != null ? node.GetComponent<Text>() : null;
            return label != null ? label.text.TrimStart('|', ' ') : null;
        }
    }
}
