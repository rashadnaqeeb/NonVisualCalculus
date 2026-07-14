using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.Text;
using NonVisualCalculus.Core.UI.Nav;
using NonVisualCalculus.Module.Input; // DialogueChoiceInput
using PixelCrushers.DialogueSystem; // ConversationState, Response, Subtitle
using Sunshine.Views;
using ConversationLogger = Sunshine.ConversationLogger;

namespace NonVisualCalculus.Module.Nav
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

        // Nothing to search through in a conversation, so bare letters are freed: type-ahead off, and the
        // world status reads (t/m/h) and Left/Right quick-heals are wanted here (health can run out mid-talk).
        public override bool TypeAheadEnabled => false;
        public override bool WantsStatusKeys => true;

        // The number-row jump below lives outside the input registry, so the key-help screen is told
        // about it here.
        public override IEnumerable<string> KeyHelpLines()
        {
            yield return Strings.KeyHelpLine(Strings.InputDialogueNumberJump, Strings.KeyHelpKeysDigits);
        }

        private Container _root;
        private Container _flow;
        private UIElement _landing;
        // The numbered choices at the foot of the flow, in on-screen order: the player responses, or the
        // lone continue when it is the only way forward (the two are mutually exclusive - DE disables the
        // continue while a response menu is up). Rebuilt with the flow; drives the number-row jump.
        private readonly List<UIElement> _choices = new List<UIElement>();
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
        // When the stranded-sequencer state was first seen this stretch, or -1 while healthy. The state
        // must persist before we recover: a line start briefly raises the lock before its sequence begins.
        private float _wedgeSince = -1f;
        private const float WedgeConfirmSeconds = 0.5f;

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
            // A continue sent mid-sequence (our continue path can, the on-screen button cannot) makes the
            // game kill the sequence commands its lock is counting, so the lock never clears and every
            // response select refuses from then on. Fire the game's own recovery once the stranded state
            // (lock up, sequencer idle) outlives a legitimate line start.
            if (DialogueAdapter.SequencerWedged())
            {
                float now = UnityEngine.Time.unscaledTime;
                if (_wedgeSince < 0f)
                    _wedgeSince = now;
                else if (now - _wedgeSince >= WedgeConfirmSeconds)
                {
                    host.LogWarning("DialogueScreen: sequencer lock stranded (lock up, sequencer idle); "
                        + "firing the game's EmergencyForceDeblock");
                    DialogueAdapter.RecoverWedgedSequencer();
                    _wedgeSince = -1f;
                }
            }
            else
                _wedgeSince = -1f;
            // A player line the game is holding for a continue is the player's own choice echoed as a
            // subtitle (we do not read it back); advancing it manually has no payoff and stalls the reader
            // silently, so auto-advance past it to the line that follows. Fire once per such line, and only
            // once its sequence has finished: a continue sent mid-sequence fast-forwards the line instead of
            // advancing, spending our single continue without moving on and wedging the conversation.
            Subtitle sub = DialogueAdapter.State()?.subtitle;
            if (sub != null && sub.speakerInfo != null && sub.speakerInfo.isPlayer
                && DialogueAdapter.ContinueAvailable() && !DialogueAdapter.SequencePlaying()
                && DialogueAdapter.ContinueOnScreen())
            {
                string playerLine = sub.formattedText != null ? sub.formattedText.text : null;
                if (playerLine != _autoContinuedLine)
                {
                    _autoContinuedLine = playerLine;
                    DialogueAdapter.Continue(host);
                    return false;
                }
            }

            // Read the current line (the NPC line or forced narration) once each time it changes, queued so
            // nothing is interrupted. A player's chosen response is not a subtitle, so it never lands here.
            // The Automatically read dialogue setting gates the auto-speak: when off we still mark the line
            // seen and let the rebuild below land the cursor on it silently, leaving the player to read it on
            // their own terms (Up re-reads it), but never queue it as the conversation advances.
            string line = CurrentLine();
            if (!string.IsNullOrEmpty(line) && line != _lastSpokenLine)
            {
                if (host.Settings.AutoReadDialogue.Value)
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

            // Number-row jump: press N to move the cursor straight to the Nth choice (a response, or the
            // continue when it is the only way forward), read like any move. Reachable from anywhere in the
            // flow, so a choice is one keypress away without arrowing past the transcript. A number past the
            // choice count does nothing - silence, as asked.
            int digit = DialogueChoiceInput.PressedChoiceDigit();
            if (digit >= 1 && digit <= _choices.Count && _choices[digit - 1].CanFocus)
                nav.Focus(_choices[digit - 1], announce: true);

            return false;
        }

        // Rebuild the flow from live state: a transcript line per rendered log row (the last one carries the
        // continue action), then a cell per player response. Records the cell to land on (the current line,
        // else the first response when the log is momentarily empty).
        private void Rebuild()
        {
            _flow.Clear();
            _landing = null;
            _choices.Clear();

            ConversationLogger logger = DialogueAdapter.Logger();
            List<LogEntry> entries = DialogueAdapter.TranscriptEntries();
            DialogueLineCell current = null;
            for (int i = 0; i < entries.Count; i++)
            {
                bool last = i == entries.Count - 1;
                // A line that resolved a check gets a silent roll line above it: the dice and modifiers the
                // game's own outcome line (skill, difficulty, success/failure) leaves out, read with Up. A
                // passive check resolves without dice, so its line is the bare total against the target.
                FinalEntry fe = entries[i].Entry;
                if (fe != null && fe.HasCheck && fe.checkResult != null
                    && (fe.checkResult.HasRoll() || fe.checkResult.checkType == Sunshine.Metric.CheckType.PASSIVE))
                    _flow.Add(new DialogueCheckRollCell(fe.checkResult));
                var cell = last
                    ? new DialogueLineCell(entries[i], DialogueAdapter.ContinueAvailable, () => DialogueAdapter.Continue(_host))
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
                _choices.Add(cell);
                if (firstResponse == null && cell.CanFocus)
                    firstResponse = cell;
            }

            // With no choices but an available continue, offer it as a navigable button below the current
            // line (Down to reach it) so advancing the conversation is discoverable, not just a hidden Enter.
            // It is choice 1 for the number-row jump in this continue-only state.
            if (responseCount == 0 && DialogueAdapter.ContinueAvailable())
            {
                var continueCell = new DialogueContinueCell(DialogueAdapter.ContinueAvailable, () => DialogueAdapter.Continue(_host));
                _flow.Add(continueCell);
                _choices.Add(continueCell);
            }

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

