using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The character-creation archetype selection screen as a single vertical list of its character
    /// choices: the three preset archetypes (Thinker, Sensitive, Physical) then Create Your Own, in the
    /// game's own selection order. The buttons come from the live <see cref="FourArchetypeSelector"/>
    /// singleton, sorted by <c>selectionIndex</c>, so the custom-character button - which sits outside the
    /// archetype ButtonPanel in the hierarchy and carries the highest index - is grouped with the presets
    /// as the fourth choice. Back is the screen's Escape (<see cref="ScreenRoot"/> closes the view the
    /// game's way), so the on-screen Back button is reached that way rather than as a list item.
    /// </summary>
    public sealed class ArchetypeScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.ARCHETYPE_SELECTION;
        public override string ScreenName => Strings.ScreenArchetypeSelection;

        public override Container BuildRoot(IModHost host)
        {
            var root = new ScreenRoot();

            FourArchetypeSelector selector = FourArchetypeSelector.Singleton;
            if (selector == null || selector.archetypeButtons == null)
            {
                host.LogWarning("ArchetypeScreen: FourArchetypeSelector has no archetype buttons; empty screen.");
                return root;
            }

            // Order by the game's own selection index (the custom-character button sorts last), so the
            // list reads top to bottom as the presets then Create Your Own.
            var buttons = new List<ArchetypeSelectButton>();
            foreach (ArchetypeSelectButton b in selector.archetypeButtons)
                if (b != null)
                    buttons.Add(b);
            buttons.Sort((a, b) => a.selectionIndex.CompareTo(b.selectionIndex));

            var list = new Container(ContainerShape.VerticalList);
            foreach (ArchetypeSelectButton b in buttons)
            {
                Selectable selectable = b.GetComponent<Selectable>();
                if (selectable != null)
                    list.Add(new ArchetypeButton(selectable));
            }
            root.Add(list);
            return root;
        }
    }
}
