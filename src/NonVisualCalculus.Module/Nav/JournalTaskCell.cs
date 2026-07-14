using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Journal;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// One task in the journal's task list, wrapping a live <see cref="JournalTaskUI"/> (itself the game's
    /// own selectable for the row). Reads the task's localized name and resolution state live at announce
    /// time (never cached) through <see cref="JournalAdapter"/> and the Core <see cref="JournalAnnouncer"/>.
    /// On focus it makes the row the game's selection, whose own OnSelect populates the centre detail panel,
    /// and points the shared <see cref="JournalDetail"/> at this task so the detail cells reflect it. The row
    /// is read-only here (selecting is the only interaction; a task is acted on out in the world).
    /// </summary>
    internal sealed class JournalTaskCell : UIElement
    {
        private readonly JournalTaskUI _ui;
        private readonly JournalDetail _detail;

        public JournalTaskCell(JournalTaskUI ui, JournalDetail detail)
        {
            _ui = ui;
            _detail = detail;
        }

        public override bool CanFocus => _ui != null && _ui.isActiveAndEnabled && _ui.task != null;

        public override string GetFocusText() => JournalAnnouncer.ComposeTask(JournalAdapter.ReadTask(_ui.task));

        // Select the row through the game's own NavigationManager so its OnSelect shows the detail and
        // highlights the row, and aim the detail panel at this task so Tab reads its description and subtasks.
        public override void OnFocused()
        {
            GameCursor.Follow(_ui);
            _detail.SetSource(_ui);
        }
    }
}
