using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Journal;
using UnityEngine.EventSystems;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// One quicktravel point on the journal's map, wrapping a live <see cref="QuicktravelButton"/> with its
    /// authored location name (DE bakes the names into the map art). Reads its state live - whether this is
    /// where the player currently is, or a place already visited. Activating fast-travels there through the
    /// button's own pointer-down handler (the game's own travel path, the same a click runs). On focus it
    /// makes the button the game's selection, whose OnSelect shows its "travel here" marker.
    ///
    /// Focusable while its GameObject is shown; the button's Selectable is left disabled by the game (it
    /// drives travel through pointer events, not the Selectable), and the screen only builds these cells when
    /// the controller reports fast travel available, so the enabled flag is not part of the gate here.
    /// </summary>
    internal sealed class QuicktravelCell : UIElement
    {
        private readonly QuicktravelButton _button;
        private readonly string _name;

        public QuicktravelCell(QuicktravelButton button, string name)
        {
            _button = button;
            _name = name;
        }

        public override bool CanFocus => _button != null && _button.gameObject.activeInHierarchy;

        public override string GetFocusText()
        {
            if (_button.CheckTequilaInActivationRadius())
                return _name + ", " + Strings.JournalYouAreHere;
            if (_button.wasVisited)
                return _name + ", " + Strings.JournalVisited;
            return _name;
        }

        public override void OnFocused() => GameCursor.Follow(_button);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Travel);
        }

        // The button travels on pointer-down; run that handler directly (our keyboard lever mutes the game's
        // own input, so a synthetic OS click would not reach it).
        private void Travel() => _button.OnPointerDown(new PointerEventData(EventSystem.current));
    }
}
