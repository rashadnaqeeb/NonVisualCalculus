using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    ///                          across calls); returns output + result/errors, then a "speech:" section
    ///                          with any lines spoken as a consequence (waits for a quiet window;
    ///                          ?speech=0 skips, ?settle=MS tunes the window, default 250).
    ///   POST /input            body = verb. UI verbs (up|down|left|right|confirm|back|tab|prev|home|end|
    ///                          secondary) drive our navigator when it owns the keyboard, else fall back
    ///                          to DE's focus system. World verbs (interact|stop|recenter|scan-next|
    ///                          scan-prev|scan-category-next|scan-category-prev|scan-people[-prev]|
    ///                          scan-items[-prev]|scan-exits[-prev]|scan-interact,
    ///                          or any raw "world.*"/"status" action key) fire the world reader's own
    ///                          handlers while it owns the keyboard. Enter/Escape on a focused text
    ///                          field commit/cancel the edit first.
    ///   POST /type             body = text appended to the focused input field (e.g. a save name).
    ///   POST /wait?timeout=MS  body = C# bool expression, compiled in the /eval session and evaluated
    ///                          each frame on the main thread; returns when true or on timeout. Replaces
    ///                          curl sleep-loops, and samples every frame so transient states
    ///                          (movementStatus flickering through IDLE) are never missed.
    ///   POST /reload           rebuild the feature module from its freshly built DLL, no restart;
    ///                          returns the /module readout so a stale DLL or lost patches are visible.
    ///   GET  /module            module type + load generation, the DLL's write time, and the Harmony
    ///                          patch table with LIVE counts (0-live = applied then stripped).
    ///   GET  /typeinfo?name=X   find a type by simple name (loaded + interop) and print its full name,
    ///                          assembly, and public members; kills namespace guessing in the REPL.
    ///   GET  /focus             the current uGUI selection (name/path/text), independent of speech.
    ///   GET  /nav               our navigator's own focus state (ownership, popup, focus path), which the
    ///                          game-level /focus cannot see; "[no module]" when the module is not loaded.
    ///   GET  /gui               raw dump of the active uGUI hierarchy (paths, component types, text,
    ///                          CanvasGroup alpha); surfaces structure /focus and /nav hide. Diff vs /nav.
    ///   GET  /speech?since=N    lines the mod has spoken since cursor N, each tagged with its speaking
    ///                          class; &wait=MS long-polls until a new line lands or the timeout passes.
    ///   GET  /log?since=N       the mod's log lines (a BepInEx listener; no grepping LogOutput.log),
    ///                          same cursor protocol as /speech; &grep=S filters to lines containing S.
    ///   GET  /screenshot        capture a PNG of the current frame; returns the file path.
    ///   GET  /health            liveness.
    ///
    /// Eval / input / reload / screenshot run on the Unity main thread: HTTP requests enqueue a job and
    /// block until <see cref="Pump"/> (called from the host pump) executes it. /speech and /log read
    /// thread-safe buffers directly off the HTTP thread. Not shipped to players.
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

        // A /wait in flight: the compiled predicate, evaluated once per frame from Pump until it turns
        // true, throws, or the HTTP thread gives up (Cancelled) - whichever comes first.
        private sealed class WaitJob
        {
            public Func<bool> Predicate;
            public string Outcome;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public readonly System.Diagnostics.Stopwatch Elapsed = System.Diagnostics.Stopwatch.StartNew();
            public volatile bool Cancelled;
        }

        private readonly ModuleLoader _loader;
        private readonly ManualLogSource _log;
        private readonly LineLog _speech = new LineLog();
        private readonly LineLog _logLines = new LineLog();
        private readonly CSharpEvaluator _evaluator = new CSharpEvaluator();
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private readonly List<WaitJob> _waits = new List<WaitJob>(); // guarded by lock(_waits)
        private DevHttpServer _http;
        private DevLogListener _logListener;
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
            // interrupted or queued, and which class spoke it, so the dev driver can see speech policy
            // and attribution it can't hear.
            SpeechPipeline.Spoken = (text, interrupt, source) => _speech.Add(
                (interrupt ? "[interrupt] " : "[queue] ")
                + (string.IsNullOrEmpty(source) ? "" : "[" + source + "] ")
                + text);

            // Mirror every BepInEx log event into the /log ring so the driver reads the log in-band
            // (cursors, long-poll-able) instead of grepping LogOutput.log on disk.
            _logListener = new DevLogListener(_logLines);
            Logger.Listeners.Add(_logListener);

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

        /// <summary>Run queued main-thread jobs and pending /wait predicates. Call once per frame from
        /// the host pump.</summary>
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
            PumpWaits();
        }

        // Evaluate each pending /wait predicate once this frame. Per-frame sampling is the point: an
        // external poll loop can only sample between frames and misses transient states.
        private void PumpWaits()
        {
            lock (_waits)
            {
                for (int i = _waits.Count - 1; i >= 0; i--)
                {
                    WaitJob w = _waits[i];
                    if (w.Cancelled)
                    {
                        _waits.RemoveAt(i);
                        continue;
                    }
                    try
                    {
                        if (!w.Predicate())
                            continue;
                        w.Outcome = "[true] after " + w.Elapsed.ElapsedMilliseconds + "ms\n";
                    }
                    catch (Exception e)
                    {
                        w.Outcome = "[exception] " + e + "\n";
                    }
                    w.Done.Set();
                    _waits.RemoveAt(i);
                }
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
            var query = new Dictionary<string, string>(StringComparer.Ordinal);
            int q = path.IndexOf('?');
            if (q >= 0)
            {
                route = path.Substring(0, q);
                foreach (string kv in path.Substring(q + 1).Split('&'))
                {
                    int eq = kv.IndexOf('=');
                    if (eq > 0)
                        query[kv.Substring(0, eq)] = Uri.UnescapeDataString(kv.Substring(eq + 1));
                }
            }

            if (route == "/eval" && method == "POST")
            {
                if (string.IsNullOrWhiteSpace(body))
                    return "[empty] POST C# source as the request body\n";
                return EvalWithSpeech(body, query);
            }

            if (route == "/input" && method == "POST")
            {
                string verb = (body ?? "").Trim();
                return OnMainThread(() => DriveInput(verb));
            }

            if (route == "/type" && method == "POST")
                return OnMainThread(() =>
                {
                    // A mod-owned text edit (a bookmark name) has no focused InputField to inject into;
                    // the module routes the text itself and reports null when no mod edit is active, so
                    // the game-field injector stays the fallback.
                    var driver = _loader.Module as IDevDriver;
                    string viaModule = driver != null ? driver.TypeText(body ?? "") : null;
                    return viaModule ?? TextInjector.Type(body ?? "");
                });

            if (route == "/wait" && method == "POST")
                return Wait(body, QueryInt(query, "timeout", 10000, 100, 120000));

            if (route == "/reload" && method == "POST")
                return OnMainThread(ReloadModule);

            if (route == "/module" && method == "GET")
                return OnMainThread(() => ModuleInspector.Describe(_loader));

            if (route == "/typeinfo" && method == "GET")
                return OnMainThread(() => TypeFinder.Describe(query.TryGetValue("name", out string n) ? n : ""));

            if (route == "/focus" && method == "GET")
                return OnMainThread(FocusInspector.Describe);

            if (route == "/nav" && method == "GET")
                return OnMainThread(DescribeNav);

            if (route == "/gui" && method == "GET")
                return OnMainThread(GuiInspector.Describe);

            if (route == "/screenshot" && method == "GET")
                return Screenshot();

            if (route == "/speech" && method == "GET")
                return ReadLines(_speech, query);

            if (route == "/log" && method == "GET")
                return ReadLines(_logLines, query, query.TryGetValue("grep", out string g) ? g : null);

            if (route == "/health" || route == "/")
                return "ok\n";

            return "[404] " + method + " " + route + "\n";
        }

        // Shared /speech and /log read: cursor render, optional long-poll (wait=MS blocks until a line
        // newer than since lands), optional substring filter.
        private static string ReadLines(LineLog log, Dictionary<string, string> query, string grep = null)
        {
            long since = 0;
            if (query.TryGetValue("since", out string s))
                long.TryParse(s, out since);
            int wait = QueryInt(query, "wait", 0, 0, 120000);
            if (wait > 0)
                log.WaitForNew(since, wait);
            string lines = log.Render(since, out long next);
            if (!string.IsNullOrEmpty(grep))
            {
                var kept = new System.Text.StringBuilder();
                foreach (string line in lines.Split('\n'))
                    if (line.IndexOf(grep, StringComparison.OrdinalIgnoreCase) >= 0)
                        kept.Append(line).Append('\n');
                lines = kept.ToString();
            }
            return "cursor: " + next + "\n" + lines;
        }

        // Run an eval, then read back what it caused the mod to SAY: announcements land on later frames
        // (a state change speaks from the next pump drain), so after the eval returns we wait for a
        // quiet window (no new speech for settle ms, capped overall) and append whatever arrived. This
        // folds the act-then-listen loop into one request; ?speech=0 skips the wait entirely.
        private string EvalWithSpeech(string code, Dictionary<string, string> query)
        {
            bool withSpeech = !query.TryGetValue("speech", out string sp) || sp != "0";
            int settle = QueryInt(query, "settle", 250, 0, 2000);

            long cursor = _speech.End;
            string result = OnMainThread(() => _evaluator.Eval(code));
            if (!withSpeech || settle == 0)
                return result;

            // Extend while lines keep arriving, so a multi-line announcement (walk feedback then the
            // arrival) is captured whole; the overall cap keeps a chatty ambient scene from pinning us.
            var overall = System.Diagnostics.Stopwatch.StartNew();
            long seen = cursor;
            while (overall.ElapsedMilliseconds < 5000 && _speech.WaitForNew(seen, settle))
                seen = _speech.End;

            string spoken = _speech.Render(cursor, out _);
            return spoken.Length == 0 ? result : result + "speech:\n" + spoken;
        }

        // Compile the predicate in the REPL session (so it can use variables prior evals defined), then
        // hand it to the pump and block this HTTP thread until it turns true, throws, or times out.
        private string Wait(string body, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "[empty] POST a C# bool expression as the request body\n";

            Func<bool> predicate = null;
            string error = null;
            OnMainThread(() =>
            {
                error = _evaluator.CompilePredicate(body, out predicate);
                return "";
            });
            if (error != null)
                return error;

            var wait = new WaitJob { Predicate = predicate };
            lock (_waits)
                _waits.Add(wait);
            if (wait.Done.Wait(timeoutMs))
                return wait.Outcome;
            wait.Cancelled = true;
            return "[timeout] not true within " + timeoutMs + "ms\n";
        }

        private static int QueryInt(Dictionary<string, string> query, string key, int fallback, int min, int max)
        {
            if (!query.TryGetValue(key, out string raw) || !int.TryParse(raw, out int value))
                return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        // Drive input. Prefer our own navigator (the module's IDevDriver): on a migrated screen or the
        // popup overlay it owns the keyboard and the game's NavigationManager is muted, so the game injector
        // would no-op. A world verb fires the world reader's own registered handler instead. When neither
        // of our layers is driving (a not-yet-migrated screen, or no module), fall back to the game's
        // focus system so the legacy follower can still be exercised.
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
                string world = VerbToWorldAction(v);
                if (world != null)
                {
                    string r = driver.DriveWorld(world);
                    // A world verb aimed at a world that is not driving is reported, never re-routed:
                    // falling through to the game injector would poke DE's menu focus instead.
                    return "[world] " + (r ?? "not driving (a menu or a text edit owns the keyboard)") + "\n";
                }
            }
            return "[game] " + InputInjector.Inject(v);
        }

        // Map a dev verb to a UiActions key our navigator understands. Unknown verbs return null so /input
        // falls through to the world mapping, then the game injector.
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
                case "secondary": case "backspace": return UiActions.Secondary;
                default: return null;
            }
        }

        // Map a dev verb to a world-layer action key. The keys are the module's registered action ids
        // (WorldActions), duplicated here as the dev wire protocol - the host cannot reference the
        // reloadable module's types. A dotted verb passes through as a raw key, so every registered
        // world/status action - and the "mod." global toggles (mod.menu, mod.bookmarks) - is reachable
        // without a new alias.
        private static string VerbToWorldAction(string v)
        {
            if (v.StartsWith("world.", StringComparison.Ordinal) || v.StartsWith("mod.", StringComparison.Ordinal))
                return v;
            switch (v)
            {
                case "interact": return "world.interact";
                case "stop": return "world.stop";
                case "recenter": return "world.recenter";
                case "scan-next": case "scannext": return "world.scan.next";
                case "scan-prev": case "scanprev": return "world.scan.prev";
                case "scan-category-next": case "scan-cat-next": return "world.scan.category.next";
                case "scan-category-prev": case "scan-cat-prev": return "world.scan.category.prev";
                case "scan-people": case "scan-people-next": return "world.scan.people.next";
                case "scan-people-prev": return "world.scan.people.prev";
                case "scan-items": case "scan-items-next": return "world.scan.items.next";
                case "scan-items-prev": return "world.scan.items.prev";
                case "scan-exits": case "scan-exits-next": return "world.scan.exits.next";
                case "scan-exits-prev": return "world.scan.exits.prev";
                case "scan-interact": return "world.scan.interact";
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
        // module types (the old ones leak in a pinned collectible context until process exit). The
        // response is the full /module readout: the DLL write time catches a stale deploy, and the
        // patch table catches patches lost in the swap - both otherwise invisible until a feature
        // fails silently.
        private string ReloadModule()
        {
            bool ok = _loader.Reload();
            _evaluator.Reset();
            return (ok ? "reloaded\n" : "[reload failed] see /log\n") + ModuleInspector.Describe(_loader);
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
