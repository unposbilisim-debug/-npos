using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Security;

namespace UnAccess.Data.Infrastructure;

public sealed class SanitizingFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly int _retentionDays;
    private readonly ConcurrentDictionary<string, SanitizingLogger> _loggers = new();

    public SanitizingFileLoggerProvider(IAppPathProvider paths, LogLevel minimumLevel, int retentionDays = 14)
    {
        _directory = paths.LogDirectory;
        _minimumLevel = minimumLevel;
        _retentionDays = retentionDays < 1 ? 1 : retentionDays;
        TryPurge();
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new SanitizingLogger(name, _directory, _minimumLevel, Write));

    public void Dispose()
    {
    }

    private void Write(string line)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "unaccess-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
        lock (_loggers)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private void TryPurge()
    {
        try
        {
            if (!Directory.Exists(_directory))
                return;
            var cutoff = DateTime.UtcNow.Date.AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "unaccess-*.log"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var stamp = name.Length >= 17 ? name[^8..] : "";
                if (DateTime.TryParseExact(stamp, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day)
                    && day < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception)
        {
        }
    }
}

public sealed class SanitizingConsoleLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, SanitizingLogger> _loggers = new();

    public SanitizingConsoleLoggerProvider(LogLevel minimumLevel)
    {
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new SanitizingLogger(name, null, _minimumLevel, line => Console.Error.WriteLine(line)));

    public void Dispose()
    {
    }
}

internal sealed class SanitizingLogger : ILogger
{
    private readonly string _category;
    private readonly string? _directory;
    private readonly LogLevel _minimumLevel;
    private readonly Action<string> _write;

    public SanitizingLogger(string category, string? directory, LogLevel minimumLevel, Action<string> write)
    {
        _category = category;
        _directory = directory;
        _minimumLevel = minimumLevel;
        _write = write;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel && logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter == null)
            return;

        try
        {
            var message = SensitiveDataSanitizer.Clean(formatter(state, exception));
            var line = DateTime.UtcNow.ToString("O") + " " + logLevel + " [" + _category + "] " + message;
            if (exception != null)
                line += Environment.NewLine + SensitiveDataSanitizer.Clean(exception.ToString());
            _ = _directory;
            _write(line);
        }
        catch (Exception)
        {
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
