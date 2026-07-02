using System;
using DiscoAccess.Core.Text;

namespace DiscoAccess.Core.Speech
{
    /// <summary>
    /// The single funnel for everything the mod says. Owns policy: clean the text (strip TMP markup)
    /// and route to the backend. Callers never touch the backend directly. House rule (from the
    /// reference mods): navigation interrupts, ambient announcements queue.
    /// </summary>
    public sealed class SpeechPipeline
    {
        /// <summary>Set once by the plugin at load; null in unit tests that construct their own.</summary>
        public static SpeechPipeline? Instance { get; set; }

        /// <summary>
        /// Optional tap invoked with (text, interrupt, source) for every line that clears the clean
        /// gate, so it sees exactly what was voiced. The dev server sets this to read spoken text back
        /// (it can't hear the TTS). Source is the speaking class's name (the first stack frame outside
        /// this pipeline), so the dev driver can attribute a line to its reader; it is computed only
        /// while the tap is attached. Null in normal play and in unit tests.
        /// </summary>
        public static Action<string, bool, string?>? Spoken;

        private readonly ISpeechBackend _backend;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// When set, the backend is not driven but the <see cref="Spoken"/> tap still fires, so a
        /// headless/overnight dev run reads back through /speech without depending on a screen reader.
        /// Set by the host from DISCOACCESS_NO_SPEECH. Distinct from <see cref="Enabled"/>, which gates
        /// the line entirely (tap included).
        /// </summary>
        public bool Muted { get; set; }

        public SpeechPipeline(ISpeechBackend backend)
        {
            _backend = backend;
        }

        public void Speak(string? text, bool interrupt = false)
        {
            if (!Enabled)
                return;

            string clean = TextFilter.Clean(text);
            if (clean.Length == 0)
                return;

            if (!Muted)
                _backend.Speak(clean, interrupt);
            Spoken?.Invoke(clean, interrupt, Spoken != null ? CallerName() : null);
        }

        // The speaking class, for the dev tap's attribution: the first stack frame whose type is not
        // this pipeline, with compiler-generated closure types (a lambda handed to a walk verb) resolved
        // to the class that declared them. Dev-only cost: computed only while the tap is attached, and
        // speech is low-frequency. Null when the walk finds nothing readable.
        private static string? CallerName()
        {
            var trace = new System.Diagnostics.StackTrace(2, false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                Type? type = trace.GetFrame(i)?.GetMethod()?.DeclaringType;
                while (type != null && type.Name.StartsWith("<", StringComparison.Ordinal))
                    type = type.DeclaringType; // a closure/state-machine; name its declaring class
                if (type == null || type == typeof(SpeechPipeline)) continue;
                return type.Name;
            }
            return null;
        }

        public void Stop()
        {
            _backend.Stop();
        }
    }
}
