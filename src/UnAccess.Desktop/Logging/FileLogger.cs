using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UnAccess.Desktop.Logging;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(LogLevel minimumLevel)
    {
        _minimumLevel = minimumLevel;
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();
        _directory = Path.Combine(root, "UnAccess", "logs");
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _directory, _minimumLevel));

    public void Dispose()
    {
    }
}

internal sealed class FileLogger : ILogger
{
    private static readonly object Gate = new();
    private readonly string _category;
    private readonly string _directory;
    private readonly LogLevel _minimumLevel;

    public FileLogger(string category, string directory, LogLevel minimumLevel)
    {
        _category = category;
        _directory = directory;
        _minimumLevel = minimumLevel;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel && logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        try
        {
            Directory.CreateDirectory(_directory);
            var line = $"{DateTime.UtcNow:O} {logLevel} [{_category}] {formatter(state, exception)}";
            if (exception != null)
                line += Environment.NewLine + exception;
            var path = Path.Combine(_directory, $"unaccess-{DateTime.UtcNow:yyyyMMdd}.log");
            lock (Gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception writeError)
        {
            Debug.WriteLine(writeError);
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
