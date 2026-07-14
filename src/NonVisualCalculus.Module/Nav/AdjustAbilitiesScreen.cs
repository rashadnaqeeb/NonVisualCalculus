using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The Adjust Abilities step of Create Your Own as a single vertical list: the four abilities
    /// (Intellect, Psyche, Physique, Motorics) as sliders, then the Random, Revert, and Next action
    /// buttons. Each ability adjusts in place through its leveler (see <see cref="AbilityControl"/>); the
    /// action buttons - icon-only, captioned through their localized image terms - run the game's own
    /// submit. Back is the screen's Escape (<see cref="ScreenRoot"/> closes the view), returning to
    /// archetype selection.
    /// </summary>
    public sealed class AdjustAbilitiesScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.CHARACTER_CREATION_ADJUST_ABILITIES;
        public override string ScreenName => Strings.ScreenAdjustAbilities;

        public override Container BuildRoot(IModHost host)
        {
            var root = new ScreenRoot();
            var list = new Container(ContainerShape.VerticalList);

            // The four creation abilities: every active StatPanel carrying a grade flip clock (the
            // character-sheet panels that reuse the type have none), ordered by the game's ability enum so
            // they read Intellect, Psyche, Physique, Motorics.
            var panels = new List<StatPanel>();
            foreach (StatPanel p in Object.FindObjectsOfType<StatPanel>())
                if (p.abilityGradeFlipClock != null)
                    panels.Add(p);
            panels.Sort((a, b) => ((int)a.ability).CompareTo((int)b.ability));
            foreach (StatPanel p in panels)
                list.Add(new AbilityControl(p));

            if (panels.Count == 0)
                host.LogWarning("AdjustAbilitiesScreen: no ability panels found; abilities will be unreachable.");

            // The action buttons live in the AdjustAbilitiesMode branch beside the abilities; reach it from
            // an ability panel's shared Charsheet ancestor so it is tied to the same live sheet.
            Transform mode = AdjustMode(panels);
            if (mode != null)
            {
                AddButton(list, mode, "Random Button", host);
                AddButton(list, mode, "Revert Abilities Button", host);
                AddButton(list, mode, "Next Button", host);
            }
            else
                host.LogWarning("AdjustAbilitiesScreen: AdjustAbilitiesMode not found; action buttons will be unreachable.");

            root.Add(list);
            return root;
        }

        // The AdjustAbilitiesMode transform, climbed from an ability panel up to the Charsheet it shares
        // with the mode, then down to the mode. Null when there is no ability panel to anchor from.
        private static Transform AdjustMode(List<StatPanel> panels)
        {
            if (panels.Count == 0)
                return null;
            Transform t = panels[0].transform;
            while (t != null && t.name != "Charsheet")
                t = t.parent;
            return t != null ? t.Find("AdjustAbilitiesMode") : null;
        }

        private static void AddButton(Container list, Transform mode, string name, IModHost host)
        {
            Transform t = mode.Find(name);
            Selectable s = t != null ? t.GetComponent<Selectable>() : null;
            if (s != null)
                list.Add(new SelectableButton(s));
            else
                host.LogWarning($"AdjustAbilitiesScreen: '{name}' not found; it will be unreachable.");
        }
    }
}
