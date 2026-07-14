using System.Collections.Generic;
using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A navigable character-creation choice on the archetype selection screen: one of the three preset
    /// archetypes (Thinker, Sensitive, Physical) or the Create Your Own button. It reads the live
    /// <see cref="Selectable"/> through the structured <see cref="ArchetypeAdapter"/> and composes with the
    /// Core <see cref="ArchetypeAnnouncer"/> (name, attributes, signature skill, description), so it speaks
    /// exactly what the focus-follower did and never voices the stacked flip-clock animation layers a raw
    /// TMP sweep would. The custom-character button carries no stats, so the adapter returns null and it
    /// falls back to the generic label read ("Create your own character"). No "button" role is appended:
    /// the long stat-and-description line reads better without a trailing type word, matching the prior
    /// readout. Activation runs the game's own submit path (Select then Submit) so the choice registers.
    /// </summary>
    public sealed class ArchetypeButton : UIElement
    {
        private readonly Selectable _selectable;

        public ArchetypeButton(Selectable selectable) => _selectable = selectable;

        // Focusable only while shown and interactable.
        public override bool CanFocus => _selectable != null && _selectable.isActiveAndEnabled && _selectable.interactable;

        public override string GetFocusText()
        {
            ArchetypeState s = ArchetypeAdapter.TryRead(_selectable);
            return s != null ? ArchetypeAnnouncer.Compose(s) : FocusReader.Read(_selectable);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Activate);
        }

        // Move the game's cursor to this choice as our focus lands, so its selection follows ours.
        public override void OnFocused() => GameCursor.Follow(_selectable);

        private void Activate()
        {
            // Make this the game's current selection, then run its submit handler (the archetype button's
            // ISubmitHandler), so DE's full activation runs while our keyboard lever mutes its input.
            var nav = NavigationManager.Singleton;
            nav.Select(_selectable);
            nav.Submit();
        }
    }
}
