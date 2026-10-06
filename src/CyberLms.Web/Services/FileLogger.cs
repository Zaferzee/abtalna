using System.Text;

namespace CyberLms.Web.Services;

/// <summary>
/// Minimal rolling file logger for production (one file per day, old files purged). Under IIS the console is not captured, so this
/// is the primary application log. Configure with Logging:File:Path / RetentionDays / Enabled and the usual Logging:LogLevel filters.
/// It writes what the framework logs (exceptions, warnings) and never request bodies, passwords or connection strings.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly int _retentionDays;
    private readonly object _lock = new();
    private DateTime _purgedOn = DateTime.MinValue;

    public FileLoggerProvider(string directory, int retentionDays)
    {
        _dir = directory; _retentionDays = Math.Max(1, retentionDays);
        Directory.CreateDirectory(_dir);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    internal void Write(string category, LogLevel level, string message, Exception? ex)
    {
        var now = DateTime.UtcNow;
        var sb = new StringBuilder().Append(now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")).Append(" [").Append(level).Append("] ").Append(category).Append(": ").Append(message.Replace("\r", " ").Replace("\n", " "));
        if (ex != null) sb.AppendLine().Append(ex);
        sb.AppendLine();
        try
        {
            lock (_lock)
            {
                File.AppendAllText(Path.Combine(_dir, $"cyberlms-{now:yyyyMMdd}.log"), sb.ToString(), new UTF8Encoding(false));
                if (_purgedOn.Date != now.Date) { _purgedOn = now; Purge(now); }
            }
        }
        catch (IOException) { /* logging must never break a request */ }
        catch (UnauthorizedAccessException) { }
    }

    private void Purge(DateTime now)
    {
        foreach (var f in Directory.EnumerateFiles(_dir, "cyberlms-*.log"))
            if (File.GetLastWriteTimeUtc(f) < now.AddDays(-_retentionDays)) { try { File.Delete(f); } catch (IOException) { } }
    }

    private sealed class FileLogger(FileLoggerProvider p, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level != LogLevel.None;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> fmt)
        {
            if (!IsEnabled(level)) return;
            p.Write(category, level, fmt(state, ex), ex);
        }
    }
}
