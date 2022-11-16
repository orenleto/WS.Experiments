namespace Daemon.Configurations;

public sealed class FileSystemEventConfiguration
{
    /// <summary>
    ///     Initializes a new <see cref="FileSystemEventConfiguration" />
    /// </summary>
    /// <param name="directory">Directory to monitor</param>
    /// <param name="filePattern">File pattern to monitor within directory</param>
    public FileSystemEventConfiguration(string directory, string? filePattern = null)
    {
        DirectoryToMonitor = directory;
        if (!string.IsNullOrEmpty(filePattern))
            DirectoryFileFilter = filePattern;
    }

    /// <summary>
    ///     Gets or sets <see cref="DuplicateEventDelayWindow" />, this
    ///     value represents the time to wait before posting and event from <see cref="System.IO.FileSystemWatcher" />
    ///     in order to verify it is not a duplicate event
    /// </summary>
    public TimeSpan DuplicateEventDelayWindow { get; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Gets or sets <see cref="DirectoryToMonitor" />, the directory to monitor for file changes
    /// </summary>
    public string DirectoryToMonitor { get; }

    /// <summary>
    ///     Gets or sets <see cref="DirectoryFileFilter" />, the filter to use for monitoring file changes,
    ///     default value is "*", for all files
    /// </summary>
    public string DirectoryFileFilter { get; } = "*";
}