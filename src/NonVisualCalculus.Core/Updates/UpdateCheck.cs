using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace NonVisualCalculus.Core.Updates
{
    /// <summary>
    /// The launch update check: one background fetch of the mod's newest GitHub release, compared to
    /// the running version. The module starts it from <c>Load</c> and consumes the one pending
    /// announcement from <c>Tick</c> via <see cref="TryTakeUpdate"/>; nothing is pending when the mod
    /// is up to date (silence, by design) or the check fails (logged only - the player can do nothing
    /// about a network error at launch). Static and in Core ON PURPOSE: Core loads once in the default
    /// context, so the once-per-process latch and the pending result survive a module hot-reload,
    /// which would otherwise re-check and re-announce on every F6.
    /// </summary>
    public static class UpdateCheck
    {
        // The same release feed the installer reads; its zip asset carries the version the tag names.
        private const string ApiUrl = "https://api.github.com/repos/rashadnaqeeb/NonVisualCalculus/releases/latest";

        private static readonly Regex TagPattern = new Regex(
            "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"", RegexOptions.Compiled);

        private static bool _started;
        // Set once by the worker thread when a newer release exists; consumed once by TakeUpdate.
        private static volatile string? _available;

        /// <summary>Fetch the newest release version in the background and stage the announcement if
        /// it is newer than <paramref name="currentVersion"/>. First call wins for the process; later
        /// calls (module hot-reloads) are no-ops.</summary>
        public static void Start(string currentVersion, Action<string> logInfo, Action<string> logWarning)
        {
            if (_started) return;
            _started = true;

            var worker = new Thread(() =>
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                    request.UserAgent = "NonVisualCalculus";
                    request.Timeout = 10000;
                    string json;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                        json = reader.ReadToEnd();

                    string? latest = ParseLatestVersion(json);
                    if (latest == null)
                    {
                        logWarning("Update check: could not parse a release version from the GitHub response");
                    }
                    else if (IsNewer(currentVersion, latest))
                    {
                        logInfo($"Update check: {latest} is available (running {currentVersion})");
                        _available = latest;
                    }
                    else
                    {
                        logInfo($"Update check: up to date ({currentVersion}, newest release {latest})");
                    }
                }
                catch (Exception ex)
                {
                    logWarning($"Update check failed: {ex.Message}");
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>Hand over the staged newer version exactly once (main thread, from the pump).
        /// Null while the check is pending, after the handover, and always when up to date.</summary>
        public static string? TakeUpdate()
        {
            string? version = _available;
            if (version != null) _available = null;
            return version;
        }

        /// <summary>The version the release JSON's tag names ("v1.2.0" and "1.2.0" both read "1.2.0"),
        /// or null when no tag is found.</summary>
        public static string? ParseLatestVersion(string releaseJson)
        {
            Match match = TagPattern.Match(releaseJson);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>Whether <paramref name="latest"/> is an upgrade over <paramref name="current"/>:
        /// a numeric compare when both parse as versions, else any difference counts (a renamed
        /// scheme should still be offered rather than silently ignored).</summary>
        public static bool IsNewer(string current, string latest)
        {
            if (Version.TryParse(current, out Version currentVersion)
                && Version.TryParse(latest, out Version latestVersion))
                return latestVersion > currentVersion;
            return !string.Equals(current, latest, StringComparison.Ordinal);
        }
    }
}
