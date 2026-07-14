using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonVisualCalculus.Dev
{
    /// <summary>
    /// Reads the current uGUI focus on demand for the dev /focus endpoint, so the driver can see what
    /// is selected at any moment, independent of speech (which only fires on a change that clears the
    /// dedup gate). Deliberately host-side and self-contained - it does not go through the feature
    /// module - so focus stays inspectable even when the module failed to load or its Tick threw, which
    /// is exactly when this is needed. Reads live state; caches nothing.
    ///
    /// Reports two sources because they can disagree: the uGUI <see cref="EventSystem"/> selection (the
    /// ground truth) and DE's <c>NavigationManager</c> (what the focus pump and input injector read).
    /// At a freshly loaded menu the EventSystem has a selection before NavigationManager records one, so
    /// seeing the split here is the diagnostic for "why didn't the pump announce / input move?".
    /// </summary>
    internal static class FocusInspector
    {
        public static string Describe()
        {
            var sb = new StringBuilder();

            EventSystem es = EventSystem.current;
            GameObject esGo = es != null ? es.currentSelectedGameObject : null;
            var nav = NavigationManager.Singleton;
            Selectable navSel = nav != null ? nav.GetCurrentSelectedSelectable() : null;

            sb.Append("eventsystem: ")
              .Append(esGo != null ? esGo.name : (es == null ? "(no EventSystem)" : "none")).Append('\n');
            sb.Append("navmanager: ")
              .Append(navSel != null ? navSel.name : (nav == null ? "(no NavigationManager)" : "none")).Append('\n');

            // Describe the effective selection: prefer the live EventSystem selection (ground truth).
            GameObject go = esGo != null ? esGo : (navSel != null ? navSel.gameObject : null);
            if (go == null)
            {
                sb.Append("text: (nothing selected)\n");
                return sb.ToString();
            }

            Selectable sel = go.GetComponent<Selectable>();
            sb.Append("path: ").Append(HierarchyPath(go)).Append('\n');
            sb.Append("interactable: ").Append(sel != null ? sel.interactable.ToString() : "n/a")
              .Append("  button: ").Append(sel != null && sel.TryCast<Button>() != null).Append('\n');
            sb.Append("text: ").Append(LabelText(go)).Append('\n');
            return sb.ToString();
        }

        // Concatenated TMP label text under the object, matching what FocusReader would extract, but
        // '|'-joined so the per-label split stays visible for debugging.
        private static string LabelText(GameObject go)
        {
            var parts = new StringBuilder();
            var labels = go.GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in labels)
            {
                string t = label.text;
                if (string.IsNullOrEmpty(t))
                    continue;
                if (parts.Length > 0)
                    parts.Append(" | ");
                parts.Append(t);
            }
            return parts.Length > 0 ? parts.ToString() : "(no TMP text)";
        }

        private static string HierarchyPath(GameObject go)
        {
            var names = new List<string>();
            Transform t = go.transform;
            while (t != null)
            {
                names.Add(t.name);
                t = t.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
