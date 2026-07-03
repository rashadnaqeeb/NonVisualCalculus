using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken lines for the journal screen from its Unity-free snapshots. The task list row
    /// leads with the task name (the distinguishing word, since the player scans the list), then its status
    /// and a "timed" marker. The detail panel is navigated line by line, so its description, each subtask,
    /// the filed time and the resolved time are each composed on their own here. A white check reads the
    /// thing it is on, the skill, its difficulty, and whether it can be attempted now. Names, descriptions
    /// and difficulty words are the game's own localized text; the status, label and availability words are
    /// mod-authored.
    /// </summary>
    public static class JournalAnnouncer
    {
        public static string ComposeTask(JournalTaskSnapshot t)
            => Text.SpokenLine.Join(t.Name, StatusWord(t.Status), t.Timed ? JournalTimed : null);

        // One subtask line in the detail panel: its name, plus its state when resolved.
        public static string ComposeSubtask(JournalSubtaskSnapshot st)
            => Text.SpokenLine.Join(st.Name, st.Status == JournalTaskStatus.Active ? null : StatusWord(st.Status));

        // The filed line: "filed {day} {clock}".
        public static string ComposeFiled(string day, int hour, int minute)
            => JournalFiled + " " + Clock(day, hour, minute);

        // The resolution line: "completed {day} {clock}" or "forfeited {day} {clock}".
        public static string ComposeResolution(JournalTaskStatus status, string day, int hour, int minute)
            => ResolutionWord(status) + " " + Clock(day, hour, minute);

        public static string ComposeWhiteCheck(WhiteCheckSnapshot c)
            => Text.SpokenLine.Join(c.Actor, c.Skill, c.Difficulty, CheckStatusWord(c.Status));

        private static string CheckStatusWord(WhiteCheckStatus s)
        {
            switch (s)
            {
                case WhiteCheckStatus.Locked: return JournalCheckLocked;
                case WhiteCheckStatus.Seen: return JournalCheckSeen;
                default: return JournalCheckAvailable;
            }
        }

        private static string StatusWord(JournalTaskStatus s)
        {
            switch (s)
            {
                case JournalTaskStatus.Done: return JournalStatusDone;
                case JournalTaskStatus.Cancelled: return JournalStatusCancelled;
                default: return JournalStatusActive;
            }
        }

        // The resolved-line verb: a completed task reads "completed", a cancelled one "forfeited" (the
        // game's own wording for a forfeited task).
        private static string ResolutionWord(JournalTaskStatus s)
            => s == JournalTaskStatus.Cancelled ? JournalForfeited : JournalCompleted;

        // The day word is game text and can arrive display-shaped; it composes with authored labels,
        // so it must invert here, within its own part.
        private static string Clock(string day, int hour, int minute)
            => Text.RtlText.Unfix(day) + " " + hour + ":" + minute.ToString("00");
    }
}
