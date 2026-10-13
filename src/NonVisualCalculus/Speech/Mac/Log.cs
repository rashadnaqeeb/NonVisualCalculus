using System;

namespace NonVisualCalculus.Speech.Mac
{
    /// <summary>
    /// Logging seam. Core code logs through here; the plugin installs sinks that route
    /// to the BepInEx logger. Keeping the sinks injectable lets Core stay free of any
    /// BepInEx/Unity reference and lets tests capture output.
    ///
    /// The mod runs on P/Invoke and Harmony patches that fail invisibly, so failures
    /// must be logged, never swallowed.
    /// </summary>
    public static class Log
    {
        private static Action<string> _info;
        private static Action<string> _warn;
        private static Action<string> _error;

        public static void Install(Action<string> info, Action<string> warn, Action<string> error)
        {
            _info = info;
            _warn = warn;
            _error = error;
        }

        /// <summary>Give the sinks back. They are delegates over the plugin host, so a mod that has
        /// unloaded without this leaves its dead assembly's static holding the live loader's logger -
        /// the same asymmetry every other Start/Stop pair here is careful about. Called LAST in
        /// teardown: nothing after it can log.</summary>
        public static void Reset()
        {
            _info = null;
            _warn = null;
            _error = null;
        }

        public static void Info(string message)
        {
            if (_info != null)
            {
                _info(message);
            }
        }

        public static void Warn(string message)
        {
            if (_warn != null)
            {
                _warn(message);
            }
        }

        public static void Error(string message)
        {
            if (_error != null)
            {
                _error(message);
            }
        }
    }
}
