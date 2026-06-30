using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BepInEx.Logging;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.UI.Nav;
using DiscoAccess.Modularity;

namespace DiscoAccess.Dev
{
    /// <summary>
    /// In-process dev driver, on by default (set DISCOACCESS_NO_DEV=1 to disable). The HTTP server
    /// binds 127.0.0.1 only (see <see cref="DevHttpServer"/>), so it is reachable from this machine
    /// alone. Exposes that loopback server so an external driver can:
    ///   POST /eval             body = C# source, run against the live game (REPL state persists
    ///                          across calls); returns output + result/errors.
    ///   POST /input            body = verb (up|down|left|right|confirm|back|tab|prev|home|end). Drives
    ///                          our own navigator when it owns the keyboard (a migrated screen or the popup
    ///                          overlay), else falls back to DE's focus system for not-yet-migrated screens.
    ///                          Enter/Escape on a focused text field commit/cancel the edit first.
    ///   POST /type             body = text appended to the focused input field (e.g. a save name).
    ///   POST /reload           rebuild the feature module from its freshly built DLL, no restart.
    ///   GET  /focus             the current uGUI selection (name/path/text), independent of speech.
    ///   GET  /nav               our navigator's own focus state (ownership, popup, focus path), which the
    ///                          game-level /focus cannot see; "[no module]" when the module is not loaded.
    ///   GET  /gui               raw dump of the active uGUI hierarchy (paths, component types, text,
    ///                          CanvasGroup alpha); surfaces structure /focus and /nav hide. Diff vs /nav.
    ///   GET  /speech?since=N    lines the mod has spoken since cursor N (we can't hear the TTS).
    ///   GET  /screenshot        capture a PNG of the current frame; returns the file path.
    ///   GET  /health            liveness.
    ///
    /// Eval / input / reload / screenshot run on the Unity main thread: HTTP requests enqueue a job and
    /// block until <see cref="Pump"/> (called from the host pump) executes it. /speech reads a
    /// thread-safe buffer directly off the HTTP thread. Not shipped to players.
    /// </summary>
    internal sealed class DevServer
    {
        public const string DisableEnv = "DISCOACCESS_NO_DEV";
        public const string PortEnv = "DISCOACCESS_DEV_PORT";
        private const int DefaultPort = 8771;

        private sealed class Job
        {
            public Func<string> Work;
            public string Result = "";
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private readonly ModuleLoader _loader;
        private readonly ManualLogSource _log;
        private readonly SpeechLog _speech = new SpeechLog();
        private readonly CSharpEvaluator _evaluator = new CSharpEvaluator();
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private DevHttpServer _http;
        private bool _enabled;
        private bool _runInBackgroundForced;
        private bool _warmedUp;

        public DevServer(ModuleLoader loader, ManualLogSource log)
        {
            _loader = loader;
            _log = log;
        }

        /// <summary>Stand up the loopback server unless DISCOACCESS_NO_DEV=1.</summary>
        public void Start()
        {
            if (Environment.GetEnvironmentVariable(DisableEnv) == "1")
            {
                _log.LogInfo("Dev server disabled (DISCOACCESS_NO_DEV=1)");
                return;
            }

            int port = DefaultPort;
            string p = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(p))
                int.TryParse(p, out port);

            // Tap every line the mod speaks (single chokepoint) into the ring buffer, tagging whether it
            // interrupted or queued so the dev driver can see speech policy it can't hear.
            SpeechPipeline.Spoken = (text, interrupt) => _speech.Add((interrupt ? "[interrupt] " : "[queue] ") + text);

            try
            {
                _http = new DevHttpServer(port, HandleRequest, _log.LogWarning);
                _http.Start();
                _enabled = true;
                _log.LogInfo("Dev server on http://127.0.0.1:" + port + " (POST /eval, GET /speech)");
            }
            catch (Exception e)
            {
                _log.LogError("Dev server failed to start: " + e);
            }
        }

        /// <summary>Run queued main-thread jobs. Call once per frame from the host pump.</summary>
        public void Pump()
        {
            if (!_enabled)
                return;
            if (!_runInBackgroundForced)
            {
                // We drive the game while its window is unfocused; keep it simulating when not focused.
                UnityEngine.Application.runInBackground = true;
                _runInBackgroundForced = true;
            }
            if (!_warmedUp)
            {
                // The first Roslyn compile loads its deps (Immutable/Metadata 7.0) through a one-time
                // cold assembly resolve that fails the first eval. Absorb it here on the main thread so
                // the first real /eval is clean. Stays loaded process-wide, so /reload re-inits cleanly.
                _warmedUp = true;
                _evaluator.Eval("1");
            }
            while (_jobs.TryDequeue(out Job job))
            {
                try
                {
                    job.Result = job.Work() ?? "";
                }
                catch (Exception e)
                {
                    job.Result = "[host error] " + e + "\n";
                }
                job.Done.Set();
            }
        }

        /// <summary>Run <paramref name="work"/> on the main thread (next Pump) and block for its result.</summary>
        private string OnMainThread(Func<string> work, int timeoutSeconds = 30)
        {
            var job = new Job { Work = work };
            _jobs.Enqueue(job);
            if (!job.Done.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
                return "[timeout] main thread did not run the job within " + timeoutSeconds + "s (frozen / not pumping?)\n";
            return job.Result;
        }

        // Runs on the HTTP thread.
        private string HandleRequest(string method, string path, string body)
        {
            string route = path;
            string query = "";
            int q = path.IndexOf('?');
            if (q >= 0)
            {
                route = path.Substring(0, q);
                query = path.Substring(q + 1);
            }

            if (route == "/eval" && method == "POST")
            {
                if (string.IsNullOrWhiteSpace(body))
                    return "[empty] POST C# source as the request body\n";
                return OnMainThread(() => _evaluator.Eval(body));
            }

            if (route == "/input" && method == "POST")
            {
                string verb = (body ?? "").Trim();
                return OnMainThread(() => DriveInput(verb));
            }

            if (route == "/type" && method == "POST")
                return OnMainThread(() => TextInjector.Type(body ?? ""));

            if (route == "/reload" && method == "POST")
                return OnMainThread(ReloadModule);

            if (route == "/focus" && method == "GET")
                return OnMainThread(FocusInspector.Describe);

            if (route == "/nav" && method == "GET")
                return OnMainThread(DescribeNav);

            if (route == "/gui" && method == "GET")
                return OnMainThread(GuiInspector.Describe);

            if (route == "/screenshot" && method == "GET")
                return Screenshot();

            if (route == "/speech" && method == "GET")
            {
                long since = 0;
                foreach (string kv in query.Split('&'))
                {
                    if (kv.StartsWith("since=", StringComparison.Ordinal))
                        long.TryParse(kv.Substring("since=".Length), out since);
                }
                string lines = _speech.Render(since, out long next);
                return "cursor: " + next + "\n" + lines;
            }

            if (route == "/health" || route == "/")
                return "ok\n";

            return "[404] " + method + " " + route + "\n";
        }

        // Drive input. Prefer our own navigator (the module's IDevDriver): on a migrated screen or the
        // popup overlay it owns the keyboard and the game's NavigationManager is muted, so the game injector
        // would no-op. When our navigator is not driving (a not-yet-migrated screen, or no module), fall
        // back to the game's focus system so the legacy follower can still be exercised.
        private string DriveInput(string verb)
        {
            string v = (verb ?? "").Trim().ToLowerInvariant();

            // A focused text field (a save-name edit) takes Enter/Escape first, committing or cancelling the
            // edit the way a real key would, before navigation or the game injector see them. No field
            // focused returns null, so these fall through to the normal handling below.
            if (v == "confirm" || v == "enter" || v == "ok")
            {
                string commit = TextInjector.TryCommit();
                if (commit != null) return "[field] " + commit + "\n";
            }
            else if (v == "back" || v == "escape" || v == "cancel")
            {
                string cancel = TextInjector.TryCancel();
                if (cancel != null) return "[field] " + cancel + "\n";
            }

            var driver = _loader.Module as IDevDriver;
            if (driver != null)
            {
                string action = VerbToAction(v);
                if (action != null)
                {
                    string r = driver.DispatchUi(action);
                    if (r != null)
                        return "[nav] " + r + "\n";
                }
            }
            return "[game] " + InputInjector.Inject(v);
        }

        // Map a dev verb to a UiActions key our navigator understands. Unknown verbs return null so /input
        // falls through to the game injector (which handles the directional/confirm/back subset).
        private static string VerbToAction(string v)
        {
            switch (v)
            {
                case "up": return UiActions.Up;
                case "down": return UiActions.Down;
                case "left": return UiActions.Left;
                case "right": return UiActions.Right;
                case "confirm": case "enter": case "ok": return UiActions.Activate;
                case "back": case "escape": case "cancel": return UiActions.Back;
                case "tab": case "next": return UiActions.Next;
                case "prev": case "shifttab": case "shift-tab": return UiActions.Prev;
                case "home": return UiActions.Home;
                case "end": return UiActions.End;
                case "secondary": case "backslash": return UiActions.Secondary;
                default: return null;
            }
        }

        // Our navigator's own state, from the module's IDevDriver. "[no module]" when none is loaded, so the
        // caller can tell "module dead" apart from "navigator idle".
        private string DescribeNav()
        {
            var driver = _loader.Module as IDevDriver;
            return driver != null ? driver.DescribeNav() : "[no module] nav driver unavailable\n";
        }

        /// <summary>F6 reloads through here (on the main thread) so the evaluator resets exactly like
        /// POST /reload, keeping the two reload entry points in sync.</summary>
        public string ReloadFromHost() => ReloadModule();

        // Reload the feature module, then reset the evaluator so /eval recompiles against the fresh
        // module types (the old ones leak in a pinned collectible context until process exit).
        private string ReloadModule()
        {
            bool ok = _loader.Reload();
            _evaluator.Reset();
            return ok ? "reloaded\n" : "[reload failed] see LogOutput.log\n";
        }

        // Trigger a screenshot on the main thread, then wait (on this HTTP thread) for the PNG, which
        // ScreenCapture writes asynchronously over the next frame(s). Returns the path, which the
        // driver then reads to view the frame.
        private string Screenshot()
        {
            string path = Path.Combine(Path.GetTempPath(), "disco_shot.png");
            // Only a file written at/after this instant counts as the new frame, so a stale leftover
            // (e.g. the prior PNG couldn't be deleted because a viewer holds it open) is never returned
            // as if fresh.
            DateTime requestedAt = DateTime.UtcNow;
            OnMainThread(() =>
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception e)
                {
                    _log.LogWarning("screenshot: could not delete stale " + path + ": " + e.Message);
                }
                UnityEngine.ScreenCapture.CaptureScreenshot(path);
                return "requested";
            });

            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < 8)
            {
                try
                {
                    if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= requestedAt)
                    {
                        long size = new FileInfo(path).Length;
                        if (size > 0)
                        {
                            Thread.Sleep(60); // let the write settle, then confirm size is stable
                            if (new FileInfo(path).Length == size)
                                return path + "\n";
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.LogWarning("screenshot: probe failed: " + e.Message);
                }
                Thread.Sleep(50);
            }
            return "[timeout] screenshot not written within 8s\n";
        }
    }
}
