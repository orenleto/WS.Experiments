namespace Daemon.Interfaces;

public interface IWatcher : IDisposable
{
    string Directory { get; }
    int Subscribers { get; }
    void AddCallback(Action<FileSystemEventArgs> callback);
    void RemoveCallback(Action<FileSystemEventArgs> callback);
}