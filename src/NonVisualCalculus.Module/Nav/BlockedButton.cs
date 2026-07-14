using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// A navigable menu button that reads like any other but refuses to open: activating it speaks an
    /// authored reason instead of running the game's submit. Used for the main menu's Collage button,
    /// which opens DE's screenshot composition canvas - a visual screen with no accessible path - so the
    /// player hears what it is and why it is closed rather than dropping into an unreadable view. This is
    /// the one control we deliberately block; every other button activates normally via
    /// <see cref="SelectableButton"/>.
    /// </summary>
    public sealed class BlockedButton : UIElement
    {
        private readonly Selectable _selectable;
        private readonly IModHost _host;
        private readonly string _reason;

        public BlockedButton(Selectable selectable, IModHost host, string reason)
        {
            _selectable = selectable;
            _host = host;
            _reason = reason;
        }

        public override bool CanFocus => _selectable != null && _selectable.isActiveAndEnabled && _selectable.interactable;

        public override string Label => FocusReader.Read(_selectable);
        public override string Role => Strings.RoleButton;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, Activate);
        }

        // Move the game's cursor to follow our focus, like a normal button, so the menu highlight tracks
        // us even though activation will not open the view.
        public override void OnFocused() => GameCursor.Follow(_selectable);

        // Speak the reason and stop: never call NavigationManager.Submit, so the game's open path never
        // runs. Interrupting because the player just acted and this supersedes the landing announcement.
        private void Activate() => _host.Speech.Speak(_reason, interrupt: true);
    }
}
