namespace NonVisualCalculus.Core.UI
{
    /// <summary>
    /// A task's (or subtask's) resolution state, for speech. The module adapter maps the game's
    /// <c>Completeable</c> booleans (IsCanceled, IsDone) onto this so the announcer's word choice stays in
    /// Core and unit-testable. Cancelled is checked before done, matching the game (a cancelled task reads
    /// "forfeited", never "completed").
    /// </summary>
    public enum JournalTaskStatus
    {
        Active,     // ongoing, not yet resolved
        Done,       // completed
        Cancelled,  // forfeited
    }

    /// <summary>
    /// Unity-free snapshot of one task in the journal's task list, extracted by the module adapter and
    /// composed for the list row by <see cref="JournalAnnouncer.ComposeTask"/>. <see cref="Name"/> is the
    /// game's own localized task name (read from the model, so it carries none of the strikethrough the UI
    /// paints onto a resolved task).
    /// </summary>
    public sealed class JournalTaskSnapshot
    {
        public string Name { get; }
        public JournalTaskStatus Status { get; }
        public bool Timed { get; }

        public JournalTaskSnapshot(string name, JournalTaskStatus status, bool timed)
        {
            Name = name;
            Status = status;
            Timed = timed;
        }
    }

    /// <summary>One revealed subtask of a task, for the detail readout: its localized name and its own
    /// resolution state.</summary>
    public sealed class JournalSubtaskSnapshot
    {
        public string Name { get; }
        public JournalTaskStatus Status { get; }

        public JournalSubtaskSnapshot(string name, JournalTaskStatus status)
        {
            Name = name;
            Status = status;
        }
    }

    /// <summary>
    /// Unity-free snapshot of the focused task's filed and resolved times (the detail panel reads them as
    /// their own lines via <see cref="JournalAnnouncer.ComposeFiled"/> and
    /// <see cref="JournalAnnouncer.ComposeResolution"/>). The game's resolution TMP reads stale for an
    /// active task, so the lines are composed from the model. Times are in-game day-of-week and clock;
    /// <see cref="ResolvedDay"/> and the resolved clock are meaningful only when <see cref="Resolved"/> is
    /// true (an active task has no finish time).
    /// </summary>
    public sealed class JournalTimesSnapshot
    {
        public JournalTaskStatus Status { get; }
        public string FiledDay { get; }
        public int FiledHour { get; }
        public int FiledMinute { get; }
        public bool Resolved { get; }
        public string ResolvedDay { get; }
        public int ResolvedHour { get; }
        public int ResolvedMinute { get; }

        public JournalTimesSnapshot(JournalTaskStatus status, string filedDay, int filedHour, int filedMinute,
            bool resolved, string resolvedDay, int resolvedHour, int resolvedMinute)
        {
            Status = status;
            FiledDay = filedDay;
            FiledHour = filedHour;
            FiledMinute = filedMinute;
            Resolved = resolved;
            ResolvedDay = resolvedDay;
            ResolvedHour = resolvedHour;
            ResolvedMinute = resolvedMinute;
        }
    }

    /// <summary>A found white check's current state for the player: whether it can be attempted now, is
    /// locked behind an unmet precondition, or has only been spotted in the world.</summary>
    public enum WhiteCheckStatus
    {
        Available,  // open or reopened: can be attempted now (the game's white state)
        Locked,     // discovered but its precondition is not yet met, so it cannot be tried
        Seen,       // only spotted in the world, not yet engaged
    }

    /// <summary>
    /// Unity-free snapshot of one found white check in the journal's map tab, composed by
    /// <see cref="JournalAnnouncer.ComposeWhiteCheck"/>. <see cref="Actor"/> (the thing the check is on),
    /// <see cref="Skill"/> and <see cref="Difficulty"/> (the difficulty band word) are the game's own
    /// localized text, the three things the journal row shows; <see cref="Status"/> is whether it can be
    /// attempted now, is locked, or has only been seen. The numeric target and the modifier breakdown are
    /// not on this screen (they appear on the actual check in the world), so they are not read here.
    /// </summary>
    public sealed class WhiteCheckSnapshot
    {
        public string Actor { get; }
        public string Skill { get; }
        public string Difficulty { get; }
        public WhiteCheckStatus Status { get; }

        public WhiteCheckSnapshot(string actor, string skill, string difficulty, WhiteCheckStatus status)
        {
            Actor = actor;
            Skill = skill;
            Difficulty = difficulty;
            Status = status;
        }
    }
}
