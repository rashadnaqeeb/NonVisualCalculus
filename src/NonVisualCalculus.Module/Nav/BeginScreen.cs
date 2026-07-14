using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The full-screen begin prompt: a black screen with a single BEGIN button a new game waits on before
    /// its opening dialogue (and the ledger dream on later mornings). The game runs it under the
    /// transitional SPECIAL view with a <c>SunshinePseudoContinueButton</c> that selects itself and fires
    /// on the continue input, so this screen applies only while such a button is live. The button's
    /// visible caption is baked into a localized sprite (no text anywhere under it), so the authored
    /// prompt is the whole announcement and the one element is silent - it exists so Enter lands
    /// somewhere and fires the button's own onClick, the same handler the game's continue input invokes.
    /// </summary>
    public sealed class BeginScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.SPECIAL;
        public override string ScreenName => Strings.ScreenBeginPrompt;
        // A single silent button; letters have nothing to search.
        public override bool TypeAheadEnabled => false;

        public override bool AppliesNow() => FindButton() != null;

        public override Container BuildRoot(IModHost host)
        {
            var root = new Container(ContainerShape.VerticalList);
            SunshinePseudoContinueButton button = FindButton();
            // The button can drop between AppliesNow and the build (the prompt was dismissed the same
            // frame); the empty root then detaches next frame when AppliesNow goes false.
            if (button != null)
                root.Add(new BeginButton(button.GetComponent<UnityEngine.UI.Button>()));
            return root;
        }

        // The live pseudo-continue when one is up (FindObjectOfType sees active objects only), else null.
        private static SunshinePseudoContinueButton FindButton() =>
            Object.FindObjectOfType<SunshinePseudoContinueButton>();

        // The prompt's one element: no label, role, or value (the screen name said everything), just the
        // Activate that invokes the game button's own onClick.
        private sealed class BeginButton : UIElement
        {
            private readonly UnityEngine.UI.Button _button;

            public BeginButton(UnityEngine.UI.Button button) => _button = button;

            public override bool CanFocus => _button != null && _button.isActiveAndEnabled && _button.interactable;

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Activate, () => _button.onClick.Invoke());
            }
        }
    }
}
