using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoAccess.Dev
{
    /// <summary>
    /// Raw structural dump of the active uGUI hierarchy for the dev /gui endpoint: every active
    /// GameObject under each root canvas, indented by depth, with its component types, any
    /// TMP/legacy text, and CanvasGroup alpha. Deliberately NOT the mod's cleaned focus view (that
    /// is /focus and /nav) - it surfaces structure those hide, e.g. data living in sub-objects the
    /// focus label never exposes, so an unfamiliar screen (DE's Pages/SubPage wrapping especially)
    /// can be reverse-engineered in one shot and then driven with /eval. Diff this against /nav to
    /// find where the mod is losing information. Reads live state; caches nothing.
    ///
    /// Caveat: lists GameObject-active objects only (inactive subtrees are pruned), but an object can
    /// be active yet visually hidden via CanvasGroup alpha 0 - the reported alpha flags that, and
    /// /screenshot is the visibility truth.
    /// </summary>
    internal static class GuiInspector
    {
        public static string Describe()
        {
            var sb = new StringBuilder();
            int roots = 0;
            foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
            {
                if (!canvas.isRootCanvas || !canvas.gameObject.activeInHierarchy)
                    continue;
                roots++;
                sb.Append("=== canvas: ").Append(canvas.name)
                  .Append("  sort=").Append(canvas.sortingOrder).Append(" ===\n");
                Walk(canvas.transform, 0, sb);
            }
            if (roots == 0)
                sb.Append("(no active root canvas)\n");
            return sb.ToString();
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            GameObject go = t.gameObject;
            if (!go.activeInHierarchy)
                return;

            sb.Append(' ', depth * 2).Append(go.name);
            AppendComponents(go, sb);

            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
                sb.Append("  alpha=").Append(cg.alpha);

            string text = NodeText(go);
            if (text != null)
                sb.Append("  \"").Append(text).Append('"');

            sb.Append('\n');

            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }

        // Component type names on this node, skipping the ones present on nearly every UI node (they are
        // noise that buries the diagnostic ones - Image, Button, Canvas, the game's own scripts).
        private static void AppendComponents(GameObject go, StringBuilder sb)
        {
            bool any = false;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null)
                    continue; // a missing-script slot
                string name = c.GetType().Name;
                if (name == "Transform" || name == "RectTransform" || name == "CanvasRenderer")
                    continue;
                sb.Append(any ? ", " : "  [").Append(name);
                any = true;
            }
            if (any)
                sb.Append(']');
        }

        // Text on this exact node only (children get their own lines). Reads both TMP and legacy
        // UnityEngine.UI.Text, since DE menus mix the two.
        private static string NodeText(GameObject go)
        {
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                return tmp.text;
            var txt = go.GetComponent<Text>();
            if (txt != null && !string.IsNullOrEmpty(txt.text))
                return txt.text;
            return null;
        }
    }
}
