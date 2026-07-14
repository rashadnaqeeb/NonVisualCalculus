using System.Text;
using TMPro;
using UnityEngine.UI;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Adapter: pulls the raw text off a focused Selectable by reading the text labels on it and its
    /// children. Extraction only; the Core pipeline cleans the result. DE's UI mixes the two uGUI text
    /// kinds, TextMeshPro (TMP_Text) and the legacy <see cref="Text"/> (the save/load menus are almost
    /// entirely the latter), so both are swept. Each label is read in natural case via
    /// <see cref="GameLocalization.Cased(TMP_Text)"/> (DE draws labels ALL-CAPS, which reads oddly),
    /// falling back to its displayed text when it carries no localized source to recase from. Distinct
    /// labels (a control's title and its description) are joined with a line break so the Core filter
    /// reads them with a pause, the same way it breaks any multi-line label, instead of running them
    /// into one breathless phrase. A control with no text label of its own (an icon button) falls back
    /// to the caption behind its localized image via <see cref="GameLocalization.ImageButtonLabel"/>.
    /// </summary>
    public static class FocusReader
    {
        public static string Read(Selectable selectable)
        {
            var go = selectable.gameObject;
            var sb = new StringBuilder();
            foreach (var label in go.GetComponentsInChildren<TMP_Text>(true))
                Append(sb, GameLocalization.Cased(label));
            foreach (var label in go.GetComponentsInChildren<Text>(true))
                Append(sb, GameLocalization.Cased(label));
            // An icon button carries no text label; fall back to the caption behind its localized image.
            if (sb.Length == 0)
                return GameLocalization.ImageButtonLabel(go) ?? string.Empty;
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(text);
        }
    }
}
