using System.Collections.Generic;
using DiscoAccess.Core.Modularity;        // IModHost
using DiscoAccess.Core.Strings;
using DiscoPages;                         // DialogueBridgePages
using PixelCrushers.DialogueSystem;       // DialogueManager, ConversationState, Response, SelectedResponseEventArgs
using ConversationLogger = Sunshine.ConversationLogger;
using UnityEngine;
// LogEntry, LogRenderer, SunshineContinueButton, ContState, FinalEntry, and the tooltip data types
// (TooltipDataHolder, TooltipData, CostTooltipData) live in the global namespace.

namespace DiscoAccess.Module
{
    /// <summary>
    /// Thin live reader for an in-game conversation (the Pixel Crushers Dialogue System wrapped by DE's
    /// <see cref="ConversationLogger"/>). It extracts the live state the dialogue screen reads each rebuild -
    /// the on-screen transcript entries, the current player responses, and whether a continue is pending -
    /// and runs the game's own advance paths (select a response, click continue) so sounds and conversation
    /// flow happen exactly as a mouse click would. Holds nothing; every accessor re-queries the game.
    /// </summary>
    internal static class DialogueAdapter
    {
        /// <summary>DE's live conversation logger (the Sunshine variant this build uses), via the bridge that
        /// resolves it whether the conversation runs in an area or on the world map. Null between conversations.</summary>
        public static ConversationLogger Logger() => DialogueBridgePages.ConversationLogger;

        /// <summary>The live conversation state (current line + responses), or null when none is running.</summary>
        public static ConversationState State() => DialogueManager.currentConversationState;

        /// <summary>The on-screen transcript, oldest first, current line last. Reads the rendered log entries
        /// under the log panel (the same scrollback the player would see) and drops the empty pooled template
        /// row, so the last entry returned is always the line currently delivered.</summary>
        public static List<LogEntry> TranscriptEntries()
        {
            var list = new List<LogEntry>();
            LogRenderer lr = Logger()?.logRenderer;
            RectTransform panel = lr != null ? lr.logPanel : null;
            if (panel == null)
                return list;
            foreach (LogEntry e in panel.GetComponentsInChildren<LogEntry>())
                if (HasText(e))
                    list.Add(e);
            return list;
        }

        // A rendered log row carries text when its paragraph has rendered something or its entry holds a
        // spoken line; the pooled template row has neither and is skipped.
        private static bool HasText(LogEntry e)
        {
            if (e == null)
                return false;
            var lt = e.logText;
            if (lt != null && !string.IsNullOrEmpty(lt.text))
                return true;
            FinalEntry fe = e.Entry;
            return fe != null && !string.IsNullOrEmpty(fe.spokenLine);
        }

        /// <summary>Whether a continue is currently available to advance the conversation (the game's continue
        /// button is in any state but disabled - it is disabled precisely while a response menu is up).</summary>
        public static bool ContinueAvailable()
        {
            SunshineContinueButton cb = Logger()?.continueButton;
            return cb != null && cb.State != ContState.DISABLED;
        }

        /// <summary>Advance the conversation through the game's own continue handler (plays its sound and
        /// runs the same path the on-screen continue button would).</summary>
        public static void Continue() => Logger()?.continueButton?.WasClicked();

        /// <summary>Whether the current line's sequence is still playing. A continue sent while the
        /// sequence runs fast-forwards the line (the game's continue handler calls Sequencer.Stop)
        /// instead of advancing to the next entry, so an auto-advance must wait for this to be false or
        /// its single continue is spent on the fast-forward and the conversation wedges.</summary>
        public static bool SequencePlaying()
        {
            ConversationView view = DialogueManager.conversationView;
            Sequencer seq = view != null ? view.sequencer : null;
            return seq != null && seq.IsPlaying;
        }

        /// <summary>The on-screen button rendering a response, matched by its destination entry (two
        /// interop proxies of one response are not reference-equal), or null when none is on screen.</summary>
        public static SunshineResponseButton FindButton(Response response)
        {
            DialogueEntry want = response != null ? response.destinationEntry : null;
            if (want == null)
                return null;
            foreach (SunshineResponseButton b in Resources.FindObjectsOfTypeAll<SunshineResponseButton>())
            {
                if (b == null || !b.gameObject.activeInHierarchy)
                    continue;
                DialogueEntry have = b.response != null ? b.response.destinationEntry : null;
                if (have != null && have.id == want.id && have.conversationID == want.conversationID)
                    return b;
            }
            return null;
        }

        /// <summary>The game's own computed data for a money response - the cost and, for a purchase, the
        /// resolved <c>InventoryItem</c> - read live from the response button's tooltip holder, the same
        /// data its hover tooltip shows a sighted player. Null when the button is not on screen or the
        /// response carries no cost data.</summary>
        public static CostTooltipData CostData(Response response)
        {
            SunshineResponseButton b = FindButton(response);
            TooltipDataHolder holder = b != null ? b.GetComponent<TooltipDataHolder>() : null;
            TooltipData data = holder != null ? holder.tooltipData : null;
            return data != null ? data.costTooltipData : null;
        }

        /// <summary>Choose a player response by clicking its own on-screen button, the game's real click
        /// path. For a skill check that runs the full pipeline - rolls the dice, locks a white check, plays
        /// the dice animation, and records the result on the outcome line - which the bare
        /// <c>conversationView.SelectResponse</c> skips, leaving the check unrolled and re-selectable.
        /// Falls back to the conversation API (logged) if no button is found, so a plain response still
        /// advances even though a check there would not roll.</summary>
        public static void SelectResponse(Response response, IModHost host)
        {
            // While a line's sequence commands play, the game stashes the whole options panel off the
            // layout (ContinueResponseToggle, driven by this same flag) - a sighted player has no
            // buttons to click until the sequence finishes. Committing a response through the model in
            // that window tears down the running sequence, whose killed commands then never report
            // their finish to SequenceCommander, stranding its input lock PERMANENTLY (the wedge that
            // freezes world clicks for the rest of the session). Refuse exactly while the game hides
            // the menu, spoken so the early press is never a silent dead key.
            if (ContinueResponseToggle.SequencerLock)
            {
                host?.Speech.Speak(Strings.DialogueNotReady, interrupt: true);
                return;
            }
            SunshineResponseButton b = FindButton(response);
            if (b != null && b.button != null)
            {
                // Invoking onClick skips uGUI's clickability test (Button.interactable and every parent
                // CanvasGroup) - the layer that physically stops a sighted player's click while the menu
                // is still fading in. Refuse those presses the same way the screen refuses the mouse.
                // The handler's own guards (the game's input-delay window, the dialogue-view check,
                // response.enabled) still run inside the invoke.
                if (!b.button.IsInteractable())
                {
                    host?.Speech.Speak(Strings.DialogueNotReady, interrupt: true);
                    return;
                }
                b.button.onClick.Invoke();
                return;
            }
            host?.LogWarning("DialogueAdapter: no on-screen button matched the selected response; a check there "
                + "will not roll. Falling back to the conversation API.");
            DialogueManager.conversationView.SelectResponse(new SelectedResponseEventArgs(response));
        }
    }
}
