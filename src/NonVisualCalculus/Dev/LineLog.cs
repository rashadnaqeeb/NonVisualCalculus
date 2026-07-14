using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NonVisualCalculus.Dev
{
    /// <summary>
    /// Thread-safe ring buffer of lines with stable monotonic indices, the dev server's read-back
    /// primitive: one instance holds what the mod has spoken (the driver can't hear TTS), another the
    /// mod's log lines (so the driver need not grep LogOutput.log on disk). Callers poll with a cursor
    /// to get only what's new; <see cref="WaitForNew"/> long-polls so a driver can block until a line
    /// lands instead of sleep-looping.
    /// </summary>
    internal sealed class LineLog
    {
        private const int Capacity = 500;

        private readonly object _lock = new object();
        private readonly List<string> _lines = new List<string>();
        private long _base; // global index of _lines[0]

        public void Add(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            lock (_lock)
            {
                _lines.Add(text);
                if (_lines.Count > Capacity)
                {
                    _lines.RemoveAt(0);
                    _base++;
                }
                Monitor.PulseAll(_lock); // wake any long-poll waiter
            }
        }

        /// <summary>The cursor one past the newest line (what to pass as since to get only the future).</summary>
        public long End
        {
            get { lock (_lock) return _base + _lines.Count; }
        }

        /// <summary>
        /// Render lines with global index &gt;= <paramref name="since"/>, one per line as "index: text".
        /// <paramref name="next"/> returns the cursor to pass next time to get only newer lines.
        /// </summary>
        public string Render(long since, out long next)
        {
            lock (_lock)
            {
                long end = _base + _lines.Count; // exclusive
                if (since < _base)
                    since = _base;
                var sb = new StringBuilder();
                for (long i = since; i < end; i++)
                    sb.Append(i).Append(": ").Append(_lines[(int)(i - _base)]).Append('\n');
                next = end;
                return sb.ToString();
            }
        }

        /// <summary>Block (on the calling HTTP thread) until a line with index &gt;= <paramref name="since"/>
        /// exists or <paramref name="timeoutMs"/> passes. Returns whether a line arrived.</summary>
        public bool WaitForNew(long since, int timeoutMs)
        {
            var deadline = System.DateTime.UtcNow.AddMilliseconds(timeoutMs);
            lock (_lock)
            {
                while (_base + _lines.Count <= since)
                {
                    double left = (deadline - System.DateTime.UtcNow).TotalMilliseconds;
                    if (left <= 0 || !Monitor.Wait(_lock, (int)left))
                        return false;
                }
                return true;
            }
        }
    }
}
