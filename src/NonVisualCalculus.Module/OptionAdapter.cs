using System.Text.RegularExpressions;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI;
using LocalizationCustomSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Adapter: turns a focused options control into a Unity-free <see cref="OptionState"/> for Core to
    /// compose. Reads live component state only (never caches game data) and does no word choice. An
    /// options control is any focusable carrying an <see cref="OptionSelectableController"/>; its label
    /// resolves per kind (a toggle's name is a child "Label", a slider's or dropdown's is the sibling
    /// row "Label", because a dropdown's own child "Label" holds its value, not its name).
    /// </summary>
    public static class OptionAdapter
    {
        private static readonly Regex CamelBoundary = new Regex("([a-z])([A-Z])", RegexOptions.Compiled);

        /// <summary>
        /// Build an OptionState for a focused options control, or null if it is not one. The tooltip
        /// body is read only when <paramref name="withTooltip"/> is set (on a fresh focus, not on the
        /// per-frame value poll, where it is not spoken and would re-resolve every frame).
        /// </summary>
        public static OptionState TryRead(Selectable selectable, bool withTooltip)
        {
            var go = selectable.gameObject;
            if (go.GetComponent<OptionSelectableController>() == null)
                return null;

            var dropdown = selectable.TryCast<TMP_Dropdown>();
            if (dropdown != null)
            {
                Transform label = RowLabelNode(go);
                string labelText = LabelText(label, go);
                string caption = dropdown.captionText != null ? GameLocalization.Spoken(dropdown.captionText) : null;
                return OptionState.Dropdown(labelText, caption, Description(go, label, labelText, withTooltip));
            }

            var toggle = selectable.TryCast<Toggle>();
            if (toggle != null)
            {
                Transform label = ToggleLabelNode(go);
                string labelText = LabelText(label, go);
                return OptionState.Toggle(labelText, toggle.isOn, Description(go, label, labelText, withTooltip));
            }

            var slider = selectable.TryCast<Slider>();
            if (slider != null)
            {
                Transform label = RowLabelNode(go);
                string labelText = LabelText(label, go);
                return ReadSlider(go, slider, labelText, Description(go, label, labelText, withTooltip));
            }

            return null;
        }

        private static OptionState ReadSlider(GameObject go, Slider slider, string label, string description)
        {
            if (slider.wholeNumbers)
            {
                int min = Mathf.RoundToInt(slider.minValue);
                int max = Mathf.RoundToInt(slider.maxValue);
                int value = Mathf.RoundToInt(slider.value);
                return OptionState.SteppedSlider(label, SteppedIdFor(go), value - min, max - min + 1, description);
            }

            float range = slider.maxValue - slider.minValue;
            float fraction = range > 0f ? (slider.value - slider.minValue) / range : 0f;
            return OptionState.ContinuousSlider(label, fraction, description);
        }

        // The game's description body for the row, already localized; null when the control has none.
        // Two shapes: a standard tooltip provider nested under the setting's "Label" node (used only
        // when its title matches the label, since the game leaves some tooltip nodes wired to another
        // setting's text, e.g. the HDR checkbox carries Detective Mode's), and the secondary-language
        // dropdown's own description component, which is not a tooltip provider and has no title.
        private static string Description(GameObject go, Transform labelNode, string labelText, bool withTooltip)
        {
            if (!withTooltip)
                return null;

            if (labelNode != null)
            {
                var data = TooltipData(labelNode);
                if (data != null && Normalize(data.Title) == Normalize(labelText))
                    return data.Description;
            }

            if (go.GetComponent<SwitchableLocalizationSettingsOption>() != null)
            {
                var swLang = FindInRow<SwitchableLanguageDescription>(go);
                if (swLang != null)
                {
                    var tmp = swLang.GetComponent<TMP_Text>();
                    if (tmp == null)
                        tmp = swLang.GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                        return GameLocalization.Spoken(tmp);
                }
            }
            return null;
        }

        // Tooltip providers under the Label node: the localized provider (and its switch-platform
        // subclass), then the platform-specific provider some controls (Vibration) use instead.
        private static GenericTooltipData TooltipData(Transform labelNode)
        {
            var localized = labelNode.GetComponentInChildren<LocalizedTooltipDescription>(true);
            if (localized != null)
            {
                var data = localized.GetTooltipData();
                if (data != null)
                    return data;
            }
            var platform = labelNode.GetComponentInChildren<LocalizedPlatformSpecificTooltipDescription>(true);
            return platform != null ? platform.GetTooltipData() : null;
        }

        // The nearest component of type T in the control's own row, climbing without crossing into a
        // container that holds another option control.
        private static T FindInRow<T>(GameObject go) where T : Component
        {
            Transform t = go.transform;
            while (t.parent != null)
            {
                var found = t.parent.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
                if (t.parent.GetComponentsInChildren<OptionSelectableController>(true).Length > 1)
                    break;
                t = t.parent;
            }
            return null;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return Regex.Replace(s, "\\s+", " ").Trim().ToLowerInvariant();
        }

        // Menu Size and Dialogue Text Size are the only stepped sliders; their rows carry stable
        // internal names while the focused slider object is just "Slider".
        private static SteppedSliderId SteppedIdFor(GameObject go)
        {
            string row = go.transform.parent != null ? go.transform.parent.name : "";
            if (row == "LayoutProfileSlider")
                return SteppedSliderId.MenuSize;
            if (row == "Text Size")
                return SteppedSliderId.DialogueTextSize;
            return SteppedSliderId.Unknown;
        }

        // A slider or dropdown's setting name sits on a "Label" sibling on the row. The control is
        // usually a direct child of the row, but some (the language dropdowns) nest under an extra
        // "Layout" node, so climb until a "Label" is found, stopping before a container that holds
        // another option control (whose "Label" would belong to a different setting).
        private static Transform RowLabelNode(GameObject go)
        {
            Transform t = go.transform;
            while (t.parent != null)
            {
                Transform label = t.parent.Find("Label");
                if (HasText(label))
                    return label;
                if (t.parent.GetComponentsInChildren<OptionSelectableController>(true).Length > 1)
                    break;
                t = t.parent;
            }
            return null;
        }

        // A toggle's setting name sits on a "Label" child of the toggle itself (with a "Separator"
        // sibling carrying a "|" the named lookup skips).
        private static Transform ToggleLabelNode(GameObject go)
        {
            Transform label = FindDescendant(go.transform, "Label");
            return HasText(label) ? label : null;
        }

        // The setting name: the resolved "Label" node's text, an authored fallback for the few controls
        // the game draws with no label of their own, else the cleaned object name.
        private static string LabelText(Transform labelNode, GameObject go)
        {
            if (labelNode != null)
            {
                var tmp = labelNode.GetComponent<TMP_Text>();
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                    return GameLocalization.Spoken(tmp);
            }

            if (go.GetComponent<SwitchableLocalizationSettingsOption>() != null)
                return Strings.SecondaryLanguage;
            return CleanName(go.name);
        }

        private static bool HasText(Transform t)
        {
            if (t == null)
                return false;
            var tmp = t.GetComponent<TMP_Text>();
            return tmp != null && !string.IsNullOrEmpty(tmp.text);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                    return child;
                var found = FindDescendant(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static string CleanName(string name)
        {
            return CamelBoundary.Replace(name, "$1 $2");
        }
    }
}
