namespace Daemon.Extensions;

internal static class LoggerExtensions
{
    private static readonly Action<ILogger, Exception> _callbackOverride = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(2, nameof(CallbackOverride)),
        "Activate is being overridden");

    private static readonly Action<ILogger, Exception> _cancellationRequested = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(3, nameof(CancellationRequested)),
        "Cancellation requested");

    private static readonly Action<ILogger, FileSystemEventArgs, Exception> _duplicateTimerRestart = LoggerMessage.Define<FileSystemEventArgs>(
        LogLevel.Trace,
        new EventId(6, nameof(DuplicateTimerRestart)),
        "Timer reset for: @{fileEventArgs}");

    private static readonly Action<ILogger, FileSystemEventArgs, Exception> _enqueue = LoggerMessage.Define<FileSystemEventArgs>(
        LogLevel.Debug,
        new EventId(5, nameof(Enqueue)),
        "File queued: @{fileEventArgs}");

    private static readonly Action<ILogger, string, string, Exception> _initializing = LoggerMessage.Define<string, string>(
        LogLevel.Debug,
        new EventId(1, nameof(Initializing)),
        "Initializing {type} {extraInformation}");

    private static readonly Action<ILogger, FileSystemEventArgs, Exception> _originalTimerAdded = LoggerMessage.Define<FileSystemEventArgs>(
        LogLevel.Trace,
        new EventId(6, nameof(OriginalTimerAdded)),
        "Added timer for new item: @{fileEventArgs}");

    private static readonly Action<ILogger, Exception> _queuingInitialFiles = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(4, nameof(QueuingInitialFiles)),
        "Queuing initial files");

    public static void CallbackOverride(this ILogger logger)
    {
        _callbackOverride(logger, null);
    }

    public static void CancellationRequested(this ILogger logger)
    {
        _cancellationRequested(logger, null);
    }

    public static void DuplicateTimerRestart(this ILogger logger, FileSystemEventArgs? fileSystemEventArgs)
    {
        _duplicateTimerRestart(logger, fileSystemEventArgs, null);
    }

    public static void Enqueue(this ILogger logger, FileSystemEventArgs? fileSystemEventArgs)
    {
        _enqueue(logger, fileSystemEventArgs, null);
    }

    public static void Initializing<T>(this ILogger logger, string extraInformation = null)
    {
        _initializing(logger, typeof(T).Name, extraInformation, null);
    }

    public static void OriginalTimerAdded(this ILogger logger, FileSystemEventArgs? fileSystemEventArgs)
    {
        _originalTimerAdded(logger, fileSystemEventArgs, null);
    }

    public static void QueuingInitialFiles(this ILogger logger)
    {
        _queuingInitialFiles(logger, null);
    }
}