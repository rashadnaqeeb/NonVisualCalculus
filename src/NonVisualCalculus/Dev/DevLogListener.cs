using BepInEx.Logging;

namespace NonVisualCalculus.Dev
{
    /// <summary>
    /// Mirrors every BepInEx log event into the dev server's /log ring buffer, in the disk log's
    /// "[Level:Source] text" shape, so a driver reads the log in-band with cursors and long-polling
    /// instead of grepping LogOutput.log (a spaces-in-path file that truncates each launch).
    /// </summary>
    internal sealed class DevLogListener : ILogListener
    {
        private readonly LineLog _lines;

        public DevLogListener(LineLog lines) => _lines = lines;

        public LogLevel LogLevelFilter => LogLevel.All;

        public void LogEvent(object sender, LogEventArgs eventArgs)
            => _lines.Add("[" + eventArgs.Level + ":" + eventArgs.Source.SourceName + "] " + eventArgs.Data);

        public void Dispose() { }
    }
}
