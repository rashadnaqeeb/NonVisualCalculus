using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Journal;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// Holds the task whose detail the centre panel is showing - the task the list cells point it at on
    /// focus. The detail cells read it live, so moving the list focus changes what they read with no tree
    /// rebuild. Null until a task is focused, which keeps the whole detail panel out of the Tab order.
    ///
    /// When the focused task changes, the detail list's remembered line is cleared so the new task's detail
    /// starts at the description rather than wherever the previous task's was left. Identity is tracked by
    /// the source row (a Unity object, so reference equality is reliable), and re-focusing the same task
    /// keeps its detail position.
    /// </summary>
    internal sealed class JournalDetail
    {
        private Container _list;
        private JournalTaskUI _source;

        public Completeable Subject { get; private set; }

        // Wired after the detail container is built (its cells need the holder, so the holder gets the
        // container second).
        public void AttachList(Container list) => _list = list;

        public void SetSource(JournalTaskUI source)
        {
            if (source == _source)
                return;
            _source = source;
            Subject = source.task;
            _list?.SetFocusedChild(null);
        }
    }

    /// <summary>The task description, the detail panel's first line. Read live; drops out when there is no
    /// focused task or it has no description.</summary>
    internal sealed class JournalDescriptionCell : UIElement
    {
        private readonly JournalDetail _detail;
        public JournalDescriptionCell(JournalDetail detail) => _detail = detail;

        public override bool CanFocus
            => _detail.Subject != null && !string.IsNullOrEmpty(JournalAdapter.ReadDescription(_detail.Subject));

        public override string GetFocusText() => JournalAdapter.ReadDescription(_detail.Subject);
    }

    /// <summary>One revealed subtask line, by index. Focusable only while the focused task has a subtask at
    /// this index, so a fixed set of these covers any task without a rebuild.</summary>
    internal sealed class JournalSubtaskCell : UIElement
    {
        private readonly JournalDetail _detail;
        private readonly int _index;

        public JournalSubtaskCell(JournalDetail detail, int index)
        {
            _detail = detail;
            _index = index;
        }

        public override bool CanFocus
            => _detail.Subject != null && JournalAdapter.SubtaskCount(_detail.Subject) > _index;

        public override string GetFocusText()
            => JournalAnnouncer.ComposeSubtask(JournalAdapter.ReadSubtask(_detail.Subject, _index));
    }

    /// <summary>The filed-time line. Present for any focused task.</summary>
    internal sealed class JournalFiledCell : UIElement
    {
        private readonly JournalDetail _detail;
        public JournalFiledCell(JournalDetail detail) => _detail = detail;

        public override bool CanFocus => _detail.Subject != null;

        public override string GetFocusText()
        {
            JournalTimesSnapshot s = JournalAdapter.ReadTimes(_detail.Subject);
            return JournalAnnouncer.ComposeFiled(s.FiledDay, s.FiledHour, s.FiledMinute);
        }
    }

    /// <summary>The resolution-time line ("completed"/"forfeited"). Focusable only when the focused task is
    /// resolved (an active task has no finish time).</summary>
    internal sealed class JournalResolutionCell : UIElement
    {
        private readonly JournalDetail _detail;
        public JournalResolutionCell(JournalDetail detail) => _detail = detail;

        public override bool CanFocus => _detail.Subject != null && JournalAdapter.IsResolved(_detail.Subject);

        public override string GetFocusText()
        {
            JournalTimesSnapshot s = JournalAdapter.ReadTimes(_detail.Subject);
            return JournalAnnouncer.ComposeResolution(s.Status, s.ResolvedDay, s.ResolvedHour, s.ResolvedMinute);
        }
    }
}
