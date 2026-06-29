using System.Collections.Generic;
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

        /// <summary>Choose a player response through the game's own selection path, which advances the
        /// conversation - the next line announces itself through the screen's per-frame update.</summary>
        public static void SelectResponse(Response response)
            => DialogueManager.conversationView.SelectResponse(new SelectedResponseEventArgs(response));
    }
}
