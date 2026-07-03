using DiscoAccess.Core.UI;
using Sunshine.Journal;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Adapter: turns a live journal task, its detail lines, or a found white check into Unity-free data for
    /// Core to compose. Everything is read straight from the game model (the <see cref="Completeable"/> task,
    /// its <see cref="JournalTask.GainedSubtasks"/>, the <see cref="WhiteCheck"/>), not the UI text, so the
    /// strikethrough the UI paints onto a resolved task and the resolution line that reads stale for an
    /// active task never reach speech. The detail panel reads line by line, so each piece has its own read.
    /// Extraction only; nothing cached past the live read.
    /// </summary>
    public static class JournalAdapter
    {
        public static JournalTaskSnapshot ReadTask(Completeable t)
            => new JournalTaskSnapshot(t.LocalizedName, StatusOf(t), t.IsTimed);

        /// <summary>The task's localized description (the detail panel's first line).</summary>
        public static string ReadDescription(Completeable t) => t.LocalizedDescription;

        /// <summary>How many subtasks the task has revealed (each is its own detail line).</summary>
        public static int SubtaskCount(Completeable t)
        {
            JournalTask jt = t.TryCast<JournalTask>();
            return jt != null && jt.GainedSubtasks != null ? jt.GainedSubtasks.Count : 0;
        }

        /// <summary>The i-th revealed subtask's name and state.</summary>
        public static JournalSubtaskSnapshot ReadSubtask(Completeable t, int i)
        {
            JournalSubtask st = t.TryCast<JournalTask>().GainedSubtasks[i];
            return new JournalSubtaskSnapshot(st.LocalizedName, StatusOf(st));
        }

        public static bool IsResolved(Completeable t) => t.IsDone || t.IsCanceled;

        // The filed and (when resolved) finished times. The finish time is read only when the task is done or
        // cancelled (an active task's FinishTime is unset and throws if read).
        public static JournalTimesSnapshot ReadTimes(Completeable t)
        {
            SunshineClockTime filed = t.AquisitionTime;
            bool resolved = IsResolved(t);
            string resolvedDay = null;
            int resolvedHour = 0, resolvedMinute = 0;
            if (resolved)
            {
                SunshineClockTime fin = t.FinishTime;
                resolvedDay = fin.GetDayOfWeek().ToString();
                resolvedHour = fin.Hours;
                resolvedMinute = fin.Minutes;
            }
            return new JournalTimesSnapshot(StatusOf(t),
                filed.GetDayOfWeek().ToString(), filed.Hours, filed.Minutes,
                resolved, resolvedDay, resolvedHour, resolvedMinute);
        }

        public static WhiteCheckSnapshot ReadWhiteCheck(JournalWhiteCheckUI ui)
        {
            WhiteCheck w = ui.whiteCheck;
            // Actor name from the model reads in natural case; the skill name from the live label is already
            // localized and clean; the difficulty band is the game's word with its bracket framing stripped.
            // skillText and difficultyText are wired prefab fields, so they are read directly: a null would be
            // a wiring bug worth crashing on, not a state to paper over with a shortened announcement.
            return new WhiteCheckSnapshot(w.ActorName, GameLocalization.Spoken(ui.skillText),
                CleanDifficulty(GameLocalization.Spoken(ui.difficultyText)), Status(ui, w));
        }

        // A locked check (precondition unmet, the game greys it out) cannot be tried, even if it has been
        // engaged; a check only seen in the world is reported as such; everything else is open or reopened,
        // i.e. available to try now. Locked is checked first because it overrides the others.
        private static WhiteCheckStatus Status(JournalWhiteCheckUI ui, WhiteCheck w)
        {
            if (ui.IsCheckLocked())
                return WhiteCheckStatus.Locked;
            if (w.isOnlySeen)
                return WhiteCheckStatus.Seen;
            return WhiteCheckStatus.Available;
        }

        private static JournalTaskStatus StatusOf(Completeable t)
            => t.IsCanceled ? JournalTaskStatus.Cancelled
             : t.IsDone ? JournalTaskStatus.Done
             : JournalTaskStatus.Active;

        // The difficulty label is framed for display (": Medium]"); strip the colon and brackets to leave the
        // band word. Residual punctuation is handled by the speech filter.
        private static string CleanDifficulty(string raw)
            => string.IsNullOrEmpty(raw) ? raw : raw.Replace(":", "").Replace("[", "").Replace("]", "").Trim();
    }
}
