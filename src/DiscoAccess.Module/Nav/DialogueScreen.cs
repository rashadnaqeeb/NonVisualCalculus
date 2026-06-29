using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using PixelCrushers.DialogueSystem; // ConversationState, Response, Subtitle
using Sunshine.Views;
using ConversationLogger = Sunshine.ConversationLogger;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// An in-game character conversation, read as one flowing transcript that mirrors the reference mod's
    /// dialogue: the scrollback (the game's own rendered log lines, oldest first) ending in the current
    /// line, then the player's response choices. A new line rebuilds the flow, lands focus silently on the
    /// current line, and speaks the newly delivered line QUEUED - nothing in dialogue ever interrupts, so the
    /// screen reader never cuts the line off. From the current line the player presses Down to reach the
    /// responses (or Enter to advance when a continue is the only way forward), and Up to re-read earlier
    /// lines. Entry is silent (no screen name); the current line speaks itself through the landing announce.
    ///
    /// Only the conversation's current line (its subtitle - an NPC line or forced narration) is read on
    /// delivery; a player response the user just chose is never a subtitle, so it is never read back (they
    /// clicked it and know it), though it stays in the scrollback for Up to review.
    ///
    /// Escape is not consumed here (the root advertises no Back), so it falls through to the game's own
    /// Escape, which opens the pause menu mid-conversation the same as vanilla.
    /// </summary>
    public sealed class DialogueScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.DIALOGUE;

        // Empty so entering a conversation announces nothing of its own - the current line speaks itself via
        // the landing announce, and a spoken "dialogue" would only cut into the line and break immersion.
        public override string ScreenName => string.Empty;

        private Container _root;
        private Container _flow;
        private UIElement _landing;
        private string _builtSig;
        // Held so the response cells' select action can route through the game's button click and log a miss.
        private IModHost _host;
        // The current line we have already spoken, so the per-frame update reads each new line exactly once.
        // Keyed on the conversation's subtitle, never the transcript tail, so a player response (logged but
        // never a subtitle) is not read back.
        private string _lastSpokenLine;
        // The player line we last auto-advanced past, so a player line the game holds for a continue is
        // advanced exactly once (not re-fired each frame until the state settles, which would skip a line).
        private string _autoContinuedLine;

        public override Container BuildRoot(IModHost host)
        {
            _host = host;
            _root = new Container(ContainerShape.Panel);
            _flow = new Container(ContainerShape.VerticalList);
            _root.Add(_flow);
            Rebuild();
            _builtSig = Signature();
            // The current line is already on screen as we enter; mark it spoken so the per-frame update does
            // not replay it - the ScreenManager's landing announce reads it once.
            _lastSpokenLine = CurrentLine();
            return _root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            _host = host;
            // A player line the game is holding for a continue is the player's own choice echoed as a
            // subtitle (we do not read it back); advancing it manually has no payoff and stalls the reader
            // silently, so auto-advance past it to the line that follows. Fire once per such line.
            Subtitle sub = DialogueAdapter.State()?.subtitle;
            if (sub != null && sub.speakerInfo != null && sub.speakerInfo.isPlayer && DialogueAdapter.ContinueAvailable())
            {
                string playerLine = sub.formattedText != null ? sub.formattedText.text : null;
                if (playerLine != _autoContinuedLine)
                {
                    _autoContinuedLine = playerLine;
                    DialogueAdapter.Continue();
                    return false;
                }
            }

            // Read the current line (the NPC line or forced narration) once each time it changes, queued so
            // nothing is interrupted. A player's chosen response is not a subtitle, so it never lands here.
            string line = CurrentLine();
            if (!string.IsNullOrEmpty(line) && line != _lastSpokenLine)
            {
                host.Speech.Speak(line, interrupt: false);
                _lastSpokenLine = line;
            }

            // Rebuild the flow when the conversation moved (a new line, or a response menu appeared/changed),
            // and land silently on the current line - we have already spoken it, and the ScreenManager must
            // not re-announce interrupting.
            string sig = Signature();
            if (sig != _builtSig)
            {
                Rebuild();
                _builtSig = sig;
                if (_landing != null)
                    nav.Focus(_landing, announce: false);
                else
                    nav.EnsureFocusValid();
            }
            return false;
        }

        // Rebuild the flow from live state: a transcript line per rendered log row (the last one carries the
        // continue action), then a cell per player response. Records the cell to land on (the current line,
        // else the first response when the log is momentarily empty).
        private void Rebuild()
        {
            _flow.Clear();
            _landing = null;

            ConversationLogger logger = DialogueAdapter.Logger();
            List<LogEntry> entries = DialogueAdapter.TranscriptEntries();
            DialogueLineCell current = null;
            for (int i = 0; i < entries.Count; i++)
            {
                bool last = i == entries.Count - 1;
                // A line that resolved a check gets a silent roll line above it: the dice and modifiers the
                // game's own outcome line (skill, difficulty, success/failure) leaves out, read with Up.
                FinalEntry fe = entries[i].Entry;
                if (fe != null && fe.HasCheck && fe.checkResult != null && fe.checkResult.HasRoll())
                    _flow.Add(new DialogueCheckRollCell(fe.checkResult));
                var cell = last
                    ? new DialogueLineCell(entries[i], DialogueAdapter.ContinueAvailable, DialogueAdapter.Continue)
                    : new DialogueLineCell(entries[i]);
                _flow.Add(cell);
                if (last)
                    current = cell;
            }

            ConversationState state = DialogueAdapter.State();
            var responses = state != null ? state.pcResponses : null;
            int responseCount = responses != null ? responses.Length : 0;
            DialogueResponseCell firstResponse = null;
            for (int i = 0; i < responseCount; i++)
            {
                Response r = responses[i];
                var cell = new DialogueResponseCell(logger, r, i + 1, () => DialogueAdapter.SelectResponse(r, _host));
                _flow.Add(cell);
                if (firstResponse == null && cell.CanFocus)
                    firstResponse = cell;
            }

            // With no choices but an available continue, offer it as a navigable button below the current
            // line (Down to reach it) so advancing the conversation is discoverable, not just a hidden Enter.
            if (responseCount == 0 && DialogueAdapter.ContinueAvailable())
                _flow.Add(new DialogueContinueCell(DialogueAdapter.ContinueAvailable, DialogueAdapter.Continue));

            _landing = (UIElement)current ?? firstResponse;
            if (_landing != null)
            {
                _flow.SetFocusedChild(_landing);
                _root.SetFocusedChild(_flow);
            }
        }

        // A cheap per-frame fingerprint of "the conversation moved": the rendered line count, the current
        // line's spoken text, and the response count. Stable while the player browses the standing menu, so
        // navigation never triggers a rebuild; changes the instant a line is delivered or a menu appears.
        private string Signature()
        {
            List<LogEntry> entries = DialogueAdapter.TranscriptEntries();
            string lastLine = string.Empty;
            if (entries.Count > 0)
            {
                FinalEntry fe = entries[entries.Count - 1].Entry;
                lastLine = fe != null ? fe.spokenLine : null;
            }
            ConversationState state = DialogueAdapter.State();
            int responseCount = state != null && state.pcResponses != null ? state.pcResponses.Length : 0;
            // Continue availability is part of the fingerprint so the continue button appears the moment it
            // becomes available, even when that lands a frame after the line itself (the count is unchanged).
            return entries.Count + "|" + lastLine + "|" + responseCount + "|" + DialogueAdapter.ContinueAvailable();
        }

        // The conversation's current line to read on delivery, cleaned for speech. A player line - both a
        // chosen response, which DE momentarily shows as the protagonist's own subtitle, and any forced
        // protagonist line - is skipped (the player drove it and does not need it read back); the inner-voice
        // skills are separate non-player speakers, so they still read. The skipped line still appears in the
        // scrollback for Up to review. The text is composed from the current transcript row, so delivery and
        // Up-review read a line identically (speaker, any check tag, line), falling back to the bare subtitle
        // before the matching log row exists.
        private static string CurrentLine()
        {
            Subtitle sub = DialogueAdapter.State()?.subtitle;
            if (sub == null || (sub.speakerInfo != null && sub.speakerInfo.isPlayer))
                return null;
            List<LogEntry> entries = DialogueAdapter.TranscriptEntries();
            if (entries.Count > 0)
                return TextFilter.Clean(DialogueLineCell.Raw(entries[entries.Count - 1]));
            string text = sub.formattedText != null ? sub.formattedText.text : null;
            if (string.IsNullOrEmpty(text))
                return null;
            string speaker = sub.speakerInfo != null ? sub.speakerInfo.Name : null;
            return TextFilter.Clean(string.IsNullOrEmpty(speaker) ? text : speaker + ": " + text);
        }
    }
}

