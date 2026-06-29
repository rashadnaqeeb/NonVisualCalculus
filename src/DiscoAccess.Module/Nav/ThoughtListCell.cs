using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;
using Sunshine;
using Sunshine.Metric;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// One entry in the thought cabinet's master list, wrapping a live <see cref="ThoughtOnList"/>. Reads
    /// the thought's name, stage, effects and description live at announce time (never cached) through
    /// <see cref="ThoughtAdapter"/> and the Core <see cref="ThoughtAnnouncer"/>. On focus it makes the
    /// thought the game's selection so the cabinet highlights it. On Enter it runs the thought's contextual
    /// action (Internalize a gathered or forgotten thought into a free slot, Forget one already placed) the
    /// game's own way, then re-announces the new stage. An undiscovered thought is a mystery with no action.
    /// </summary>
    internal sealed class ThoughtListCell : UIElement
    {
        private readonly ThoughtOnList _item;
        private readonly IModHost _host;

        public ThoughtListCell(ThoughtOnList item, IModHost host)
        {
            _item = item;
            _host = host;
        }

        public override bool CanFocus
            => _item != null && _item.isActiveAndEnabled && _item.Project != null;

        public override string GetFocusText() => ThoughtAnnouncer.Compose(ThoughtAdapter.ReadListItem(_item));

        // The re-announce after an Enter: the thought's now-current stage (e.g. "researching, 0 percent"
        // right after it is internalized). Internalize and forget are immediate (no confirmation), so the
        // updated stage is what to read back.
        public override string Value => ThoughtAnnouncer.ComposeStatus(ThoughtAdapter.ReadListItem(_item));

        public override bool ReannounceOnActivate => true;

        public override void OnFocused() => GameCursor.Follow(_item);

        public override IEnumerable<ElementAction> GetActions()
        {
            // An undiscovered thought cannot be acted on; everything else has a contextual action the game
            // wires onto the tooltip button when the thought is selected.
            if (_item.Project != null && _item.Project.state != ThoughtState.UNKNOWN)
                yield return new ElementAction(ActionIds.Activate, () => ThoughtCommit.Interact(_item, _host));
        }
    }
}
