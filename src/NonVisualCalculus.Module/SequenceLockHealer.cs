using NonVisualCalculus.Core.Modularity;
using Il2CppInterop.Runtime;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Heals the game's stranded dialogue input lock, a vanilla wedge with no vanilla recovery. When a
    /// conversation line carries sequence commands, <c>SequenceCommander.ReadyCutscene</c> counts them
    /// and holds a <c>typeof(SequenceCommander)</c> lock in <c>GameController.inputLocks</c>; each
    /// command's finish decrements the counter, and at zero the lock is removed. But a command killed
    /// early (a fast-forward click, an interrupted sequence) reports itself not-playing and never
    /// decrements, so the counter strands above zero and the lock stays forever - the game then ignores
    /// every world click, for mouse players too. The game ships the cure,
    /// <c>SequenceCommander.EmergencyForceDeblock()</c> (forces the counter down, removes the lock, and
    /// re-enables the continue button), but nothing in the game ever calls it.
    ///
    /// This watches for the stranded signature - the SequenceCommander lock held with no conversation
    /// active and no sequencer playing anywhere, a state that can no longer resolve itself - and after a
    /// short grace fires the game's own deblock. A legitimate post-dialogue outro (the window between a
    /// conversation ending and its last animations finishing) keeps its sequencer playing, so it never
    /// matches. Control returning is announced by the world reader (its "map" line), so the heal itself
    /// only logs.
    /// </summary>
    internal sealed class SequenceLockHealer
    {
        /// <summary>How long (seconds, unscaled) the stranded signature must hold before healing. The
        /// signature cannot resolve itself, so this only rides out frame-order transients (a sequence
        /// registered this frame whose sequencer starts the next).</summary>
        private const float StrandedGraceSeconds = 3f;

        private readonly IModHost _host;
        private float _strandedFor;

        public SequenceLockHealer(IModHost host) { _host = host; }

        /// <summary>Watch for the stranded lock and heal it; call once per frame from the pump.</summary>
        public void Tick()
        {
            if (!Stranded()) { _strandedFor = 0f; return; }
            _strandedFor += Time.unscaledDeltaTime;
            if (_strandedFor < StrandedGraceSeconds) return;
            _strandedFor = 0f;
            _host.LogWarning("SequenceLockHealer: the SequenceCommander input lock was stranded (no "
                + "conversation, no sequencer playing); firing the game's EmergencyForceDeblock.");
            SequenceCommander.EmergencyForceDeblock();
        }

        // The stranded signature: the dialogue-sequence lock held while nothing that could release it is
        // running. During a live conversation the counter can still be decremented by finishing commands,
        // and a post-dialogue outro keeps its sequencer playing until its last command completes; with
        // neither, the counter is stuck and the lock is permanent.
        private static bool Stranded()
        {
            Sunshine.GameController gc = Sunshine.GameController.Singleton;
            if (gc == null || !gc.HasInputLock(Il2CppType.From(typeof(SequenceCommander))))
                return false;
            if (DialogueManager.isConversationActive)
                return false;
            foreach (Sequencer s in Object.FindObjectsOfType<Sequencer>())
                if (s.isPlaying)
                    return false;
            return true;
        }
    }
}
