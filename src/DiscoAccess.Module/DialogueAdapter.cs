using System.Collections.Generic;
using DiscoAccess.Core.Modularity;        // IModHost
using DiscoPages;                         // DialogueBridgePages
using PixelCrushers.DialogueSystem;       // DialogueManager, ConversationState, Response, SelectedResponseEventArgs
using ConversationLogger = Sunshine.ConversationLogger;
using UnityEngine;
// LogEntry, LogRenderer, SunshineContinueButton, ContState, FinalEntry live in the global namespace.

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

        /// <summary>Choose a player response by clicking its own on-screen button, the game's real click
        /// path. For a skill check that runs the full pipeline - rolls the dice, locks a white check, plays
        /// the dice animation, and records the result on the outcome line - which the bare
        /// <c>conversationView.SelectResponse</c> skips, leaving the check unrolled and re-selectable. Match
        /// the button to the response by its destination entry, since two interop proxies of one response
        /// are not reference-equal. Falls back to the conversation API (logged) if no button is found, so a
        /// plain response still advances even though a check there would not roll.</summary>
        public static void SelectResponse(Response response, IModHost host)
        {
            DialogueEntry want = response != null ? response.destinationEntry : null;
            if (want != null)
                foreach (SunshineResponseButton b in Resources.FindObjectsOfTypeAll<SunshineResponseButton>())
                {
                    if (b == null || !b.gameObject.activeInHierarchy || b.button == null)
                        continue;
                    DialogueEntry have = b.response != null ? b.response.destinationEntry : null;
                    if (have == null || have.id != want.id || have.conversationID != want.conversationID)
                        continue;
                    b.button.onClick.Invoke();
                    return;
                }
            host?.LogWarning("DialogueAdapter: no on-screen button matched the selected response; a check there "
                + "will not roll. Falling back to the conversation API.");
            DialogueManager.conversationView.SelectResponse(new SelectedResponseEventArgs(response));
        }
    }
}
